using System;

namespace TwitchDropsBot.Core.Exception;

[Serializable]
public class CurrentDropSessionChanged : System.Exception
{
    private const string DefaultMessage = "RequiredMinutesWatched is equal to zero, restarting the loop.";


    public CurrentDropSessionChanged() : base(DefaultMessage) { }
    public CurrentDropSessionChanged(string message) : base(message) { }
    public CurrentDropSessionChanged(string message, System.Exception inner) : base(message, inner) { }
    protected CurrentDropSessionChanged(System.Runtime.Serialization.SerializationInfo info,
        System.Runtime.Serialization.StreamingContext context)
        : base(info, context)
    {
    }
}


