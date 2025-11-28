using System.Net.WebSockets;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using TwitchDropsBot.Core.Platform.Kick.Bot;
using TwitchDropsBot.Core.Platform.Kick.Device;
using TwitchDropsBot.Core.Platform.Kick.Models;
using TwitchDropsBot.Core.Platform.Kick.Repository;
using TwitchDropsBot.Core.Platform.Shared.Bots;
using TwitchDropsBot.Core.Platform.Shared.Exceptions;

namespace TwitchDropsBot.Core.Platform.Kick.WatchManager;

public class WatchRequest : IKickWatchManager
{
    private const string WEBSOCKET_CONNECTION_URL = "wss://websockets.kick.com/viewer/v1/connect";

    private string _wssToken;
    private CancellationTokenSource _cancellationTokenSource;
    private ClientWebSocket _clientWebSocket;
    private readonly KickHttpRepository _kickHttpRepository;
    private readonly ILogger _logger;
    private Task? _receivingTask;
    private Task? _sendingTask;
    private readonly SemaphoreSlim _connectionLock = new SemaphoreSlim(1, 1);

    public WatchRequest(KickHttpRepository kickHttpRepository, ILogger logger)
    {
        _logger = logger;
        _kickHttpRepository = kickHttpRepository;
        _clientWebSocket = new ClientWebSocket();
        _cancellationTokenSource = new CancellationTokenSource();
        
        _logger.LogInformation("WatchRequest initialized");
    }

    public KickUser BotUser { get; }

    public async Task WatchStreamAsync(Channel broadcaster, Category category)
    {
        _logger.LogDebug("Attempting to watch stream for channel {ChannelSlug}, category {CategoryName}", 
            broadcaster.slug, category.Name);

        var channel = await _kickHttpRepository.GetChannelAsync(broadcaster.slug);

        if (channel?.Livestream is null)
        {
            _logger.LogInformation("Stream is offline for channel {ChannelSlug}", broadcaster.slug);
            
            var disconnectMsg =
                $"{{\"type\":\"channel_disconnect\",\"data\":{{\"message\":{{\"channelId\":\"{broadcaster.Id}\"}}}}}}";

            if (_clientWebSocket.State == WebSocketState.Open)
            {
                await SendMessageAsync(disconnectMsg, _cancellationTokenSource.Token);
                _logger.LogDebug("Sent channel_disconnect message for offline stream");
            }

            throw new StreamOffline();
        }

        if (channel?.Livestream?.Category?.Contains(category) == false)
        {
            _logger.LogWarning("Stream category mismatch for channel {ChannelSlug}. Expected {ExpectedCategory}", 
                broadcaster.slug, category.Name);
            throw new StreamOffline();
        }

        await _connectionLock.WaitAsync();
        try
        {
            // Check if we need to reconnect
            if (_clientWebSocket.State != WebSocketState.Open)
            {
                _logger.LogInformation("WebSocket not open (state: {State}), establishing new connection", 
                    _clientWebSocket.State);

                // Clean up old connection
                await CleanupConnectionAsync();

                // Refresh token for new connection
                _wssToken = await _kickHttpRepository.GetWssToken();
                _logger.LogDebug("Fetched new WSS token");

                // Create new WebSocket
                _clientWebSocket = new ClientWebSocket();
                _cancellationTokenSource = new CancellationTokenSource();

                var uri = new Uri($"{WEBSOCKET_CONNECTION_URL}?token={_wssToken}");
                
                try
                {
                    await _clientWebSocket.ConnectAsync(uri, _cancellationTokenSource.Token);
                    _logger.LogInformation("WebSocket connected successfully to {Url}", WEBSOCKET_CONNECTION_URL);
                }
                catch (WebSocketException ex)
                {
                    _logger.LogError(ex, "Failed to connect WebSocket");
                    await CleanupConnectionAsync();
                    throw;
                }

                var livestreamId = channel.Livestream.Id;
                _logger.LogDebug("Starting watch tasks for livestream {LivestreamId}", (object)livestreamId);

                // Start receive loop
                _receivingTask = Task.Run(() => ReceiveLoopAsync(_cancellationTokenSource.Token));

                // Start periodic message sender
                _sendingTask = Task.Run(() => SendPeriodicMessages(broadcaster, livestreamId, _cancellationTokenSource.Token));
            }
            else
            {
                _logger.LogDebug("WebSocket already open, reusing existing connection");
            }
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private async Task SendPeriodicMessages(Channel broadcaster, dynamic livestreamId,
        CancellationToken token = default)
    {
        var channelId = broadcaster.Id;
        _logger.LogDebug("Starting periodic message sender for channel {ChannelId}", channelId);

        var pingInterval = TimeSpan.FromSeconds(30);
        var handshakeInterval = TimeSpan.FromSeconds(15);
        var trackingInterval = TimeSpan.FromMinutes(2);

        var lastPing = DateTime.MinValue;
        var lastHandshake = DateTime.MinValue;
        var lastTracking = DateTime.MinValue;

        try
        {
            while (!token.IsCancellationRequested && _clientWebSocket.State == WebSocketState.Open)
            {
                var now = DateTime.UtcNow;

                if (now - lastPing > pingInterval)
                {
                    await SendMessageAsync("{\"type\":\"ping\"}", token);
                    _logger.LogDebug("Sent ping message");
                    lastPing = now;
                }

                if (now - lastHandshake > handshakeInterval)
                {
                    var handshakeMsg =
                        $"{{\"type\":\"channel_handshake\",\"data\":{{\"message\":{{\"channelId\":\"{channelId}\"}}}}}}";
                    await SendMessageAsync(handshakeMsg, token);
                    _logger.LogDebug("Sent handshake message for channel {ChannelId}", channelId);
                    lastHandshake = now;
                }

                if (now - lastTracking > trackingInterval)
                {
                    var trackingMsg =
                        $"{{\"type\":\"user_event\",\"data\":{{\"message\":{{\"name\":\"tracking.user.watch.livestream\",\"channel_id\":{channelId},\"livestream_id\":{livestreamId}}}}}}}";
                    await SendMessageAsync(trackingMsg, token);
                    _logger.LogDebug("Sent tracking message for channel {ChannelId}, livestream {LivestreamId}", 
                        channelId, (object)livestreamId);
                    lastTracking = now;
                }

                await Task.Delay(1000, token);
            }
            
            _logger.LogDebug("Periodic message sender stopped (cancelled: {Cancelled}, state: {State})", 
                token.IsCancellationRequested, _clientWebSocket.State);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Send loop cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SendPeriodicMessages");
        }
    }

    private async Task SendMessageAsync(string message, CancellationToken token)
    {
        try
        {
            if (_clientWebSocket.State == WebSocketState.Open)
            {
                var bytes = Encoding.UTF8.GetBytes(message);
                var buffer = new ArraySegment<byte>(bytes);
                await _clientWebSocket.SendAsync(buffer, WebSocketMessageType.Text, true, token);
            }
            else
            {
                _logger.LogWarning("Cannot send message, WebSocket state: {State}", _clientWebSocket.State);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send WebSocket message");
            throw;
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken token)
    {
        var buffer = new byte[16 * 1024];
        _logger.LogDebug("Starting WebSocket receive loop");

        try
        {
            while (!token.IsCancellationRequested && _clientWebSocket.State == WebSocketState.Open)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult? result;

                do
                {
                    result = await _clientWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), token);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.LogInformation("Received close frame - Status: {Status}, Description: {Description}", 
                            result.CloseStatus, result.CloseStatusDescription);
                        
                        if (_clientWebSocket.State == WebSocketState.Open || 
                            _clientWebSocket.State == WebSocketState.CloseReceived)
                        {
                            try
                            {
                                await _clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, 
                                    "Acknowledging close", CancellationToken.None);
                                _logger.LogDebug("Acknowledged close frame");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogDebug(ex, "Error while responding to close frame");
                            }
                        }
                        
                        return;
                    }

                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage && !token.IsCancellationRequested);

                ms.Seek(0, SeekOrigin.Begin);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    using var sr = new StreamReader(ms, Encoding.UTF8);
                    var messageText = await sr.ReadToEndAsync();

                    _logger.LogDebug("WebSocket received text message: {Message}", messageText);

                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(messageText);
                        if (doc.RootElement.TryGetProperty("type", out var typeEl))
                        {
                            var messageType = typeEl.GetString();
                            _logger.LogDebug("Received WebSocket message type: {Type}", messageType);
                        }
                    }
                    catch (System.Text.Json.JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse WebSocket message as JSON");
                    }
                }
                else if (result.MessageType == WebSocketMessageType.Binary)
                {
                    var len = ms.Length;
                    _logger.LogDebug("WebSocket received binary message, length: {Length} bytes", len);
                }
            }
            
            _logger.LogDebug("Receive loop ended (cancelled: {Cancelled}, state: {State})", 
                token.IsCancellationRequested, _clientWebSocket.State);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Receive loop cancelled");
        }
        catch (WebSocketException wsex)
        {
            _logger.LogError(wsex, "WebSocket exception in receive loop - Code: {ErrorCode}", wsex.WebSocketErrorCode);
            // Trigger cleanup on WebSocket errors
            _ = Task.Run(() => Close());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in ReceiveLoopAsync");
            // Trigger cleanup on any error
            _ = Task.Run(() => Close());
        }
    }

    private async Task CleanupConnectionAsync()
    {
        _logger.LogDebug("Starting connection cleanup");
        
        try
        {
            // Cancel any ongoing operations
            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
                _logger.LogDebug("Cancellation requested");
            }

            // Wait for tasks to complete
            if (_receivingTask != null && !_receivingTask.IsCompleted)
            {
                var receiveCompleted = await Task.WhenAny(_receivingTask, Task.Delay(2000));
                if (receiveCompleted != _receivingTask)
                {
                    _logger.LogWarning("Receiving task did not complete within timeout");
                }
            }

            if (_sendingTask != null && !_sendingTask.IsCompleted)
            {
                var sendCompleted = await Task.WhenAny(_sendingTask, Task.Delay(2000));
                if (sendCompleted != _sendingTask)
                {
                    _logger.LogWarning("Sending task did not complete within timeout");
                }
            }

            // Close WebSocket if needed
            if (_clientWebSocket != null)
            {
                if (_clientWebSocket.State == WebSocketState.Open ||
                    _clientWebSocket.State == WebSocketState.CloseReceived ||
                    _clientWebSocket.State == WebSocketState.CloseSent)
                {
                    try
                    {
                        await _clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure,
                            "Closing connection", CancellationToken.None);
                        _logger.LogDebug("WebSocket closed successfully");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Error closing WebSocket");
                    }
                }

                _clientWebSocket.Dispose();
                _logger.LogDebug("WebSocket disposed");
            }
            
            _logger.LogInformation("Connection cleanup completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cleanup");
        }
    }

    public void Close()
    {
        _logger.LogInformation("Closing WatchRequest");
        
        _connectionLock.Wait();
        try
        {
            CleanupConnectionAsync().GetAwaiter().GetResult();

            // Reset state
            _wssToken = null;
            _clientWebSocket = new ClientWebSocket();
            _cancellationTokenSource = new CancellationTokenSource();
            _receivingTask = null;
            _sendingTask = null;
            
            _logger.LogInformation("WatchRequest closed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while closing WatchRequest");
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public void Dispose()
    {
        _logger.LogDebug("Disposing WatchRequest");
        Close();
        _connectionLock?.Dispose();
    }
}