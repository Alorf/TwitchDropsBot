using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
using System.Net.Http.Headers;
using System.Text.Json;
using TwitchDropsBot.Core.Object;
using TwitchDropsBot.Core.Object.Config;
using TwitchDropsBot.Core.Object.TwitchGQL;

namespace TwitchDropsBot.Core.Utilities;

public class GqlRequest
{
    private TwitchClient twitchClient = AppConfig.TwitchClient;
    private GraphQLHttpClient graphQLClient;
    private TwitchUser twitchUser;
    private string clientSessionId;
    private string userAgent;

    public GqlRequest(TwitchUser twitchUser)
    {
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
    }

    public async Task<List<AbstractCampaign>> FetchDropsAsync()
    {
        var query = new GraphQLRequest
        {
            OperationName = "ViewerDropsDashboard",
            Variables = new
            {
                fetchRewardCampaigns = true,
            },
            Extensions = new Dictionary<string, object?>
            {
                ["persistedQuery"] = new Dictionary<string, object>
                {
                    ["sha256Hash"] = "5a4da2ab3d5b47c9f9ce864e727b2cb346af1e3ea8b897fe8f704a97ff017619",
                    ["version"] = 1
                }
            }
        };

        dynamic? resp = await DoGQLRequestAsync(query);

        if (resp != null)
        {
            List<AbstractCampaign> campaigns = new List<AbstractCampaign>();
            List<DropCampaign> dropCampaigns = resp.Data.CurrentUser.DropCampaigns;
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
        var query = new GraphQLRequest
        {
            OperationName = "DropCampaignDetails",
            Variables = new
            {
                dropID,
                channelLogin = twitchUser.Id,
            },
            Extensions = new Dictionary<string, object?>
            {
                ["persistedQuery"] = new Dictionary<string, object>
                {
                    ["sha256Hash"] = "039277bf98f3130929262cc7c6efd9c141ca3749cb6dca442fc8ead9a53f77c1",
                    ["version"] = 1
                }
            }
        };

        dynamic? resp = await DoGQLRequestAsync(query);

        if (resp != null)
        {
            return resp.Data.User.DropCampaign;
        }

        return null;
    }

    public async Task<Inventory?> FetchInventoryDropsAsync()
    {
        var query = new GraphQLRequest
        {
            OperationName = "Inventory",
            Variables = new
            {
                fetchRewardCampaigns = false,
            },
            Extensions = new Dictionary<string, object?>
            {
                ["persistedQuery"] = new Dictionary<string, object>
                {
                    ["sha256Hash"] = "09acb7d3d7e605a92bdfdcc465f6aa481b71c234d8686a9ba38ea5ed51507592",
                    ["version"] = 1
                }
            }
        };

        dynamic? resp = await DoGQLRequestAsync(query);

        Inventory? inventory = resp?.Data.CurrentUser.Inventory;

        if (inventory?.DropCampaignsInProgress == null)
        {
            inventory.DropCampaignsInProgress = new List<DropCampaign>();
        }

        foreach (var dropCampaign in inventory?.DropCampaignsInProgress)
        {
            dropCampaign.TimeBasedDrops = dropCampaign.TimeBasedDrops.OrderBy(drop => drop.RequiredMinutesWatched).ToList();
        }

        return inventory;
    }

    public async Task<Notifications?> FetchNotificationsAsync(int? limit = 10)
    {
        var query = new GraphQLRequest
        {
            OperationName = "OnsiteNotifications_ListNotifications",
            Variables = new
            {
                cursor = "",
                displayType = "VIEWER",
                language = "en",
                limit,
                shouldLoadLastBroadcast = false
            },
            Extensions = new Dictionary<string, object?>
            {
                ["persistedQuery"] = new Dictionary<string, object>
                {
                    ["sha256Hash"] = "e709b905ddb963d7cf4a8f6760148926ecbd0eee0f2edc48d1cf17f3e87f6490",
                    ["version"] = 1
                }
            }
        };

        dynamic? resp = await DoGQLRequestAsync(query);

        var notifications = resp?.Data.CurrentUser.Notifications;

        return notifications;
    }

    public async Task<Game?> FetchDirectoryPageGameAsync(string slug, bool mustHaveDrops)
    {
        var query = new GraphQLRequest
        {
            OperationName = "DirectoryPage_Game",
            Variables = new
            {
                imageWidth = 50,
                slug,
                options = new
                {
                    includeRestricted = new List<string>() { "SUB_ONLY_LIVE" },
                    sort = "RELEVANCE",
                    recommendationsContext = new
                    {
                        platform = "web"
                    },
                    requestID = "JIRA-VXP-2397",
                    // freeformTags = null,
                    tags = new List<string>(),
                    broadcasterLanguages = new List<string>(),
                    systemFilters = mustHaveDrops ? new List<string>() { "DROPS_ENABLED" } : null
                },
                includeIsDJ = true,
                sortTypeIsRecency = false,
                limit = 30,
                includePreviewBlur = true,
            },
            Extensions = new Dictionary<string, object?>
            {
                ["persistedQuery"] = new Dictionary<string, object>
                {
                    ["sha256Hash"] = "c7c9d5aad09155c4161d2382092dc44610367f3536aac39019ec2582ae5065f9",
                    ["version"] = 1
                }
            }
        };

        dynamic? resp = await DoGQLRequestAsync(query);

        return resp?.Data.Game;
    }

    public async Task<StreamPlaybackAccessToken?> FetchPlaybackAccessTokenAsync(string login)
    {
        var query = new GraphQLRequest
        {
            OperationName = "PlaybackAccessToken",
            Variables = new
            {
                isLive = true,
                login,
                isVod = false,
                vodID = "",
                platform = "web",
                playerType = "site",
            },
            Extensions = new Dictionary<string, object?>
            {
                ["persistedQuery"] = new Dictionary<string, object>
                {
                    ["sha256Hash"] = "ed230aa1e33e07eebb8928504583da78a5173989fadfb1ac94be06a04f3cdbe9",
                    ["version"] = 1
                }
            }
        };

        dynamic? resp = await DoGQLRequestAsync(query);

        return resp?.Data.StreamPlaybackAccessToken;
    }

    public async Task<User?> FetchStreamInformationAsync(string channel)
    {
        var query = new GraphQLRequest
        {
            OperationName = "VideoPlayerStreamInfoOverlayChannel",
            Variables = new
            {
                channel
            },
            Extensions = new Dictionary<string, object?>
            {
                ["persistedQuery"] = new Dictionary<string, object>
                {
                    ["sha256Hash"] = "a5f2e34d626a9f4f5c0204f910bab2194948a9502089be558bb6e779a9e1b3d2",
                    ["version"] = 1
                }
            }
        };

        dynamic? resp = await DoGQLRequestAsync(query);

        return resp?.Data.User;
    }

    public async Task<DropCurrentSession?> FetchCurrentSessionContextAsync(AbstractBroadcaster channel)
    {
        var query = new GraphQLRequest
        {
            OperationName = "DropCurrentSessionContext",
            Variables = new
            {
                channelLogin = channel.Login,
                //channelId = channel.Id
            },
            Extensions = new Dictionary<string, object?>
            {
                ["persistedQuery"] = new Dictionary<string, object>
                {
                    ["sha256Hash"] = "4d06b702d25d652afb9ef835d2a550031f1cf762b193523a92166f40ea3d142b",
                    ["version"] = 1
                }
            }
        };

        dynamic? resp = await DoGQLRequestAsync(query);

        return resp?.Data.CurrentUser.DropCurrentSession;
    }

    public async Task<bool> ClaimDropAsync(string dropInstanceID)
    {
        var query = new GraphQLRequest
        {
            OperationName = "DropsPage_ClaimDropRewards",
            Variables = new
            {
                input = new
                {
                    dropInstanceID
                }
            },
            Extensions = new Dictionary<string, object?>
            {
                ["persistedQuery"] = new Dictionary<string, object>
                {
                    ["sha256Hash"] = "a455deea71bdc9015b78eb49f4acfbce8baa7ccbedd28e549bb025bd0f751930",
                    ["version"] = 1
                }
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

                return graphQLResponse;

            }
            catch (System.Exception e)
            {
                if (i == 4)
                {
                    throw new System.Exception($"Failed to execute the query {name} (attempt {i + 1}/{limit}).");
                }

                twitchUser.Logger.Error($"Failed to execute the query {name} (attempt {i + 1}/{limit}).");
                SystemLogger.Error($"(${e.Message})");

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

    public static string ToKebabCase(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        text = text.Replace(" ", "-").Replace("_", "-");
        text = text.ToLowerInvariant();

        return text;
    }
}