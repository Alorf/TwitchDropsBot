using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Serilog;
using TwitchDropsBot.Core.Platform.Kick.Bot;
using TwitchDropsBot.Core.Platform.Kick.Device;
using TwitchDropsBot.Core.Platform.Kick.Models;
using TwitchDropsBot.Core.Platform.Kick.Models.ResponseType;
using TwitchDropsBot.Core.Platform.Shared.Exceptions;
using TwitchDropsBot.Core.Platform.Shared.Repository;
using TwitchDropsBot.Core.Platform.Shared.Services;
using TwitchDropsBot.Core.Platform.Twitch.Device;
using System.Net.Quic;

namespace TwitchDropsBot.Core.Platform.Kick.Repository;

public class KickHttpRepository : BotRepository<KickUser>
{
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    public List<Category> NoClaimCategories;

    public KickHttpRepository(KickUser kickUser, ILogger logger)
    {
        BotUser = kickUser;

        _logger = logger;

        _httpClient = new HttpClient()
        {
            DefaultRequestVersion = HttpVersion.Version30,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact,
        };

        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            KickDeviceType.WEB.UserAgents.First()
        );
        
        NoClaimCategories = new List<Category>();
    }

    public async Task<List<Campaign>> GetDropsCampaignsAsync(CancellationToken cancellationToken = default)
    {
        var result = await DoHTTPRequest<List<Campaign>>(
            HttpMethod.Get,
            "https://web.kick.com/api/v1/drops/campaigns",
            operationName: "GetDropsCampaigns",
            cancellationToken: cancellationToken
        );

        if (result?.data is null)
        {
            return new List<Campaign>();
        }

        var campaigns = result.data;
        campaigns.RemoveAll(x => x.Status == "expired" || x.Status == "upcoming");

        var favGamesSet = BotUser.FavouriteGames.Select(g => g.ToLower()).Distinct().ToHashSet();

        foreach (var campaign in campaigns)
        {
            if (favGamesSet.Contains(campaign.Category.Name.ToLower()))
            {
                campaign.Category.IsFavorite = true;
            }
        }

        return campaigns;
    }

    public async Task ClaimDrop(Campaign campaign, Reward reward, CancellationToken cancellationToken = default)
    {
        var body = new
        {
            reward_id = reward.Id,
            campaign_id = campaign.Id
        };

        if (NoClaimCategories.Contains(campaign.Category))
        {
            throw new CantClaimException();
        }

        try
        {
            await DoHTTPRequest<object>(
                HttpMethod.Post,
                "https://web.kick.com/api/v1/drops/claim",
                body: body,
                requiresAuth: true,
                operationName: "ClaimDrop",
                cancellationToken: cancellationToken
            );
        }
        catch (CantClaimException exception)
        {
            NoClaimCategories.Add(campaign.Category);
            throw new CantClaimException();
        }
        
    }

    public async Task<ICollection<Livestream>> GetLivestreamCampaignsAsync(Campaign campaign, CancellationToken cancellationToken = default)
    {
        var url = $"https://web.kick.com/api/v1/drops/campaigns/{campaign.Id}/livestreams";

        var result = await DoHTTPRequest<List<Livestream>>(
            HttpMethod.Get,
            url,
            operationName: "GetLivestreamCampaigns",
            cancellationToken: cancellationToken
        );

        return result?.data ?? new List<Livestream>();
    }

    public async Task<List<Campaign>> GetInventory(CancellationToken cancellationToken = default)
    {
        var result = await DoHTTPRequest<List<Campaign>>(
            HttpMethod.Get,
            "https://web.kick.com/api/v1/drops/progress",
            requiresAuth: true,
            operationName: "GetInventory",
            cancellationToken: cancellationToken
        );

        return result?.data ?? new List<Campaign>();
    }

    public async Task<CampaignSummary?> GetSummary(Campaign campaign, CancellationToken cancellationToken = default)
    {
        var url = $"https://web.kick.com/api/v1/drops/progress/summary?campaign_id={campaign.Id}";

        var result = await DoHTTPRequest<List<CampaignSummary>>(
            HttpMethod.Get,
            url,
            requiresAuth: true,
            operationName: "GetSummary",
            cancellationToken: cancellationToken
        );

        if (result?.data is null)
        {
            return null;
        }

        return result.data.Find(x => x.Id == campaign.Id);
    }

    public async Task<Channel?> GetChannelAsync(string slug, CancellationToken cancellationToken = default)
    {
        var url = $"https://kick.com/api/v2/channels/{slug}/info";

        // todo: Return channel not a ResponseType
        var request = new HttpRequestMessage(HttpMethod.Get, url)
        {
            Version = HttpVersion.Version30,
            VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher
        };

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<Channel>(cancellationToken);
    }

    public async Task<List<Livestream>> FindStreams(Campaign campaign, CancellationToken cancellationToken = default)
    {
        var url = $"https://mobile.kick.com/api/v1/livestreams";
        var categoryId = campaign.Category.Id;
        var limit = 24;
        var sort = "viewer_count_desc";

        var headers = new Dictionary<string, string>
        {
            { "User-Agent", "okhttp/4.7.2" }
        };

        var result = await DoHTTPRequest<LivestreamsResponse>(
            HttpMethod.Get,
            $"{url}?limit={limit}&category_id={categoryId}&sort={WebUtility.UrlEncode(sort)}",
            headers: headers,
            operationName: "FindStreams",
            cancellationToken: cancellationToken
        );

        return result?.data?.livestreams ?? new List<Livestream>();
    }

    public async Task<string> GetWssToken(CancellationToken cancellationToken = default)
    {
        var headers = new Dictionary<string, string>
        {
            { "User-Agent", KickDeviceType.MOBILE.UserAgents.First() },
            { "x-client-token", KickDeviceType.MOBILE.ClientToken }
        };

        var result = await DoHTTPRequest<WssTokenResponseType>(
            HttpMethod.Get,
            "https://websockets.kick.com/viewer/v1/token",
            headers: headers,
            requiresAuth: true,
            operationName: "GetWssToken",
            cancellationToken: cancellationToken
        );

        if (result?.data?.Token is null)
        {
            throw new Exception("Failed to retrieve WSS token");
        }

        return result.data.Token;
    }
    
    public static async Task<(string? id, string? username)> GetUserInfo(string token, CancellationToken ct = default)
    {
        
        var handler = new SocketsHttpHandler
        {
            CookieContainer = new CookieContainer(),
            AllowAutoRedirect = true
        };
        var _client = new HttpClient(handler) { BaseAddress = new Uri("https://kick.com") };
            
        _client.DefaultRequestVersion = HttpVersion.Version30;
        _client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;

        _client.DefaultRequestHeaders.UserAgent.ParseAdd("okhttp/4.7.2");
        
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

    private async Task<ResponseType<T>?> DoHTTPRequest<T>(
        HttpMethod method,
        string url,
        object? body = null,
        Dictionary<string, string>? headers = null,
        bool requiresAuth = false,
        string? operationName = null,
        CancellationToken cancellationToken = default)
    {
        const int requestLimit = 5;

        operationName ??= $"{method.Method} {url}";

        for (int i = 0; i < requestLimit; i++)
        {
            try
            {
                var request = new HttpRequestMessage(method, url)
                {
                    Version = HttpVersion.Version30,
                    VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher
                };

                request.Headers.Add("Accept", "application/json");

                if (headers is not null)
                {
                    foreach (var header in headers)
                    {
                        if (header.Key.Equals("User-Agent", StringComparison.OrdinalIgnoreCase))
                        {
                            request.Headers.UserAgent.Clear();
                            request.Headers.UserAgent.ParseAdd(header.Value);
                        }
                        else
                        {
                            request.Headers.Add(header.Key, header.Value);
                        }
                    }
                }

                if (requiresAuth)
                {
                    request.Headers.Add("Authorization", $"Bearer {BotUser.BearerToken}");
                }

                if (body != null &&
                    (method == HttpMethod.Post || method == HttpMethod.Put || method == HttpMethod.Patch))
                {
                    request.Content = JsonContent.Create(body);
                }

                var response = await _httpClient.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<ResponseType<T>>(cancellationToken);

                if (result == null)
                {
                    throw new Exception($"Failed to deserialize response for {operationName}");
                }

                if (AppSettingsService.Settings.LogLevel > 0)
                {
                    _logger.Debug($"Request successful: {operationName}");
                    _logger.Debug(JsonSerializer.Serialize(result,
                        new JsonSerializerOptions { WriteIndented = false }));
                }

                return result;
            }
            catch (HttpRequestException exception)
            {
                // Fail claim send 404, if fail once for a game maybe avoid claim for a specific game
                if (exception.StatusCode == HttpStatusCode.BadRequest)
                {
                    throw new CantClaimException("Can't claim");
                }
                
                if (i == requestLimit - 1)
                {
                    throw new Exception($"Failed to execute the request {operationName} after {requestLimit} attempts.", exception);
                }

                _logger.Error(exception, $"Failed to execute the request {operationName} (attempt {i + 1}/{requestLimit}).");

                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (Exception exception)
            {
                if (i == requestLimit - 1)
                {
                    throw new Exception($"Failed to execute the request {operationName} after {requestLimit} attempts.", exception);
                }

                _logger.Error(exception, $"Failed to execute the request {operationName} (attempt {i + 1}/{requestLimit}).");

                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }

        return null;
    }
}