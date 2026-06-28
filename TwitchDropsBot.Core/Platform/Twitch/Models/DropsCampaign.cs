using System.Text.Json.Serialization;

namespace TwitchDropsBot.Core.Platform.Twitch.Models;

using System;
using System.Collections.Generic;

public class DropsCampaign
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("brandName")]
    public string BrandName { get; set; }

    [JsonPropertyName("detailsURL")]
    public string DetailsURL { get; set; }

    [JsonPropertyName("startAt")]
    public DateTime? StartAt { get; set; }

    [JsonPropertyName("endAt")]
    public DateTime? EndAt { get; set; }

    [JsonPropertyName("imageURL")]
    public string ImageURL { get; set; }

    [JsonPropertyName("hasViewerDismissedHighlight")]
    public bool HasViewerDismissedHighlight { get; set; }

    [JsonPropertyName("isPermanentlyDismissible")]
    public bool IsPermanentlyDismissible { get; set; }

    [JsonPropertyName("game")]
    public Game Game { get; set; }

    [JsonPropertyName("rewardGroups")]
    public List<DropsRewardGroup> RewardGroups { get; set; }
    
    [JsonPropertyName("ownerType")]
    public string OwnerType { get; set; }

}

// Reward Groups
public class DropsRewardGroup
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("progressCriteria")]
    public DropsProgressCriteria ProgressCriteria { get; set; }

    [JsonPropertyName("rewards")]
    public List<DropsReward> Rewards { get; set; }

    [JsonPropertyName("self")]
    public DropsRewardGroupSelfEdge Self { get; set; }

}

public class DropsProgressCriteria
{
    [JsonPropertyName("requirementType")]
    public string RequirementType { get; set; }  // SUBS

    [JsonPropertyName("requirements")]
    public DropsUnlockRequirement Requirements { get; set; }

    [JsonPropertyName("channels")]
    public string Channels { get; set; }

}

public class DropsUnlockRequirement
{
    [JsonPropertyName("minutesWatched")]
    public int? MinutesWatched { get; set; }

    [JsonPropertyName("subs")]
    public int? Subs { get; set; }

    [JsonPropertyName("turboSubs")]
    public int? TurboSubs { get; set; }

}

public class DropsReward
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("thumbnailURL")]
    public string ThumbnailURL { get; set; }
    
    [JsonPropertyName("redemptionURL")]
    public string RedemptionURL { get; set; }

    [JsonPropertyName("distributionType")]
    public DistributionType DistributionType { get; set; }

    [JsonPropertyName("accountLinkURL")]
    public string AccountLinkURL { get; set; }

    [JsonPropertyName("isAccountConnected")]
    public bool IsAccountConnected { get; set; }

}

public class DropsRewardGroupSelfEdge
{
    [JsonPropertyName("status")] // CLAIMED | IN_PROGRESS
    public string Status { get; set; }

    [JsonPropertyName("currentMinutesWatched")]
    public int? CurrentMinutesWatched { get; set; }

    [JsonPropertyName("currentSubs")]
    public int? CurrentSubs { get; set; }

}