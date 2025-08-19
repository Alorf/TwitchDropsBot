using System.Net;
using System.Net.Http.Headers;
using TwitchDropsBot.Core.Object;
using TwitchDropsBot.Core.Object.Config;

namespace TwitchDropsBot.Core.Utilities;

public class HttpRequest
{
    public HttpClient HttpClient { get; }
    private TwitchClient twitchClient;

    public HttpRequest(TwitchUser? twitchUser = null)
    {
        twitchClient = AppConfig.TwitchClient;
        HttpClient = new HttpClient();
        var userAgent = twitchClient.UserAgents[new Random().Next(twitchClient.UserAgents.Count)];
        if (twitchUser is not null)
        {
            HttpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("OAuth", twitchUser.ClientSecret);
            HttpClient.DefaultRequestHeaders.Add("Client-Id", twitchClient.ClientID);
            HttpClient.DefaultRequestHeaders.Add("Origin", twitchClient.URL);
            HttpClient.DefaultRequestHeaders.Add("Referer", twitchClient.URL);
            HttpClient.DefaultRequestHeaders.Add("User-Agent", userAgent);
        }
    }

    public async Task<HttpResponseMessage> GetAsync(string url, Dictionary<string, string>? headers = null)
    {
        var response = await this.HttpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return response;
    }
}