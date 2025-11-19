using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TwitchDropsBot.Core.Platform.Shared.Services;

namespace TwitchDropsBot.Core.Platform.Kick.Services;

public class KickAuthPollService
{
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public KickAuthPollService(HttpClient? client = null)
    {
        if (client is null)
        {
            var handler = new SocketsHttpHandler
            {
                CookieContainer = new CookieContainer(),
                AllowAutoRedirect = true
            };
            _client = new HttpClient(handler) { BaseAddress = new Uri("https://kick.com") };
            
            _client.DefaultRequestVersion = HttpVersion.Version30;
            _client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;

            _client.DefaultRequestHeaders.UserAgent.ParseAdd("okhttp/4.7.2");
        }
        else
        {
            _client = client;
        }
    }

    public async Task<string?> PollAuthenticateAsync(string uuid, string code, int pollIntervalSeconds = 5,
        int pollDurationSeconds = 30, CancellationToken ct = default)
    {
        var endPoll = DateTime.Now.AddSeconds(pollDurationSeconds);
        
        var requestUri = $"/api/tv/link/authenticate/{uuid}";
        var payload = new { key = code }; 
        
        while (DateTime.UtcNow < endPoll)
        {
            using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            
            using var req = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = content,
                Version = new Version(3, 0),
                VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher
            };
            
            HttpResponseMessage resp = await _client.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            
            if (resp.IsSuccessStatusCode)
            {
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("token", out var pToken) && pToken.ValueKind == JsonValueKind.String)
                        return pToken.GetString();
                }
                catch (JsonException)
                {
                    // body is not valid JSON, fall through to return raw body
                }

                throw new Exception();
            }
            
            await Task.Delay(TimeSpan.FromSeconds(pollIntervalSeconds), ct);
            SystemLoggerService.Logger.Information("Waiting the user to log...");
        }
        
        return null;
    }

    public async Task<(string? id, string? username)> GetUserInfo(string token, CancellationToken ct = default)
    {
        var requestUri = $"https://kick.com/api/v1/user";
        
        using var req = new HttpRequestMessage(HttpMethod.Get, requestUri)
        {
            Version = HttpVersion.Version30,
            VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher
        };

        // Add bearer token authorization
        req.Headers.Add("Authorization", $"Bearer {token}");

        // Retrieve {"id":..., "username":...}
        HttpResponseMessage resp = await _client.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (resp.IsSuccessStatusCode)
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            
            string? idStr = null;
            string? username = null;

            if (root.TryGetProperty("id", out var pId))
            {
                if (pId.ValueKind == JsonValueKind.String)
                    idStr = pId.GetString();
                else if (pId.ValueKind == JsonValueKind.Number && pId.TryGetInt64(out var idNum))
                    idStr = idNum.ToString();
                else
                    idStr = pId.ToString();
            }

            if (root.TryGetProperty("username", out var pUser) && pUser.ValueKind == JsonValueKind.String)
                username = pUser.GetString();

            if (idStr is not null && username is not null)
            {
                return (idStr, username);
            }
        }

        return (null, null);
    }
}