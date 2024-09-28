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

    public async Task<List<DropCampaign>> FetchDropsAsync()
    {
        var query = new GraphQLRequest
        {
            Query = @"
                    query FetchDrops($login: String!, $id: ID!) {
                        user(login: $login, id: $id) {
                            dropCampaigns {
                                id
                                name
                                game {
                                    id
                                    slug
                                    displayName
                                    boxArtURL
                                }
                                status
                                startAt
                                endAt
                                self {
                                    isAccountConnected
                                }
                                allow {
                                    channels {
                                        id
                                        name
                                        url
                                    }
                                }
                                timeBasedDrops {
                                    id
                                    name
                                    startAt
                                    endAt
                                    requiredMinutesWatched
                                    self {
                                        hasPreconditionsMet
                                        currentMinutesWatched
                                        isClaimed
                                        dropInstanceID
                                    }
                                    campaign {
                                        id
                                        detailsURL
                                        accountLinkURL
                                        self {
                                            isAccountConnected
                                        }
                                    }
                                }
                            }
                        }
                    }
                ",
            Variables = new
            {
                twitchUser.Login,
                twitchUser.Id
            }
        };
        
        dynamic? resp = await DoGQLRequestAsync(query, null, "FetchDrops");
        
        if (resp != null)
        {
            var dropCampaigns = resp.Data.User.DropCampaigns;
            
            dropCampaigns = ((List<DropCampaign>) dropCampaigns).FindAll(dropCampaign => dropCampaign is { Status: "ACTIVE" });
            
            List<string> favGames = twitchUser.FavouriteGames;
            foreach (var favGame in favGames)
            {
                foreach (var dropCampaign in dropCampaigns)
                {
                    if (dropCampaign.Game.DisplayName.ToLower().Equals(favGame.ToLower()) ||
                        dropCampaign.Game.Slug.ToLower().Equals(favGame.ToLower()))
                    {
                        dropCampaign.Game.IsFavorite = true;
                    }
                }
            }

            return dropCampaigns;
        }

        return new List<DropCampaign>();
    }

    public async Task<Inventory?> FetchInventoryDropsAsync()
    {
        var query = new GraphQLRequest
        {
            Query = @"
                    query FetchInventoryDrops($login: String!, $id: ID) {
                        user(login: $login, id: $id) {
                            inventory {
                                dropCampaignsInProgress {
                                    id
                                    detailsURL
                                    accountLinkURL
                                    startAt
                                    endAt
                                    imageURL
                                    name
                                    status
                                    self {
                                        isAccountConnected
                                    }
                                    game {
                                        id
                                        slug
                                        name
                                        boxArtURL
                                    }
                                    allow {
                                        channels {
                                            id
                                            name
                                            url
                                        }
                                    }
                                    eventBasedDrops {
                                        id
                                    }
                                    timeBasedDrops {
                                        id
                                        name
                                        startAt
                                        endAt
                                        requiredMinutesWatched
                                        benefitEdges {
                                            benefit {
                                                id
                                                imageAssetURL
                                                name
                                            }
                                            entitlementLimit
                                            claimCount
                                        }
                                        self {
                                            hasPreconditionsMet
                                            currentMinutesWatched
                                            isClaimed
                                            dropInstanceID
                                        }
                                        campaign {
                                            id
                                            detailsURL
                                            accountLinkURL
                                            self {
                                                isAccountConnected
                                            }
                                        }
                                    }
                                }
                                gameEventDrops {
                                    id
                                    name
                                    game {
                                        id
                                        slug
                                        displayName
                                        boxArtURL
                                    }
                                    isConnected
                                    totalCount
                                    lastAwardedAt
                               }
                            }
                        }
                    }
                ",
            Variables = new
            {
                twitchUser.Login,
                twitchUser.Id
            }
        };
        
        dynamic? resp = await DoGQLRequestAsync(query, null, "FetchInventoryDrops");

        return resp?.Data.User.Inventory;
    }

    public async Task<List<RewardCampaignsAvailableToUser>> FetchRewardCampaignsAvailableToUserAsync()
    {
        var query = new GraphQLRequest
        {
            Query = @"
                    query {
                        rewardCampaignsAvailableToUser {
                            id,
                            name,
                            brand,
                            startsAt,
                            endsAt,
                            status,
                            summary,
                            instructions,
                            externalURL,
                            rewardValueURLParam,
                            aboutURL,
                            isSitewide,
                            game {
                                id,
                                slug,
                                displayName
                            },
                            unlockRequirements {
                                subsGoal,
                                minuteWatchedGoal
                            },
                            image {
                                    image1xURL
                            },
                            rewards {
                                id,
                                name,
                                bannerImage {
                                    image1xURL
                                },
                                thumbnailImage, {
                                    image1xURL
                                },
                                earnableUntil,
                                redemptionInstructions,
                                redemptionURL
                            },
                        }
                    }
                "
        };

        dynamic? resp = await DoGQLRequestAsync(query, null, "FetchRewardCampaignsAvailableToUser");

        if (resp != null)
        {
            
            List<RewardCampaignsAvailableToUser> rewardCampaigns = resp.Data.RewardCampaignsAvailableToUser;

            rewardCampaigns = rewardCampaigns.FindAll(rewardCampaign => rewardCampaign.UnlockRequirements?.MinuteWatchedGoal != 0);

            foreach (var rewardCampaign in rewardCampaigns)
            {
                if (rewardCampaign.Game != null)
                {
                    List<string> favGames = twitchUser.FavouriteGames;
                    foreach (var favGame in favGames)
                    {
                        if (rewardCampaign.Game.DisplayName.ToLower().Equals(favGame.ToLower()) ||
                            rewardCampaign.Game.Slug.ToLower().Equals(favGame.ToLower()))
                        {
                            rewardCampaign.Game.IsFavorite = true;
                        }
                    }
                }
            }

            return rewardCampaigns;
        }

        return new List<RewardCampaignsAvailableToUser>();
    }

    public async Task<Game?> FetchDirectoryPageGameAsync(string slug)
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
                    systemFilters = new List<string>() { "DROPS_ENABLED" }
                },
                sortTypeIsRecency = false,
                limit = 30,
                includePreviewBlur = true,
            },
            Extensions = new Dictionary<string, object?>
            {
                ["persistedQuery"] = new Dictionary<string, object>
                {
                    ["sha256Hash"] = "e303f59d4836d19e66cb0f5a1efe15fbe2a1c02d314ad4f09982e825950b293d",
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
                playerType = "site"
            },
            Extensions = new Dictionary<string, object?>
            {
                ["persistedQuery"] = new Dictionary<string, object>
                {
                    ["sha256Hash"] = "3093517e37e4f4cb48906155bcd894150aef92617939236d2508f3375ab732ce",
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

                    return null;
                }
                
                return graphQLResponse;
                
            }catch (System.Exception e)
            {
                if (i == 4)
                {
                    throw new System.Exception($"Failed to execute the query {name} (attempt {i+1}/{limit}).");
                }
                
                twitchUser.Logger.Error($"Failed to execute the query {name} (attempt {i+1}/{limit}).");
                SystemLogger.Error(e.Message);
                
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
}