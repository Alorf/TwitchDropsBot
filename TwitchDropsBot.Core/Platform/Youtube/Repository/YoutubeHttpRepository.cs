using TwitchDropsBot.Core.Platform.Youtube.Bot;
using HttpClient = System.Net.Http.HttpClient;
using TwitchDropsBot.Core.Platform.Shared.Repository;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TwitchDropsBot.Core.Platform.Shared.Settings;
using YoutubeDLSharp;
using YoutubeDLSharp.Metadata;
using YoutubeDLSharp.Options;

namespace TwitchDropsBot.Core.Platform.Youtube.Repository;

public class YoutubeHttpRepository : BotRepository<YoutubeUser>
{
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private readonly IOptionsMonitor<BotSettings> _botSettings;


    public YoutubeHttpRepository(YoutubeUser YoutubeUser, ILogger logger, IOptionsMonitor<BotSettings> botSettings)
    {
        BotUser = YoutubeUser;
        _logger = logger;
        _botSettings = botSettings;

        _logger.LogTrace("YoutubeHttpRepository initialized for user {Login}", YoutubeUser.Login);
    }

    public async Task InitializeYtDlSharpAsync()
    {
        await YoutubeDLSharp.Utils.DownloadYtDlp();
    }

    public async Task<bool> IsLive(string handle)
    {
        var ytdl = new YoutubeDL();

        var res = await ytdl.RunVideoDataFetch(channelLiveUrl);

        if (!res.Success)
        {
            _logger.LogWarning("Failed to fetch YouTube data for {Handle}: {Error}", 
                handle, string.Join(", ", res.ErrorOutput));
            return false;
        }

        VideoData video = res.Data;


        _logger.LogInformation("Channel {Handle} live status: {IsLive} | Title: {Title}", 
            handle, isLive, video.Title);

        return isLive;
    }}