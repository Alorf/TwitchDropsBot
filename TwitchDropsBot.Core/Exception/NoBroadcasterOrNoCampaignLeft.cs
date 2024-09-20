using System;

namespace TwitchDropsBot.Core.Exception;

[Serializable]
public class NoBroadcasterOrNoCampaignLeft : System.Exception
{
    private const string DefaultMessage = "No broadcaster or campaign left.";
    
    public NoBroadcasterOrNoCampaignLeft() : base(DefaultMessage) { }
    public NoBroadcasterOrNoCampaignLeft(string message) : base(message) { }
    public NoBroadcasterOrNoCampaignLeft(string message, System.Exception inner) : base(message, inner) { }
    protected NoBroadcasterOrNoCampaignLeft(System.Runtime.Serialization.SerializationInfo info,
        System.Runtime.Serialization.StreamingContext context)
        : base(info, context)
    {
    }
}