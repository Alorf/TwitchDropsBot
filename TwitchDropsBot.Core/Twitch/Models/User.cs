using System.Text.Json.Serialization;

namespace TwitchDropsBot.Core.Twitch.Models;

public partial class User
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("profileURL")]
    public string? ProfileURL { get; set; }

    [JsonPropertyName("login")]
    public string? Login { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("roles")]
    public UserRoles? Roles { get; set; }

    [JsonPropertyName("profileImageURL")]
    public string? ProfileImageURL { get; set; }

    [JsonPropertyName("primaryColorHex")]
    public string? PrimaryColorHex { get; set; }

    [JsonPropertyName("broadcastSettings")]
    public BroadcastSettings? BroadcastSettings { get; set; }

    [JsonPropertyName("stream")]
    public Stream? Stream { get; set; }

    [JsonPropertyName("dropCurrentSession")]
    public DropCurrentSession? DropCurrentSession { get; set; }

    [JsonPropertyName("inventory")]
    public Inventory? Inventory { get; set; }

    [JsonPropertyName("dropCampaign")]
    public DropCampaign? DropCampaign { get; set; }

    [JsonPropertyName("dropCampaigns")]
    public List<DropCampaign>? DropCampaigns { get; set; } = new List<DropCampaign>();

    [JsonPropertyName("notifications")]
    public Notifications? Notifications { get; set; }
    
}

public class Notifications
{
    [JsonPropertyName("pageInfo")]
    public PageInfo PageInfo { get; set; }
    
    [JsonPropertyName("edges")]
    public List<OnsiteNotificationEdge> Edges { get; set; } = new List<OnsiteNotificationEdge>();
    
}

public class PageInfo
{
    [JsonPropertyName("hasNextPage")]
    public bool HasNextPage { get; set; }
}

public class OnsiteNotificationEdge
{
    [JsonPropertyName("node")]
    public OnsiteNotification Node { get; set; }
}

public class OnsiteNotification
{
    [JsonPropertyName("id")]
    public string Id { get; set; }
    
    [JsonPropertyName("type")]
    public string Type { get; set; }
    
    [JsonPropertyName("body")]
    public string Body { get; set; }
    
    [JsonPropertyName("renderStyle")]
    public string RenderStyle { get; set; }
    
    [JsonPropertyName("createdAt")]
    public DateTime? CreatedAt { get; set; }
    
    [JsonPropertyName("updatedAt")]
    public DateTime? UpdatedAt { get; set; }
    
    [JsonPropertyName("isRead")]
    public bool IsRead { get; set; }
    
    [JsonPropertyName("ThumbnailURL")]
    public string ThumbnailUrl { get; set; }
    
    [JsonPropertyName("actions")]
    public List<OnsiteNotificationAction> Actions { get; set; } = new List<OnsiteNotificationAction>();
    
    [JsonPropertyName("displayType")]
    public string DisplayType { get; set; }
    
    [JsonPropertyName("aggregationType")]
    public string AggregationType { get; set; }
    
    [JsonPropertyName("collapseKey")]
    public string CollapseKey { get; set; }
    
    [JsonPropertyName("destinationType")]
    public string DestinationType { get; set; }
    
    [JsonPropertyName("IsMobileOnly")]
    public bool IsMobileOnly { get; set; }
}

public class OnsiteNotificationAction
{
    [JsonPropertyName("id")]
    public string Id { get; set; }
    
    [JsonPropertyName("label")]
    public string Label { get; set; }
    
    [JsonPropertyName("modalID")]
    public string ModalID { get; set; }
    
    [JsonPropertyName("body")]
    public string Body { get; set; }
    
    [JsonPropertyName("type")]
    public string Type { get; set; }
    
    [JsonPropertyName("url")]
    public string Url { get; set; }
}

