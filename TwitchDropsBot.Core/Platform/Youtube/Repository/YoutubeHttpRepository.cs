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
    private YoutubeDL _ytdl;

    public YoutubeHttpRepository(YoutubeUser YoutubeUser, ILogger logger, IOptionsMonitor<BotSettings> botSettings)
    {
        BotUser = YoutubeUser;
        _logger = logger;
        _botSettings = botSettings;

        _logger.LogTrace("YoutubeHttpRepository initialized for user {Login}", YoutubeUser.Login);
        YoutubeDLSharp.Utils.DownloadYtDlp().Wait();
        _ytdl = new YoutubeDL();
    }

    public async Task<bool> IsLive(string handle)
    {
        // L'URL du live d'une chaîne via son handle
        var channelLiveUrl = $"https://www.youtube.com/@{handle}/live";

        var res = await _ytdl.RunVideoDataFetch(channelLiveUrl);

        if (!res.Success)
        {
            _logger.LogWarning("Failed to fetch YouTube data for {Handle}: {Error}", 
                handle, string.Join(", ", res.ErrorOutput));
            return false;
        }

        VideoData video = res.Data;

        // Si la chaîne n'est pas en live, yt-dlp redirige vers une VOD ou échoue
        bool isLive = video.IsLive ?? false;

        _logger.LogInformation("Channel {Handle} live status: {IsLive} | Title: {Title}", 
            handle, isLive, video.Title);

        return isLive;
    }}