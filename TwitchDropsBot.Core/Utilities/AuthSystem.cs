using System.Net;
using System.Text.Json;
using TwitchDropsBot.Core.Object.Config;

namespace TwitchDropsBot.Core.Utilities;

public class AuthSystem
{
    public static async Task<JsonDocument> GetCodeAsync()
    {
        var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "https://id.twitch.tv/oauth2/device");
        var content = new MultipartFormDataContent();

        content.Add(new StringContent(AppConfig.TwitchClient.ClientID), "client_id");
        content.Add(
            new StringContent("channel_read chat:read user_blocks_edit user_blocks_read user_follows_edit user_read"),
            "scopes");
        request.Content = content;

        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync();

        return JsonDocument.Parse(responseContent);
    }
    
    public static async Task<JsonDocument?> CodeConfirmationAsync(string deviceCode, CancellationToken? token = null)
    {
        for (int i = 0; i < 10; i++)
        {
            if (token != null && token.Value.IsCancellationRequested)
            {
                throw new OperationCanceledException();
            }
            await Task.Delay(TimeSpan.FromSeconds(5));

            var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Post, "https://id.twitch.tv/oauth2/token");
            var content = new MultipartFormDataContent();

            content.Add(new StringContent(AppConfig.TwitchClient.ClientID), "client_id");
            content.Add(new StringContent(deviceCode), "device_code");
            content.Add(new StringContent("urn:ietf:params:oauth:grant-type:device_code"), "grant_type");
            request.Content = content;

            var response = await client.SendAsync(request);

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                SystemLogger.Info("Waiting for user to authenticate...");
                continue;
            }

            var responseContent = await response.Content.ReadAsStringAsync();

            return JsonDocument.Parse(responseContent);
        }

        return null;
    }

    public static async Task<ConfigUser> ClientSecretUserAsync(string secret)
    {
        var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "https://id.twitch.tv/oauth2/validate");
        request.Headers.Add("Authorization", "OAuth " + secret);
        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync();
        var jsonResponse = JsonDocument.Parse(responseContent);

        var configUser = new ConfigUser();
        configUser.ClientSecret = secret;
        configUser.Id = jsonResponse.RootElement.GetProperty("user_id").GetString();
        configUser.Login = jsonResponse.RootElement.GetProperty("login").GetString();


        // Do request to TwitchClient URL to get the unique id
        request = new HttpRequestMessage(HttpMethod.Get, "https://m.twitch.tv");
        request.Headers.Add("Accept", "*/*");

        response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();

        // Get header set cookie and extract the unique id
        var setCookie = response.Headers.GetValues("Set-Cookie");

        foreach (var cookie in setCookie)
        {
            if (cookie.Contains("unique_id="))
            {
                // Extract the unique id
                configUser.UniqueId = cookie.Split(";").First().Split("=").Last();
                break;
            }
        }

        configUser.FavouriteGames = new List<string>();
        configUser.Enabled = true;

        return configUser;
    }
}