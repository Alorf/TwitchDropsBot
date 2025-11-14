using System.Net.Http.Headers;
using TwitchDropsBot.Core.Platform.Twitch.Bot;
using TwitchDropsBot.Core.Platform.Twitch.Device;
using Constant = TwitchDropsBot.Core.Platform.Twitch.Utils.Constant;

namespace TwitchDropsBot.Core.Platform.Twitch.Services;

public class TwitchHttpService
{
    public HttpClient HttpClient { get; }
    private TwitchDevice twitchDevice;

    public TwitchHttpService(TwitchUser? twitchUser = null)
    {
        twitchDevice = Constant.TwitchDevice;
        HttpClient = new HttpClient();
        var userAgent = twitchDevice.UserAgents[new Random().Next(twitchDevice.UserAgents.Count)];
        if (twitchUser is not null)
        {
            HttpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("OAuth", twitchUser.ClientSecret);
            HttpClient.DefaultRequestHeaders.Add("Client-Id", twitchDevice.ClientID);
            HttpClient.DefaultRequestHeaders.Add("Origin", twitchDevice.URL);
            HttpClient.DefaultRequestHeaders.Add("Referer", twitchDevice.URL);
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