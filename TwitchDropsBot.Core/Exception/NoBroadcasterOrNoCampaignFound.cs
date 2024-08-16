using System;

namespace TwitchDropsBot.Core.Exception;

[Serializable]
public class NoBroadcasterOrNoCampaignFound : System.Exception
{
    private const string DefaultMessage = "No broadcaster or campaign found.";


    public NoBroadcasterOrNoCampaignFound() : base(DefaultMessage) { }
    public NoBroadcasterOrNoCampaignFound(string message) : base(message) { }
    public NoBroadcasterOrNoCampaignFound(string message, System.Exception inner) : base(message, inner) { }
    protected NoBroadcasterOrNoCampaignFound(System.Runtime.Serialization.SerializationInfo info,
        System.Runtime.Serialization.StreamingContext context)
        : base(info, context)
    {
    }
}

