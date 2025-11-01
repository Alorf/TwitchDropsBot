namespace TwitchDropsBot.Core.Object.TwitchGQL;
public class Reward
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public thumbnailImage? thumbnailImage { get; set; } 
    public DateTime EarnableUntil { get; set; }
    public string? RedemptionInstructions { get; set; }
    public string? RedemptionURL { get; set; }
    
    public string GetImageUrl()
    {
        return thumbnailImage?.image1xURL ?? "";
    }
}

public class thumbnailImage
{
    public string image1xURL { get; set; }
}