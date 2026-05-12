using TwitchDropsBot.Core.Platform.Youtube.Bot;
using HttpClient = System.Net.Http.HttpClient;
using TwitchDropsBot.Core.Platform.Shared.Repository;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TwitchDropsBot.Core.Platform.Shared.Settings;

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

    
}