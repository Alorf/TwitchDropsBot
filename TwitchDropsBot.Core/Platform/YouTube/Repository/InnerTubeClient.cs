using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TwitchDropsBot.Core.Platform.YouTube.Repository;

/// <summary>
/// Minimal client for YouTube's internal InnerTube API.
/// All requests are plain HTTP POSTs with no external library dependencies.
/// </summary>
internal sealed class InnerTubeClient : IDisposable
{
    private const string BaseUrl       = "https://www.youtube.com/youtubei/v1/";
    private const string ClientName    = "WEB";
    private const string ClientNameId  = "1";
    private const string ClientVersion = "2.20260206.01.00";
    private const string UserAgent     =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
        "AppleWebKit/537.36 (KHTML, like Gecko) " +
        "Chrome/125.0.0.0 Safari/537.36";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly bool       _ownsHttpClient;

    public InnerTubeClient(HttpClient? httpClient = null)
    {
        if (httpClient is null)
        {
            _http           = new HttpClient();
            _ownsHttpClient = true;
        }
        else
        {
            _http           = httpClient;
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
            context        = BuildContext(),
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
            hl                 = "en",
            gl                 = "US",
            clientName         = ClientName,
            clientVersion      = ClientVersion,
            osName             = "Windows",
            osVersion          = "10.0",
            platform           = "DESKTOP",
            clientFormFactor   = "UNKNOWN_FORM_FACTOR",
            userInterfaceTheme = "USER_INTERFACE_THEME_LIGHT",
            timeZone           = "UTC",
            browserName        = "Chrome",
            browserVersion     = "125.0.0.0",
            userAgent          = UserAgent,
            originalUrl        = "https://www.youtube.com",
            mainAppWebInfo     = new
            {
                graftUrl = "https://www.youtube.com",
                webDisplayMode = "WEB_DISPLAY_MODE_BROWSER"
            }
        },
        user = new
        {
            enableSafetyMode = false,
            lockedSafetyMode = false
        },
        request = new
        {
            useSsl                  = true,
            internalExperimentFlags = Array.Empty<object>()
        }
    };

    private async Task<JsonDocument> PostAsync(string endpoint, object payload, CancellationToken ct)
    {
        var json    = JsonSerializer.Serialize(payload, SerializerOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var url     = $"{BaseUrl}{endpoint}?prettyPrint=false&alt=json";

        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("*"));
        request.Headers.Add("Origin",                    "https://www.youtube.com");
        request.Headers.Add("X-Youtube-Client-Name",     ClientNameId);
        request.Headers.Add("X-Youtube-Client-Version",  ClientVersion);
        request.Headers.UserAgent.ParseAdd(UserAgent);

        var response = await _http.SendAsync(request, ct);
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
