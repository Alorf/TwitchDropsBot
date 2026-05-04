using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TwitchDropsBot.Core.Platform.YouTube.Repository;

/// <summary>
/// Minimal client for YouTube's internal InnerTube API.
/// Inspired by LuanRT/YouTube.js — all requests are plain HTTP POSTs
/// with no external library dependencies.
/// </summary>
internal sealed class InnerTubeClient : IDisposable
{
    private const string BaseUrl       = "https://www.youtube.com/youtubei/v1/";
    private const string ApiKey        = "AIzaSyAO_FJ2SlqU8Q4STEHLGCilw_Y9_11qcW8";
    private const string ClientName    = "WEB";
    private const string ClientVersion = "2.20231219.04.00";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly bool _ownsHttpClient;

    public InnerTubeClient(HttpClient? httpClient = null)
    {
        if (httpClient is null)
        {
            _http = new HttpClient();
            _ownsHttpClient = true;
        }
        else
        {
            _http = httpClient;
            _ownsHttpClient = false;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public endpoints
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Calls the <c>/player</c> endpoint for the given video ID.
    /// The caller is responsible for disposing the returned <see cref="JsonDocument"/>.
    /// </summary>
    public Task<JsonDocument> PlayerAsync(string videoId, CancellationToken ct = default)
    {
        var payload = new
        {
            context       = BuildContext(),
            videoId,
            contentCheckOk = true,
            racyCheckOk    = true
        };
        return PostAsync("player", payload, ct);
    }

    /// <summary>
    /// Calls the <c>/browse</c> endpoint with the given <paramref name="browseId"/>
    /// and optional InnerTube <paramref name="params"/>.
    /// The caller is responsible for disposing the returned <see cref="JsonDocument"/>.
    /// </summary>
    public Task<JsonDocument> BrowseAsync(string browseId, string? @params = null, CancellationToken ct = default)
    {
        var payload = new
        {
            context  = BuildContext(),
            browseId,
            @params
        };
        return PostAsync("browse", payload, ct);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Internals
    // ─────────────────────────────────────────────────────────────────────────

    private static object BuildContext() => new
    {
        client = new
        {
            clientName    = ClientName,
            clientVersion = ClientVersion,
            hl = "en",
            gl = "US"
        }
    };

    private async Task<JsonDocument> PostAsync(string endpoint, object payload, CancellationToken ct)
    {
        var json    = JsonSerializer.Serialize(payload, SerializerOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var url     = $"{BaseUrl}{endpoint}?key={ApiKey}&prettyPrint=false";

        var response = await _http.PostAsync(url, content, ct);
        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
            _http.Dispose();
    }
}
