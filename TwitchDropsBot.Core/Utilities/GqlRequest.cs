using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
using System.Net.Http.Headers;
using System.Text.Json;
using TwitchDropsBot.Core.Object;
using TwitchDropsBot.Core.Object.Config;
using TwitchDropsBot.Core.Object.TwitchGQL;
using Game = TwitchDropsBot.Core.Object.TwitchGQL.Game;

namespace TwitchDropsBot.Core.Utilities;

public class GqlRequest
{
    private TwitchClient twitchClient = AppConfig.TwitchClient;
    private GraphQLHttpClient graphQLClient;
    private TwitchUser twitchUser;
    private string clientSessionId;
    private string userAgent;
    private AppConfig config;
    private JsonElement postmanCollection;
    private static readonly object _postmanLock = new object();


    public GqlRequest(TwitchUser twitchUser)
    {
        config = AppConfig.Instance;
        this.twitchUser = twitchUser;
        clientSessionId = GenerateClientSessionId("0123456789abcdef", 16);
        userAgent = twitchClient.UserAgents[new Random().Next(twitchClient.UserAgents.Count)];

        HttpClient httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("OAuth", twitchUser.ClientSecret);
        httpClient.DefaultRequestHeaders.Add("Client-Id", this.twitchClient.ClientID);
        httpClient.DefaultRequestHeaders.Add("Origin", twitchClient.URL);
        httpClient.DefaultRequestHeaders.Add("Referer", twitchClient.URL);
        httpClient.DefaultRequestHeaders.Add("User-Agent", userAgent);

        graphQLClient =
            new GraphQLHttpClient("https://gql.twitch.tv/gql", new SystemTextJsonSerializer(), httpClient);

        lock (_postmanLock)
        {
            var jsonString = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Postman/TwitchSave.postman_collection.json"));
            postmanCollection = JsonDocument.Parse(jsonString).RootElement;
        }
    }

    public async Task<List<AbstractCampaign>> FetchDropsAsync()
    {
        var query = CreateQuery("ViewerDropsDashboard");

        dynamic? resp = await DoGQLRequestAsync(query);

        if (resp != null)
        {
            List<AbstractCampaign> campaigns = new List<AbstractCampaign>();
            List<DropCampaign> dropCampaigns = resp.Data.CurrentUser.DropCampaigns ?? new List<DropCampaign>();
            List<RewardCampaignsAvailableToUser> rewardCampaigns = resp.Data.RewardCampaignsAvailableToUser;

            dropCampaigns = dropCampaigns.FindAll(dropCampaign => dropCampaign is { Status: "ACTIVE" });
            rewardCampaigns.RemoveAll(campaign => campaign.Game == null);


            //Select only campaigns with minute watched goal
            rewardCampaigns = rewardCampaigns.FindAll(rewardCampaign => rewardCampaign.UnlockRequirements?.MinuteWatchedGoal != 0);

            var favGamesSet = new HashSet<string>(twitchUser.FavouriteGames.Select(game => game.ToLower()));

            foreach (var dropCampaign in dropCampaigns)
            {
                if (favGamesSet.Contains(dropCampaign.Game.DisplayName.ToLower()))
                {
                    dropCampaign.Game.IsFavorite = true;
                }
            }

            foreach (var rewardCampaign in rewardCampaigns)
            {
                if (favGamesSet.Contains(rewardCampaign.Game.DisplayName.ToLower()))
                {
                    rewardCampaign.Game.IsFavorite = true;
                }
            }

            campaigns.AddRange(dropCampaigns);
            campaigns.AddRange(rewardCampaigns);

            return campaigns;
        }

        return new List<AbstractCampaign>();
    }

    public async Task<DropCampaign?> FetchTimeBasedDropsAsync(string dropID)
    {

        var query = CreateQuery("DropCampaignDetails");

        if (query.Variables is Dictionary<string, object?> variables)
        {
            variables["channelLogin"] = twitchUser.Id;
            variables["dropID"] = dropID;

        }

        dynamic? resp = await DoGQLRequestAsync(query);

        if (resp != null && resp.Data.User.DropCampaign != null)
        {
            DropCampaign dropCampaign = resp.Data.User.DropCampaign;

            if (dropCampaign.Id != dropID)
            {
                twitchUser.Logger.Error("The drop ID does not match the drop campaign ID.");
            }

            dropCampaign.TimeBasedDrops.RemoveAll(drop => drop.RequiredSubs != 0);

            return dropCampaign;
        }

        return null;
    }

    public async Task<Inventory?> FetchInventoryDropsAsync()
    {
        var query = CreateQuery("Inventory");

        dynamic? resp = await DoGQLRequestAsync(query);

        Inventory? inventory = resp?.Data.CurrentUser.Inventory;

        if (inventory?.DropCampaignsInProgress is null)
        {
            inventory.DropCampaignsInProgress = new List<DropCampaign>();
        }

        foreach (var dropCampaign in inventory.DropCampaignsInProgress)
        {
            dropCampaign.TimeBasedDrops = dropCampaign.TimeBasedDrops.OrderBy(drop => drop.RequiredMinutesWatched).ToList();
        }

        return inventory;
    }

    public async Task<Notifications?> FetchNotificationsAsync(int? limit = 10)
    {
        var query = CreateQuery("OnsiteNotifications_ListNotifications");

        if (query.Variables is Dictionary<string, object?> variables)
        {
            variables["limit"] = limit;
        }

        dynamic? resp = await DoGQLRequestAsync(query);

        var notifications = resp?.Data.CurrentUser.Notifications;

        return notifications;
    }

    public async Task<Game?> FetchDirectoryPageGameAsync(string slug, bool mustHaveDrops)
    {
        var query = CreateQuery("DirectoryPage_Game");

        if (query.Variables is Dictionary<string, object?> variables)
        {
            variables["slug"] = slug;

            //if must have drops, add {"DROPS_ENABLED"} to "systemFilters": []
            if (mustHaveDrops)
            {
                if (variables["options"] is Dictionary<string, object?> options)
                {
                    if (options.TryGetValue("systemFilters", out var systemFiltersObj) && systemFiltersObj is List<object> systemFilters)
                    {
                        options["systemFilters"] = new List<object> { "DROPS_ENABLED" };
                    }
                }
            }
        }

        dynamic? resp = await DoGQLRequestAsync(query);

        return resp?.Data.Game;
    }

    public async Task<StreamPlaybackAccessToken?> FetchPlaybackAccessTokenAsync(string login)
    {
        var query = CreateQuery("PlaybackAccessToken");

        if (query.Variables is Dictionary<string, object?> variables)
        {
            variables["login"] = login;
        }

        dynamic? resp = await DoGQLRequestAsync(query);

        return resp?.Data.StreamPlaybackAccessToken;
    }

    public async Task<User?> FetchStreamInformationAsync(string channel)
    {
        var query = CreateQuery("VideoPlayerStreamInfoOverlayChannel");

        if (query.Variables is Dictionary<string, object?> variables)
        {
            variables["channel"] = channel;
        }

        dynamic? resp = await DoGQLRequestAsync(query);

        return resp?.Data.User;
    }

    public async Task<DropCurrentSession?> FetchCurrentSessionContextAsync(AbstractBroadcaster channel)
    {
        var query = CreateQuery("DropCurrentSessionContext");

        if (query.Variables is Dictionary<string, object?> variables)
        {
            variables["channelLogin"] = channel.Login;
        }

        dynamic? resp = await DoGQLRequestAsync(query);

        return resp?.Data.CurrentUser.DropCurrentSession;
    }

    // The channel must be live to get the drops
    public async Task<List<DropCampaign>> FetchAvailableDropsAsync(string? channel)
    {
        if (channel == null)
        {
            return new List<DropCampaign>();
        }

        var query = CreateQuery("DropsHighlightService_AvailableDrops");

        if (query.Variables is Dictionary<string, object?> variables)
        {
            variables["channelID"] = channel;
        }

        dynamic? resp = await DoGQLRequestAsync(query);

        if (resp != null && resp.Data.Channel.ViewerDropCampaigns != null)
        {
            List<DropCampaign> campaigns = resp.Data.Channel.ViewerDropCampaigns;

            campaigns.ForEach(campaign => campaign.TimeBasedDrops.RemoveAll(drop => drop.RequiredMinutesWatched == 0));
            campaigns.RemoveAll(campaign => campaign.TimeBasedDrops.Count == 0);

            return campaigns;
        }

        return new List<DropCampaign?>();
    }

    public async Task<bool> ClaimDropAsync(string dropInstanceID)
    {

        var query = CreateQuery("DropsPage_ClaimDropRewards");

        query.Variables = new
        {
            input = new
            {
                dropInstanceID
            }
        };

        var customHeaders = new HttpClient();

        customHeaders.DefaultRequestHeaders.Add("Authorization", "OAuth " + twitchUser.ClientSecret);
        customHeaders.DefaultRequestHeaders.Add("client-id", twitchClient.ClientID);
        customHeaders.DefaultRequestHeaders.Add("client-session-id", clientSessionId);
        customHeaders.DefaultRequestHeaders.Add("x-device-id", twitchUser.UniqueId);
        customHeaders.DefaultRequestHeaders.Add("Origin", twitchClient.URL);
        customHeaders.DefaultRequestHeaders.Add("Referer", twitchClient.URL);
        customHeaders.DefaultRequestHeaders.Add("User-Agent", userAgent);

        var redeemGraphQLClient =
            new GraphQLHttpClient("https://gql.twitch.tv/gql", new SystemTextJsonSerializer(), customHeaders);

        dynamic? resp = await DoGQLRequestAsync(query, redeemGraphQLClient);

        return resp != null;
    }

    private async Task<dynamic?> DoGQLRequestAsync(GraphQLRequest query, GraphQLHttpClient? client = null, string? name = null)
    {
        var avoidPrint = new List<string> { "Inventory", "ViewerDropsDashboard", "DirectoryPage_Game" };

        int limit = 5;
        client ??= graphQLClient;
        name ??= query.OperationName;

        for (int i = 0; i < limit; i++)
        {
            try
            {
                var graphQLResponse = await client.SendQueryAsync<ResponseType>(query);

                if (graphQLResponse.Errors != null)
                {
                    twitchUser.Logger.Info($"Failed to execute the query {name}");
                    foreach (var error in graphQLResponse.Errors)
                    {
                        twitchUser.Logger.Info(error.Message);
                    }

                    throw new System.Exception();
                }

                if (!avoidPrint.Contains(name))
                {
                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = false
                    };
                    var json = JsonSerializer.Serialize(graphQLResponse.Data, options);
                    if (config.LogLevel > 0)
                    {
                        twitchUser.Logger.Log(name, "REQ", ConsoleColor.Blue);
                        twitchUser.Logger.Log(json, "REQ", ConsoleColor.Blue);
                    }
                }


                return graphQLResponse;

            }
            catch (System.Exception e)
            {
                if (i == 4)
                {
                    throw new System.Exception($"Failed to execute the query {name} (attempt {i + 1}/{limit}).");
                }

                twitchUser.Logger.Error($"Failed to execute the query {name} (attempt {i + 1}/{limit}).");
                SystemLogger.Error($"[{twitchUser.Login}] Failed to execute the query {name} (attempt {i + 1}/{limit}).");

                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }

        return null;
    }

    private string GenerateClientSessionId(string chars, int length)
    {
        var random = new Random();
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }

    public GraphQLRequest CreateQuery(string operationName)
    {
        var item = postmanCollection.GetProperty("item").EnumerateArray().First(i => i.GetProperty("name").GetString() == $"{operationName}");

        var rawBody = item.GetProperty("request").GetProperty("body").GetProperty("raw");

        var jsonBody = JsonDocument.Parse(rawBody.GetString()).RootElement;

        // Check if the properties exist before accessing them
        Dictionary<string, object?>? variables = null;
        Dictionary<string, object?>? extensions = null;

        if (jsonBody.TryGetProperty("variables", out JsonElement variablesElement))
        {
            variables = DeserializeJsonElement(variablesElement);
        }

        if (jsonBody.TryGetProperty("extensions", out JsonElement extensionsElement))
        {
            extensions = DeserializeJsonElement(extensionsElement);
        }

        return new GraphQLRequest
        {
            OperationName = jsonBody.GetProperty("operationName").GetString(),
            Variables = variables,
            Extensions = extensions
        };
    }

    private Dictionary<string, object?> DeserializeJsonElement(JsonElement element)
    {
        var dictionary = new Dictionary<string, object?>();

        foreach (var property in element.EnumerateObject())
        {
            dictionary[property.Name] = ConvertJsonElement(property.Value);
        }

        return dictionary;
    }

    private object? ConvertJsonElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                return DeserializeJsonElement(element);
            case JsonValueKind.Array:
                var list = new List<object?>();
                foreach (var item in element.EnumerateArray())
                {
                    list.Add(ConvertJsonElement(item));
                }
                return list;
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.Number:
                if (element.TryGetInt32(out int intValue))
                {
                    return intValue;
                }
                if (element.TryGetInt64(out long longValue))
                {
                    return longValue;
                }
                if (element.TryGetDouble(out double doubleValue))
                {
                    return doubleValue;
                }
                break;
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.Null:
                return null;
        }

        return element.GetRawText();
    }
}