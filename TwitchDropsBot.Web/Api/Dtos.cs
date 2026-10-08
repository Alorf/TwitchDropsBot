namespace TwitchDropsBot.Web.Api;

// What the page receives. Built from the bot's state; never carries tokens, the webhook or other config.

public record StateDto(string Version, bool Demo, IReadOnlyList<AccountDto> Accounts);

public record AccountDto(
    string Id,
    string Login,
    string Status,
    string? IdleReason,
    DateTime? NextCheckAt,
    WatchingDto? Watching,
    CheckingDto? Checking,
    ErrorDto? LastError,
    IReadOnlyList<QueueItemDto> Queue,
    DateTime? CampaignsUpdatedAt,
    string CampaignsVersion,
    int CampaignCount,
    bool OnlyFavourites);

public record WatchingDto(
    string CampaignId,
    string CampaignName,
    string? GameName,
    string? BoxArtUrl,
    string? ChannelLogin,
    string? ChannelName,
    string? ChannelAvatarUrl,
    string? DropName,
    string? DropImageUrl,
    int? MinutesWatched,
    int? MinutesRequired,
    DateTime? CampaignEndsAt,
    IReadOnlyList<TierDto> Tiers);

/// <summary>One drop of a campaign. Current is null when this account's progress on it isn't known.</summary>
public record TierDto(string Name, string? ImageUrl, int? Current, int Required, bool Claimed, bool Active);

public record CheckingDto(string CampaignName, string? GameName);

public record ErrorDto(string Message, DateTime At);

public record NoteDto(string Kind, DateTime At);

public record ProgressDto(int Watched, int Required, int Claimed, int Total, int Waiting);

public record QueueItemDto(
    string CampaignId,
    string? CampaignName,
    string? GameName,
    string? BoxArtUrl,
    DateTime? EndsAt,
    string State,
    NoteDto? Note,
    ProgressDto? Progress,
    bool NeedsLink);

public record CampaignListDto(DateTime? UpdatedAt, string Version, IReadOnlyList<CampaignDto> Campaigns);

public record CampaignDto(
    string Id,
    string Name,
    string? GameName,
    string? BoxArtUrl,
    DateTime? StartsAt,
    DateTime? EndsAt,
    string Kind,
    bool IsFavourite,
    string Link,
    string? LinkUrl,
    bool IsAvoided,
    bool IsWatching,
    int? QueuePosition,
    int? TotalMinutes,
    ProgressDto? Progress,
    NoteDto? Note,
    string Status,
    bool NeedsLink,
    string? StatusSource,
    DateTime? StatusCheckedAt);

public record CampaignDetailDto(
    CampaignDto Campaign,
    IReadOnlyList<TierDto> Tiers,
    IReadOnlyList<string>? Channels,
    int ChannelCount,
    string? DetailsUrl,
    bool CanQueue,
    string? CannotQueueReason,
    string? StatusChannel,
    string? StatusProblem);

public record LogLineDto(long Seq, DateTime At, string Level, string Message);

public record LogPageDto(IReadOnlyList<LogLineDto> Lines, long LastSeq);

public record QueueRequest(string CampaignId, string? Position, bool SwitchNow);

public record ReorderRequest(IReadOnlyList<string> CampaignIds);

public record LoginRequest(string? Password);

public record SessionDto(bool Authenticated, bool LoginRequired, string Version, bool Demo);

public record ProblemDto(string Error);
