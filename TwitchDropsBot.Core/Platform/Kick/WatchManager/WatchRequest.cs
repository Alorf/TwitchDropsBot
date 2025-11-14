using System.Net.WebSockets;
using System.Text;
using System.Threading;
using Serilog;
using Serilog.Core;
using TwitchDropsBot.Core.Platform.Kick.Bot;
using TwitchDropsBot.Core.Platform.Kick.Device;
using TwitchDropsBot.Core.Platform.Kick.Models;
using TwitchDropsBot.Core.Platform.Kick.Repository;
using TwitchDropsBot.Core.Platform.Shared.Exceptions;

namespace TwitchDropsBot.Core.Platform.Kick.WatchManager;

public class WatchRequest : IKickWatchManager
{
    // {"type":"user_event","data":{"message":{"name":"tracking.user.watch.livestream","channel_id":,"livestream_id":}}} send every 2 minutes
    // {"type":"ping"} every 30 sec
    // {"type":"channel_handshake","data":{"message":{"channelId":""}}} every 15 sec
    // {"type":"channel_disconnect","data":{"message":{"channelId":""}}} When stream goes offline

    private const string WEBSOCKET_CONNECTION_URL = "wss://websockets.kick.com/viewer/v1/connect";

    private string _wssToken;
    private CancellationTokenSource _cancellationTokenSource;

    private ClientWebSocket _clientWebSocket;
    private readonly KickHttpRepository _kickHttpRepository;
    private ILogger _logger;
    private Task? _receivingTask;


    public WatchRequest(KickHttpRepository kickHttpRepository, ILogger logger)
    {
        _logger = logger;
        _kickHttpRepository = kickHttpRepository;
        _clientWebSocket = new ClientWebSocket();
        _cancellationTokenSource = new CancellationTokenSource();
    }

    public KickUser BotUser { get; }

    public async Task WatchStreamAsync(Channel broadcaster, Category category)
    {
        var channel = await _kickHttpRepository.GetChannelAsync(broadcaster.slug);

        if (channel?.Livestream is null)
        {
            var disconnectMsg =
                $"{{\"type\":\"channel_disconnect\",\"data\":{{\"message\":{{\"channelId\":\"{broadcaster.Id}\"}}}}}}";

            if (_clientWebSocket.State == WebSocketState.Open)
            {
                await SendMessageAsync(disconnectMsg, _cancellationTokenSource.Token);
                _logger.Debug("Sent channel_disconnect message for offline stream.");
            }

            throw new StreamOffline();
        }

        if (channel?.Livestream?.Category?.Contains(category, Category.IdComparer) == false)
        {
            throw new StreamOffline();
        }

        try
        {
            if (string.IsNullOrEmpty(_wssToken))
            {
                _wssToken = await _kickHttpRepository.GetWssToken();
            }

            if (_clientWebSocket.State != WebSocketState.Open)
            {
                var uri = new Uri($"{WEBSOCKET_CONNECTION_URL}?token={_wssToken}");
                await _clientWebSocket.ConnectAsync(uri, _cancellationTokenSource.Token);

                _receivingTask = Task.Run(() => ReceiveLoopAsync(_cancellationTokenSource.Token));

                var livestreamId = channel.Livestream.Id;

                _ = Task.Run(() => SendPeriodicMessages(broadcaster, livestreamId, _cancellationTokenSource.Token));
            }
        }
        catch (WebSocketException ex)
        {
            _logger.Error(ex, ex.Message);
            Close();
        }
    }

    private async Task SendPeriodicMessages(Channel broadcaster, dynamic livestreamId,
        CancellationToken token = default)
    {
        var channelId = broadcaster.Id;

        var pingInterval = TimeSpan.FromSeconds(30);
        var handshakeInterval = TimeSpan.FromSeconds(15);
        var trackingInterval = TimeSpan.FromMinutes(2);

        var lastPing = DateTime.MinValue;
        var lastHandshake = DateTime.MinValue;
        var lastTracking = DateTime.MinValue;

        while (!token.IsCancellationRequested && _clientWebSocket.State == WebSocketState.Open)
        {
            var now = DateTime.UtcNow;

            if (now - lastPing > pingInterval)
            {
                await SendMessageAsync("{\"type\":\"ping\"}", token);
                _logger.Debug("Sending ping message");
                lastPing = now;
            }

            if (now - lastHandshake > handshakeInterval)
            {
                var handshakeMsg =
                    $"{{\"type\":\"channel_handshake\",\"data\":{{\"message\":{{\"channelId\":\"{channelId}\"}}}}}}";
                await SendMessageAsync(handshakeMsg, token);
                _logger.Debug("Sending handshake message");
                lastHandshake = now;
            }

            if (now - lastTracking > trackingInterval)
            {
                var trackingMsg =
                    $"{{\"type\":\"user_event\",\"data\":{{\"message\":{{\"name\":\"tracking.user.watch.livestream\",\"channel_id\":{channelId},\"livestream_id\":{livestreamId}}}}}}}";
                await SendMessageAsync(trackingMsg, token);
                _logger.Debug("Sending tracking message");
                lastTracking = now;
            }

            await Task.Delay(1000, token);
        }
    }

    private async Task SendMessageAsync(string message, CancellationToken token)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        var buffer = new ArraySegment<byte>(bytes);

        if (_clientWebSocket.State == WebSocketState.Open)
        {
            await _clientWebSocket.SendAsync(buffer, WebSocketMessageType.Text, true, token);
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken token)
    {
        var buffer = new byte[16 * 1024];

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
                        _logger.Information("Received close frame: {Status} - {Desc}", result.CloseStatus,
                            result.CloseStatusDescription);
                        try
                        {
                            _clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Acknowledging close",
                                CancellationToken.None).GetAwaiter().GetResult();
                        }
                        catch (Exception ex)
                        {
                            _logger.Debug(ex, "Error while responding to close frame");
                            throw new Exception("Error while responding to close frame");
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

                    _logger.Debug("WebSocket received (text): {Message}", messageText);

                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(messageText);
                        if (doc.RootElement.TryGetProperty("type", out var typeEl))
                        {
                            _logger.Information("WS message type: {Type}", typeEl.GetString());
                        }
                    }
                    catch (System.Text.Json.JsonException)
                    {
                    }
                }
                else if (result.MessageType == WebSocketMessageType.Binary)
                {
                    var len = ms.Length;
                    _logger.Debug("WebSocket received (binary) length: {Len} bytes", len);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("Receive loop cancelled.");
        }
        catch (WebSocketException wsex)
        {
            _logger.Error(wsex, "WebSocket exception in receive loop");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Unhandled exception in ReceiveLoopAsync");
        }
    }

    public void Close()
    {
        try
        {
            if (_clientWebSocket.State == WebSocketState.Open)
            {
                _clientWebSocket
                    .CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing connection", CancellationToken.None)
                    .GetAwaiter().GetResult();
            }

            _cancellationTokenSource.Cancel();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, ex.Message);
        }
    }
}