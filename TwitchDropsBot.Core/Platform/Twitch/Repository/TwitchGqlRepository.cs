using System.Text.Json;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
using Serilog;
using TwitchDropsBot.Core.Platform.Shared.Bots;
using TwitchDropsBot.Core.Platform.Shared.Repository;
using TwitchDropsBot.Core.Platform.Shared.Services;
using TwitchDropsBot.Core.Platform.Shared.Settings;
using TwitchDropsBot.Core.Platform.Twitch.Bot;
using TwitchDropsBot.Core.Platform.Twitch.Device;
using TwitchDropsBot.Core.Platform.Twitch.Models;
using TwitchDropsBot.Core.Platform.Twitch.Models.Abstractions;
using TwitchDropsBot.Core.Platform.Twitch.Services;
using Constant = TwitchDropsBot.Core.Platform.Twitch.Utils.Constant;

namespace TwitchDropsBot.Core.Platform.Twitch.Repository;

public class TwitchGqlRepository : BotRepository<TwitchUser>
{
    private TwitchDevice twitchDevice = Constant.TwitchDevice;
    private GraphQLHttpClient graphQLClient;
    private string clientSessionId;
    private BotSettings config;
    private JsonElement postmanCollection;
    private static readonly object _postmanLock = new object();
    private TwitchHttpService twitchHttpServiceGql;
    private readonly int requestLimit = 5;
    private ILogger? _logger;

    public TwitchGqlRepository(TwitchUser twitchUser, ILogger logger)
    {
        BotUser = twitchUser;

        clientSessionId = GenerateClientSessionId("0123456789abcdef", 16);

        config = AppSettingsService.Settings;
        twitchHttpServiceGql = new TwitchHttpService(twitchUser);

        _logger = logger; 

        graphQLClient =
            new GraphQLHttpClient("https://gql.twitch.tv/gql", new SystemTextJsonSerializer(),
                twitchHttpServiceGql.HttpClient);

        lock (_postmanLock)
        {
            var jsonString =
                File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Postman/TwitchSave.postman_collection.json"));
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
            List<RewardCampaign> rewardCampaigns = resp.Data.RewardCampaignsAvailableToUser;

            dropCampaigns = dropCampaigns.FindAll(dropCampaign => dropCampaign is { Status: "ACTIVE" });
            rewardCampaigns.RemoveAll(campaign => campaign.Game == null);

            //Select only campaigns with minute watched goal
            rewardCampaigns =
                rewardCampaigns.FindAll(rewardCampaign => rewardCampaign.UnlockRequirements?.MinuteWatchedGoal != 0);

            var favGame = (from game in BotUser.FavouriteGames select game.ToLower()).Distinct();
            var favGamesSet = favGame;

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

            rewardCampaigns = rewardCampaigns.OrderBy(x => x.UnlockRequirements.MinuteWatchedGoal).ToList();
            
            campaigns.AddRange(rewardCampaigns);

            return campaigns;
        }

        return new List<AbstractCampaign>();
    }

    public async Task<DropCampaign?> FetchTimeBasedDropsAsync(string dropId)
    {
        var query = CreateQuery("DropCampaignDetails");

        if (query.Variables is Dictionary<string, object?> variables)
        {
            variables["channelLogin"] = BotUser.Id;
            variables["dropID"] = dropId;
        }

        dynamic? resp = await DoGQLRequestAsync(query);

        if (resp != null && resp.Data.User.DropCampaign != null)
        {
            DropCampaign dropCampaign = resp.Data.User.DropCampaign;

            if (dropCampaign.Id != dropId)
            {
                _logger.Error("The drop ID does not match the drop campaign ID.");
            }

            dropCampaign.TimeBasedDrops.RemoveAll(drop => drop.RequiredSubs != 0);

            return dropCampaign;
        }

        return null;
    }

    public async Task<Inventory?> FetchInventoryDropsAsync()
    {
        var query = CreateQuery("Inventory");
        if (query.Variables is Dictionary<string, object?> variables)
        {
            variables["fetchRewardCampaigns"] = true;
        }

        dynamic? resp = await DoGQLRequestAsync(query);

        Inventory? inventory = resp?.Data.CurrentUser.Inventory;

        if (inventory?.DropCampaignsInProgress is null)
        {
            inventory.DropCampaignsInProgress = new List<DropCampaign>();
        }

        foreach (var dropCampaign in inventory.DropCampaignsInProgress)
        {
            dropCampaign.TimeBasedDrops =
                dropCampaign.TimeBasedDrops.OrderBy(drop => drop.RequiredMinutesWatched).ToList();
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

            // if must have drops, add {"DROPS_ENABLED"} to "systemFilters": []
            if (mustHaveDrops)
            {
                if (variables["options"] is Dictionary<string, object?> options)
                {
                    if (options.TryGetValue("systemFilters", out var systemFiltersObj) &&
                        systemFiltersObj is List<object> systemFilters)
                    {
                        options["systemFilters"] = new List<object> { "DROPS_ENABLED" };
                    }
                }
            }
        }

        dynamic? resp = await DoGQLRequestAsync(query);

        return resp?.Data.Game;
    }

    public async Task<PlaybackAccessToken?> FetchPlaybackAccessTokenAsync(string login)
    {
        var query = CreateQuery("PlaybackAccessToken");

        if (query.Variables is Dictionary<string, object?> variables)
        {
            variables["login"] = login;
        }

        dynamic? resp = await DoGQLRequestAsync(query);

        return resp?.Data.StreamPlaybackAccessToken;
    }

    public async Task<List<User>?> FetchStreamInformationAsync(string[] channels)
    {
        var queries = new List<GraphQLRequest>();

        foreach (var channel in channels)
        {
            var query = CreateQuery("VideoPlayerStreamInfoOverlayChannel");

            if (query.Variables is Dictionary<string, object?> variables)
            {
                variables["channel"] = channel;
            }

            queries.Add(query);
        }

        dynamic? resp = await DoGQLRequestAsync(queries);

        List<User> users = new List<User>();
        foreach (var item in resp.EnumerateArray())
        {
            if (item.GetProperty("data").GetProperty("user").ValueKind != JsonValueKind.Null)
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var user = JsonSerializer.Deserialize<User>(item.GetProperty("data").GetProperty("user").GetRawText(), options);
                if (user != null)
                {
                    users.Add(user);
                }
            }
        }

        return users;
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

    /// <summary>
    /// Fetch information about drops from a broadcaster's current session.
    /// usefull data available : CurrentMinutesWatched, requiredMinutesWatched
    /// </summary>
    /// <param name="channel">The broadcaster information to retrieve the session.</param>
    /// <returns>
    /// Return a <see cref="DropCurrentSession"/> from a broadcaster, or <c>null</c> if no data could be retrieved.
    /// </returns>
    public async Task<DropCurrentSession?> FetchCurrentSessionContextAsync(User channel)
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

        var customHttpClient = new HttpClient();
        foreach (var header in twitchHttpServiceGql.HttpClient.DefaultRequestHeaders)
        {
            customHttpClient.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
        }

        customHttpClient.DefaultRequestHeaders.Add("client-session-id", clientSessionId);
        customHttpClient.DefaultRequestHeaders.Add("x-device-id", BotUser.UniqueId);

        var redeemGraphQLClient =
            new GraphQLHttpClient("https://gql.twitch.tv/gql", new SystemTextJsonSerializer(), customHttpClient);

        dynamic? resp = await DoGQLRequestAsync(query, redeemGraphQLClient);

        return resp != null;
    }

    public async Task<RewardCampaignCode> RewardCodeModal(string campaignId, string rewardId)
    {
        var query = CreateQuery("RewardCodeModal");
        if (query.Variables is Dictionary<string, object?> variables)
        {
            variables["rewardCampaignID"] = campaignId;
            variables["rewardID"] = rewardId;
        }
        
        var redeemGraphQLClient =
            new GraphQLHttpClient("https://gql.twitch.tv/gql", new SystemTextJsonSerializer());
        
        dynamic? resp = await DoGQLRequestAsync(query);

        RewardCampaignCode rewardCampaignCode = resp.Data.CurrentUser.Inventory.RewardValue;

        return rewardCampaignCode;
    }

    private async Task<dynamic?> DoGQLRequestAsync(GraphQLRequest query, GraphQLHttpClient? client = null,
        string? name = null)
    {
        var avoidPrint = new List<string> { "Inventory", "ViewerDropsDashboard", "DirectoryPage_Game" };

        client ??= graphQLClient;
        name ??= query.OperationName;

        for (int i = 0; i < requestLimit; i++)
        {
            try
            {
                var graphQLResponse = await client.SendQueryAsync<Data>(query);

                if (graphQLResponse.Errors != null)
                {
                    _logger.Debug($"Failed to execute the query {name}");
                    foreach (var error in graphQLResponse.Errors)
                    {
                        _logger.Debug(error.Message);
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
                        _logger.Debug(name, "REQ", ConsoleColor.Blue);
                        _logger.Debug(json, "REQ", ConsoleColor.Blue);
                    }
                }


                return graphQLResponse;
            }
            catch (System.Exception e)
            {
                if (i == 4)
                {
                    throw new System.Exception($"Failed to execute the query {name} (attempt {i + 1}/{requestLimit}).");
                }
                
                _logger.Error(e, $"Failed to execute the query {name} (attempt {i + 1}/{requestLimit}).");
                
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }

        return null;
    }

    private async Task<dynamic?> DoGQLRequestAsync(List<GraphQLRequest> queries, GraphQLHttpClient? client = null,
        string? name = null)
    {
        client ??= graphQLClient;
        name ??= queries[0].OperationName;

        var body = from q in queries
            select new
            {
                operationName = q.OperationName,
                variables = q.Variables,
                extensions = q.Extensions
            };

        var jsonContent = JsonSerializer.Serialize(body);

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://gql.twitch.tv/gql")
        {
            Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json")
        };

        for (int i = 0; i < requestLimit; i++)
        {
            try
            {
                var httpResponse = await client.HttpClient.SendAsync(httpRequest);
                httpResponse.EnsureSuccessStatusCode();

                var responseContent = await httpResponse.Content.ReadAsStringAsync();
                var responseArray = JsonSerializer.Deserialize<JsonElement>(responseContent);

                if (config.LogLevel > 0)
                {
                    _logger.Debug(name, "REQ", ConsoleColor.Blue);
                    _logger.Debug(responseContent, "REQ", ConsoleColor.Blue);
                }

                return responseArray;
            }
            catch (System.Exception e)
            {
                if (i == 4)
                {
                    throw new System.Exception($"Failed to execute the query {name} (attempt {i + 1}/{requestLimit}).");
                }

                _logger.Error($"Failed to execute the query {name} (attempt {i + 1}/{requestLimit}).");
                SystemLoggerService.Logger.Error(
                    $"[{BotUser.Login}] Failed to execute the query {name} (attempt {i + 1}/{requestLimit}).");

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
        var item = postmanCollection.GetProperty("item").EnumerateArray()
            .First(i => i.GetProperty("name").GetString() == $"{operationName}");

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