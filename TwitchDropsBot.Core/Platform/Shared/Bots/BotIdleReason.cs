namespace TwitchDropsBot.Core.Platform.Shared.Bots;

/// <summary>Why the bot is between cycles, shown in the web UI.</summary>
public enum BotIdleReason
{
    None,
    CycleFinished,
    NothingToWatch,
    StreamOffline,
    DropSessionChanged,
    Switching,
    Error
}
