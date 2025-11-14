using System.Net;
using System.Net.Http.Json;
using TwitchDropsBot.Core.Platform.Kick.Bot;
using TwitchDropsBot.Core.Platform.Kick.Device;
using TwitchDropsBot.Core.Platform.Kick.Models;
using TwitchDropsBot.Core.Platform.Kick.Models.ResponseType;
using TwitchDropsBot.Core.Platform.Shared.Repository;
using TwitchDropsBot.Core.Platform.Twitch.Device;

namespace TwitchDropsBot.Core.Platform.Kick.Repository;

public class KickHttpRepository : BotRepository<KickUser>
{
    private readonly HttpClient _httpClient;
    
    public KickHttpRepository(KickUser kickUser)
    {
        BotUser = kickUser;

        _httpClient = new HttpClient()
        {
            DefaultRequestVersion = HttpVersion.Version30,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact,
        };

        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            KickDeviceType.WEB.UserAgents.First()
        );
    }

    public async Task<List<Campaign>> GetDropsCampaignsAsync(CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://web.kick.com/api/v1/drops/campaigns")
        {
            Version = HttpVersion.Version30,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact
        };
    
        request.Headers.Add("Accept", "application/json");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<ResponseType<List<Campaign>>>(cancellationToken);

        if (result is null)
        {
            return new List<Campaign>();
        }

        var campaigns = result.data;

        campaigns.RemoveAll(x => x.Status == "expired" || x.Status == "upcoming");

        var favGame = (from game in BotUser.FavouriteGames select game.ToLower()).Distinct();
        var favGamesSet = favGame;
        
        foreach (var campaign in campaigns)
        {
            if (favGamesSet.Contains(campaign.Category.Name.ToLower()))
            {
                campaign.Category.IsFavorite = true;
            }
        }

        return campaigns;
    }

    public async Task ClaimDrop(Campaign campaign, Reward reward)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://web.kick.com/api/v1/drops/claim")
        {
            Version = HttpVersion.Version30,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact
        };
        
        request.Headers.Add("Authorization", $"Bearer {BotUser.BearerToken}");
        
        var body = new
        {
            reward_id = reward.Id,
            campaign_id = campaign.Id
        };
        
        request.Content = JsonContent.Create(body);
        
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    public async Task<ICollection<Livestream>> GetLivestreamCampaignsAsync(Campaign campaign)
    {
        var url = $"https://web.kick.com/api/v1/drops/campaigns/{campaign.Id}/livestreams";
        
        var request = new HttpRequestMessage(HttpMethod.Get, url)
        {
            Version = HttpVersion.Version30,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact
        };
        
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<ResponseType<List<Livestream>>>();

        if (result is null)
        {
            return new List<Livestream>();
        }
        
        return result.data;
    }

    public async Task<List<Campaign>> GetInventory()
    {
        var url = $"https://web.kick.com/api/v1/drops/progress";
        
        var request = new HttpRequestMessage(HttpMethod.Get, url)
        {
            Version = HttpVersion.Version30,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact
        };
        
        request.Headers.Add("Authorization", $"Bearer {BotUser.BearerToken}");
        
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<ResponseType<List<Campaign>>>();

        if (result is null)
        {
            return new List<Campaign>();
        }
        
        return result.data;
    }

    public async Task<CampaignSummary?> GetSummary(Campaign campaign)
    {
        var url = $"https://web.kick.com/api/v1/drops/progress/summary?campaign_id={campaign.Id}";
        
        var request = new HttpRequestMessage(HttpMethod.Get, url)
        {
            Version = HttpVersion.Version30,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact
        };
        
        request.Headers.Add("Authorization", $"Bearer {BotUser.BearerToken}");
        
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<ResponseType<List<CampaignSummary>>>();
        if (result is null)
        {
            return null;
        }
        
        List<CampaignSummary> summaries = result.data;

        var summary = summaries.Find(x => x.Id == campaign.Id);
        
        return summary;
    }

    public async Task<Channel?> GetChannelAsync(string slug)
    {
        var url = $"https://kick.com/api/v2/channels/{slug}/info";
        
        var request = new HttpRequestMessage(HttpMethod.Get, url)
        {
            Version = HttpVersion.Version30,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact
        };
        
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<Channel>();

        if (result is null)
        {
            return null;
        }
        
        return result;
    }

    public async Task<List<Livestream>> FindStreams(Campaign campaign)
    {
        //https://mobile.kick.com/api/v1/livestreams?limit=24&category_id=13&sort=viewer_count_desc
        var url = $"https://mobile.kick.com/api/v1/livestreams";
        var categoryId = campaign.Category.Id;
        var limit = 24;
        var sort = "viewer_count_desc";
        
        var request = new HttpRequestMessage(HttpMethod.Get, $"{url}?limit={limit}&category_id={categoryId}&sort={WebUtility.UrlEncode(sort)}")
        {
            Version = HttpVersion.Version30,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact
        };

        request.Headers.UserAgent.ParseAdd("okhttp/4.7.2");
        
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<ResponseType<LivestreamsResponse>>();
        var livestreams = result.data.livestreams;
        return livestreams;
    }

    public async Task<string> GetWssToken()
    {
        var url = "https://websockets.kick.com/viewer/v1/token";

        var request = new HttpRequestMessage(HttpMethod.Get, url)
        {
            Version = HttpVersion.Version30,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact
        };
        
        //Set custom header
        request.Headers.UserAgent.ParseAdd(KickDeviceType.MOBILE.UserAgents.First());
        request.Headers.Add("x-client-token", KickDeviceType.MOBILE.ClientToken);
        request.Headers.Add("Authorization", $"Bearer {BotUser.BearerToken}");
        
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ResponseType<WssTokenResponseType>>();
        var token = result.data.Token;

        return token;
    }
}