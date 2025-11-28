using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TwitchDropsBot.Core.Platform.Kick.Bot;
using TwitchDropsBot.Core.Platform.Kick.Device;
using TwitchDropsBot.Core.Platform.Kick.Models;
using TwitchDropsBot.Core.Platform.Kick.Models.ResponseType;
using TwitchDropsBot.Core.Platform.Shared.Exceptions;
using TwitchDropsBot.Core.Platform.Shared.Repository;
using TwitchDropsBot.Core.Platform.Shared.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TwitchDropsBot.Core.Platform.Shared.Bots;
using TwitchDropsBot.Core.Platform.Shared.Settings;

namespace TwitchDropsBot.Core.Platform.Kick.Repository;

public class KickHttpRepository : BotRepository<KickUser>
{
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private readonly IOptionsMonitor<BotSettings> _botSettings;
    public List<Category> NoClaimCategories;

    public KickHttpRepository(KickUser kickUser, ILogger logger, IOptionsMonitor<BotSettings> botSettings)
    {
        BotUser = kickUser;
        _logger = logger;
        _botSettings = botSettings;

        _httpClient = new HttpClient()
        {
            DefaultRequestVersion = HttpVersion.Version30,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact,
        };

        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            KickDeviceType.WEB.UserAgents.First()
        );
        
        NoClaimCategories = new List<Category>();
        
        _logger.LogTrace("KickHttpRepository initialized for user {Login}", kickUser.Login);
    }

    public async Task<List<Campaign>> GetDropsCampaignsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("Fetching drops campaigns");
        
        var result = await DoHTTPRequest<List<Campaign>>(
            HttpMethod.Get,
            "https://web.kick.com/api/v1/drops/campaigns",
            operationName: "GetDropsCampaigns",
            cancellationToken: cancellationToken
        );

        if (result?.data is null)
        {
            _logger.LogTrace("No campaigns data returned");
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

        _logger.LogTrace("Retrieved {Count} active campaigns", campaigns.Count);
        return campaigns;
    }

    public async Task ClaimDrop(Campaign campaign, Reward reward, CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("Attempting to claim drop for campaign {CampaignName}, reward {RewardName}", 
            campaign.Category.Name, reward.Name);
        
        var body = new
        {
            reward_id = reward.Id,
            campaign_id = campaign.Id
        };

        if (NoClaimCategories.Contains(campaign.Category))
        {
            _logger.LogTrace("Category {CategoryName} is in no-claim list, skipping", campaign.Category.Name);
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
            
            _logger.LogTrace("Successfully claimed drop for {CampaignName}", campaign.Category.Name);
        }
        catch (CantClaimException)
        {
            _logger.LogTrace("Cannot claim for category {CategoryName}, adding to no-claim list", 
                campaign.Category.Name);
            NoClaimCategories.Add(campaign.Category);
            throw;
        }
    }

    public async Task<ICollection<Livestream>> GetLivestreamCampaignsAsync(Campaign campaign, CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("Fetching livestreams for campaign {CampaignId}", campaign.Id);
        
        var url = $"https://web.kick.com/api/v1/drops/campaigns/{campaign.Id}/livestreams";

        var result = await DoHTTPRequest<List<Livestream>>(
            HttpMethod.Get,
            url,
            operationName: "GetLivestreamCampaigns",
            cancellationToken: cancellationToken
        );

        var livestreams = result?.data ?? new List<Livestream>();
        _logger.LogTrace("Found {Count} livestreams for campaign {CampaignId}", livestreams.Count, campaign.Id);
        
        return livestreams;
    }

    public async Task<List<Campaign>> GetInventory(CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("Fetching inventory");
        
        var result = await DoHTTPRequest<List<Campaign>>(
            HttpMethod.Get,
            "https://web.kick.com/api/v1/drops/progress",
            requiresAuth: true,
            operationName: "GetInventory",
            cancellationToken: cancellationToken
        );

        var inventory = result?.data ?? new List<Campaign>();
        _logger.LogTrace("Retrieved {Count} items in inventory", inventory.Count);
        
        return inventory;
    }

    public async Task<CampaignSummary?> GetSummary(Campaign campaign, CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("Fetching summary for campaign {CampaignId}", campaign.Id);
        
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
            _logger.LogTrace("No summary data returned for campaign {CampaignId}", campaign.Id);
            return null;
        }

        return result.data.Find(x => x.Id == campaign.Id);
    }

    public async Task<Channel?> GetChannelAsync(string slug, CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("Fetching channel info for {Slug}", slug);
        
        var url = $"https://kick.com/api/v2/channels/{slug}/info";

        var request = new HttpRequestMessage(HttpMethod.Get, url)
        {
            Version = HttpVersion.Version30,
            VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher
        };

        try
        {
            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var channel = await response.Content.ReadFromJsonAsync<Channel>(cancellationToken);
            _logger.LogTrace("Retrieved channel info for {Slug}", slug);
            
            return channel;
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Failed to get channel info for {Slug}", slug);
            throw;
        }
    }

    public async Task<List<Livestream>> FindStreams(Campaign campaign, CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("Finding streams for campaign {CampaignName}", campaign.Category.Name);
        
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

        var streams = result?.data?.livestreams ?? new List<Livestream>();
        _logger.LogTrace("Found {Count} streams for campaign {CampaignName}", 
            streams.Count, campaign.Category.Name);
        
        return streams;
    }

    public async Task<string> GetWssToken(CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("Fetching WSS token");
        
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
            _logger.LogTrace("Failed to retrieve WSS token - null response");
            throw new Exception("Failed to retrieve WSS token");
        }

        _logger.LogTrace("WSS token retrieved successfully");
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

        req.Headers.Add("Authorization", $"Bearer {token}");

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
                    _logger.LogTrace("Failed to deserialize response for {Operation}", operationName);
                    throw new Exception($"Failed to deserialize response for {operationName}");
                }

                if (_botSettings.CurrentValue.LogLevel > 0)
                {
                    _logger.LogDebug("Request successful: {Operation}", operationName);
                    _logger.LogTrace("{ResponseData}", 
                        JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = false }));
                }

                return result;
            }
            catch (HttpRequestException exception)
            {
                if (exception.StatusCode == HttpStatusCode.BadRequest)
                {
                    _logger.LogTrace("Cannot claim - BadRequest returned for {Operation}", operationName);
                    throw new CantClaimException("Can't claim");
                }
                
                if (i == requestLimit - 1)
                {
                    _logger.LogError(exception, 
                        "Failed to execute request {Operation} after {Attempts} attempts", 
                        operationName, requestLimit);
                    throw new Exception($"Failed to execute the request {operationName} after {requestLimit} attempts.", exception);
                }

                _logger.LogWarning(exception, 
                    "Failed to execute request {Operation} (attempt {Attempt}/{Total})", 
                    operationName, i + 1, requestLimit);

                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (Exception exception)
            {
                if (i == requestLimit - 1)
                {
                    _logger.LogError(exception, 
                        "Failed to execute request {Operation} after {Attempts} attempts", 
                        operationName, requestLimit);
                    throw new Exception($"Failed to execute the request {operationName} after {requestLimit} attempts.", exception);
                }

                _logger.LogWarning(exception, 
                    "Failed to execute request {Operation} (attempt {Attempt}/{Total})", 
                    operationName, i + 1, requestLimit);

                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }

        return null;
    }
}