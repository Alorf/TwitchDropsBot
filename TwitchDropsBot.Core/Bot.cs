using Discord.Webhook;
using Discord;
using System.Diagnostics;
using TwitchDropsBot.Core.Exception;
using TwitchDropsBot.Core.Object;
using TwitchDropsBot.Core.Object.Config;
using TwitchDropsBot.Core.Object.TwitchGQL;
using Game = TwitchDropsBot.Core.Object.TwitchGQL.Game;
using System.Threading.Channels;
using Channel = TwitchDropsBot.Core.Object.TwitchGQL.Channel;

namespace TwitchDropsBot.Core;

public class Bot
{
    private TwitchUser twitchUser;
    private AppConfig config;
    private static readonly object _configLock = new object();

    public Bot(TwitchUser twitchUser)
    {
        this.twitchUser = twitchUser;
        config = AppConfig.Instance;
    }

    public static Task StartBot(TwitchUser twitchUser)
    {
        Bot bot = new Bot(twitchUser);
        TimeSpan waitingTime;
        twitchUser.CancellationTokenSource = new CancellationTokenSource();
        return Task.Run(async () =>
        {

            AppConfig config = AppConfig.Instance;

            while (true)
            {
                try
                {
                    await bot.StartAsync();
                    waitingTime = TimeSpan.FromSeconds(20);
                }
                catch (NoBroadcasterOrNoCampaignLeft ex)
                {
                    twitchUser.Logger.Info(ex.Message);
                    twitchUser.Logger.Info($"Waiting {config.waitingSeconds} seconds before trying again.");
                    waitingTime = TimeSpan.FromSeconds(config.waitingSeconds);
                }
                catch (StreamOffline ex)
                {
                    twitchUser.Logger.Info(ex.Message);
                    twitchUser.Logger.Info($"Waiting {config.waitingSeconds} seconds before trying again.");
                    waitingTime = TimeSpan.FromSeconds(config.waitingSeconds);
                }
                catch (OperationCanceledException ex)
                {
                    twitchUser.Logger.Info(ex.Message);
                    twitchUser.CancellationTokenSource = new CancellationTokenSource();
                    waitingTime = TimeSpan.FromSeconds(10);
                }
                catch (System.Exception ex)
                {
                    twitchUser.Logger.Error(ex);
                    waitingTime = TimeSpan.FromSeconds(config.waitingSeconds);
                }

                twitchUser.StreamURL = null;
                twitchUser.Status = BotStatus.Idle;

                await Task.Delay(waitingTime);
            }
        });
    }

    public async Task StartAsync()
    {
        lock (_configLock)
        {
            config.GetConfig();
        }
        twitchUser.FavouriteGames = config.Users.Find(x => x.Id == twitchUser.Id).FavouriteGames ?? config.FavouriteGames;
        twitchUser.OnlyFavouriteGames = config.OnlyFavouriteGames;
        twitchUser.OnlyConnectedAccounts = config.OnlyConnectedAccounts;

        // Get campaigns
        List<AbstractCampaign> thingsToWatch = await twitchUser.GqlRequest.FetchDropsAsync();
        // Get inventory
        Inventory? inventory = await twitchUser.GqlRequest.FetchInventoryDropsAsync();
        twitchUser.Inventory = inventory;

        twitchUser.Status = BotStatus.Seeking;

        await CheckCancellation();
        await CheckForClaim(inventory);
        await CheckCancellation();

        if (twitchUser.OnlyConnectedAccounts)
        {
            thingsToWatch.RemoveAll(x => x is DropCampaign dropCampaign && !dropCampaign.Self.IsAccountConnected && dropCampaign.AccountLinkURL != "https://twitch.tv/");
        }

        if (twitchUser.OnlyFavouriteGames)
        {
            thingsToWatch.RemoveAll(x => !x.Game.IsFavorite);
        }

        // Assuming you have a list of favorite game names
        List<string> favoriteGameNames = twitchUser.FavouriteGames;

        // Order things to watch by the order of favorite game names and drop that is ending soon
        await CheckCancellation();
        thingsToWatch = thingsToWatch
            .OrderBy(x => favoriteGameNames.IndexOf(x.Game.DisplayName) == -1 ? int.MaxValue : favoriteGameNames.IndexOf(x.Game.DisplayName))
            .ThenBy(x => (x as DropCampaign)?.EndAt ?? DateTime.MaxValue)
            .ToList();

        bool timeBasedDropFound = false;
        AbstractCampaign? campaign;
        AbstractBroadcaster? broadcaster;
        TimeBasedDrop currentTimeBasedDrop;
        DropCurrentSession? dropCurrentSession = null; // Rewards also use that

        do
        {
            if (thingsToWatch.Count == 0)
            {
                throw new NoBroadcasterOrNoCampaignLeft();
            }

            await CheckCancellation();
            var result = await SelectBroadcasterAsync(thingsToWatch);
            campaign = result.campaign;
            broadcaster = result.broadcaster;

//            var campaignsAvailable = await twitchUser.GqlRequest.FetchAvailableDropsAsync(broadcaster?.Id);

            if (campaign == null || broadcaster == null /*|| campaignsAvailable.Count == 0*/)
            {
                if (broadcaster == null)
                {
                    twitchUser.Logger.Log($"No broadcaster found for this campaign ({campaign?.Name}).");
                }

//              if (broadcaster != null && campaignsAvailable.Count == 0)
//              {
//                  twitchUser.Logger.Log($"It seems like every drops have been watched for this campaign ({campaign?.Name} | {broadcaster.Login} - {broadcaster.Id}).");
//              }

                var ToDelete = thingsToWatch.Find(x => x.Id == campaign.Id);
                thingsToWatch.Remove(ToDelete);
            }
            else
            {
                twitchUser.Logger.Log(
                    $"Current drop campaign: {campaign?.Name} ({campaign?.Game.DisplayName}), watching {broadcaster.Login} | {broadcaster.Id}");
                twitchUser.CurrentCampaign = campaign;

                dropCurrentSession =
                    await twitchUser.GqlRequest.FetchCurrentSessionContextAsync(broadcaster);

                if (string.IsNullOrEmpty(dropCurrentSession?.DropId) || dropCurrentSession.Channel.Id != broadcaster.Id || dropCurrentSession.CurrentMinutesWatched == dropCurrentSession.requiredMinutesWatched)
                {

                    if (dropCurrentSession != null && dropCurrentSession?.Channel != null && dropCurrentSession?.Channel.Id != broadcaster.Id)
                    {
                        twitchUser.Logger.Log($"Drop found but not the right channel, watching 20 sec to init the drop (got {dropCurrentSession.Channel.Name} instead of {broadcaster.Login})");
                    }


                    // Sometimes, we have to watch 2 or 3 times to init the drop or it will skip
                    for (int i = 0; i < config.AttemptToWatch; i++)
                    {
                        twitchUser.Logger.Log($"No time based drop found, watching 20 sec to init the drop");
                        dropCurrentSession = await FakeWatchAsync(broadcaster);
                    }
                }

                if (campaign is DropCampaign dropCampaign)
                {

                    currentTimeBasedDrop = dropCampaign!.TimeBasedDrops.Find((x) => x.Id == dropCurrentSession.DropId);
                    twitchUser.CurrentTimeBasedDrop = currentTimeBasedDrop;

                    // idk why but sometimes CurrentMinutesWatched is > requiredMinutesWatched for some reason
                    if (currentTimeBasedDrop == null || dropCurrentSession?.CurrentMinutesWatched >= dropCurrentSession?.requiredMinutesWatched)
                    {
                        twitchUser.Logger.Log($"Time based drop not found, skipping");
                        var toDelete = thingsToWatch.Find(x => x.Id == dropCampaign.Id);
                        thingsToWatch.Remove(toDelete);
                    }
                    else
                    {
                        twitchUser.Logger.Log($"Time based drops : {currentTimeBasedDrop?.Name}");

                        timeBasedDropFound = true;
                    }
                }
                else
                {
                    if (dropCurrentSession?.CurrentMinutesWatched >= dropCurrentSession?.requiredMinutesWatched)
                    {
                        var toDelete = thingsToWatch.Find(x => x.Id == campaign.Id);
                        thingsToWatch.Remove(toDelete);
                    }
                    else
                    {
                        twitchUser.Logger.Log($"Time based drops : {campaign?.Name}");

                        timeBasedDropFound = true;
                    }
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(2));
        } while (!timeBasedDropFound);

        twitchUser.Status = BotStatus.Watching;
        await WatchStreamAsync(broadcaster, dropCurrentSession);
    }

    public async Task WatchStreamAsync(AbstractBroadcaster broadcaster, DropCurrentSession dropCurrentSession,
        int? minutes = null)
    {
        int stuckCounter = 0;
        int previousMinuteWatched = 0;
        var minuteWatched = dropCurrentSession.CurrentMinutesWatched;
        twitchUser.CurrentDropCurrentSession = dropCurrentSession;

        while (minuteWatched <
               (minutes ?? dropCurrentSession.requiredMinutesWatched) || dropCurrentSession.requiredMinutesWatched == 0) // While all the drops are not claimed
        {
            await CheckCancellation();

            try
            {
                await twitchUser.WatchStreamAsync(broadcaster.Login); // If not live, it will throw a 404 error    
            }
            catch (System.Exception ex)
            {
                twitchUser.Logger.Error(ex);
                twitchUser.StreamURL = null;
                throw new StreamOffline();
            }

            try
            {
                dropCurrentSession =
                    await twitchUser.GqlRequest.FetchCurrentSessionContextAsync(broadcaster);

                twitchUser.CurrentDropCurrentSession = dropCurrentSession;
            }
            catch (System.Exception e)
            {
                twitchUser.Logger.Error(e);
            }

            minuteWatched = dropCurrentSession.CurrentMinutesWatched;

            if (previousMinuteWatched == minuteWatched)
            {
                stuckCounter++;
            }
            else
            {
                stuckCounter = 0;
            }

            if (stuckCounter >= 30)
            {
                twitchUser.StreamURL = null;
                await twitchUser.WatchStreamAsync(broadcaster.Login);
                await Task.Delay(TimeSpan.FromSeconds(20));
                dropCurrentSession =
                    await twitchUser.GqlRequest.FetchCurrentSessionContextAsync(broadcaster);
            }

            previousMinuteWatched = minuteWatched;

            if (dropCurrentSession.requiredMinutesWatched == 0)
            {
                break;
            }

            twitchUser.Logger.Log(
                $"Waiting 20 seconds... {minuteWatched}/{dropCurrentSession.requiredMinutesWatched} minutes watched.");

            await Task.Delay(TimeSpan.FromSeconds(20));
        }

        await NotifiateAsync();
    }

    public async Task<(AbstractCampaign? campaign, AbstractBroadcaster? broadcaster)> SelectBroadcasterAsync(
        List<AbstractCampaign> campaigns)
    {
        AbstractBroadcaster broadcaster = null;
        foreach (var campaign in campaigns)
        {
            await CheckCancellation();
            twitchUser.Logger.Log($"Checking {campaign.Game.DisplayName}...");

            if (campaign is DropCampaign dropCampaign)
            {
                var tempDropCampaign = await twitchUser.GqlRequest.FetchTimeBasedDropsAsync(campaign.Id);
                dropCampaign.TimeBasedDrops = tempDropCampaign.TimeBasedDrops;
                dropCampaign.Game = tempDropCampaign.Game;
                dropCampaign.Allow = tempDropCampaign.Allow;

                List<Channel>? channels = dropCampaign.GetChannels();

                if (channels != null && channels.Count <= 10)
                {
                    foreach (var channel in channels)
                    {
                        await CheckCancellation();
                        var tempBroadcaster = await twitchUser.GqlRequest.FetchStreamInformationAsync(channel.Name);

                        if (tempBroadcaster == null)
                        {
                            continue;
                        }

                        if (tempBroadcaster.IsLive() && tempBroadcaster.BroadcastSettings.Game?.Id != null &&
                            (campaign.Game.DisplayName == "Special Events" || tempBroadcaster.BroadcastSettings.Game.Id == campaign.Game.Id))
                        {
                            broadcaster = tempBroadcaster;
                            twitchUser.CurrentBroadcaster = broadcaster;
                            break;
                        }
                    }

                    return (campaign, broadcaster);
                }
            }

            // Search for channel that potentially have the drops
            Game game = await twitchUser.GqlRequest.FetchDirectoryPageGameAsync(campaign.Game.Slug, campaign is DropCampaign);

            // Select the channel that have the most viewers
            game.Streams.Edges = game.Streams.Edges.OrderByDescending(x => x.Node.ViewersCount).ToList();
            var edge = game.Streams.Edges.FirstOrDefault();
            if (edge != null)
            {
                broadcaster = edge.Node.Broadcaster;
                twitchUser.CurrentBroadcaster = broadcaster;
            }

            return (campaign, broadcaster);

        }

        return (null, null);
    }

    private async Task CheckForClaim(Inventory? inventory)
    {
        if (inventory?.DropCampaignsInProgress != null)
        {
            bool haveClaimed = false;
            // For every timebased drop, check if it is claimed
            foreach (var dropCampaignInProgress in inventory.DropCampaignsInProgress)
            {
                await CheckCancellation();
                foreach (var timeBasedDrop in dropCampaignInProgress.TimeBasedDrops)
                {
                    if (timeBasedDrop.Self.IsClaimed == false && timeBasedDrop.Self?.DropInstanceID != null)
                    {
                        await twitchUser.GqlRequest.ClaimDropAsync(timeBasedDrop.Self.DropInstanceID);
                        haveClaimed = true;
                    }
                }
            }

            if (haveClaimed)
            {
                await Task.Delay(TimeSpan.FromSeconds(20));
            }
        }
    }

    private async Task NotifiateAsync()
    {
        var notifications = await twitchUser.GqlRequest.FetchNotificationsAsync(1);

        List<Embed> embeds = new List<Embed>();

        foreach (var edge in notifications.Edges)
        {
            //Search for the first action with the type "click"
            var action = edge.Node.Actions.FirstOrDefault(x => x.Type == "click");

            var description = System.Net.WebUtility.HtmlDecode(edge.Node.Body);

            Embed embed = new EmbedBuilder()
                .WithTitle("You received a new item!")
                .WithDescription(edge.Node.Body)
                .WithColor(new Color(2326507))
                .WithThumbnailUrl(edge.Node.ThumbnailURL)
                .WithUrl(action.Url)
                .Build();

            embeds.Add(embed);
        }

        await twitchUser.SendWebhookAsync(embeds);

    }

    private async Task<DropCurrentSession> FakeWatchAsync(AbstractBroadcaster broadcaster)
    {
        //trying to init the drop

        await twitchUser.WatchStreamAsync(broadcaster.Login);
        twitchUser.StreamURL = null;
        await Task.Delay(TimeSpan.FromSeconds(20));

        return await twitchUser.GqlRequest.FetchCurrentSessionContextAsync(broadcaster);
    }

    private async Task CheckCancellation()
    {
        if (twitchUser.CancellationTokenSource != null && twitchUser.CancellationTokenSource.Token.IsCancellationRequested)
        {
            throw new OperationCanceledException();
        }
    }
}