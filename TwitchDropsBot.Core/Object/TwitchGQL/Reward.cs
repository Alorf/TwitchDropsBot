namespace TwitchDropsBot.Core.Object.TwitchGQL;
public class Reward
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public DateTime EarnableUntil { get; set; }
    public string? RedemptionInstructions { get; set; }
    public string? RedemptionURL { get; set; }
}