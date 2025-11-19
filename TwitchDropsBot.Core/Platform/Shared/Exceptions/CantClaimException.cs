namespace TwitchDropsBot.Core.Platform.Shared.Exceptions;

[Serializable]
public class CantClaimException : System.Exception
{
    private const string DefaultMessage = "Can't claim";


    public CantClaimException() : base(DefaultMessage) { }
    public CantClaimException(string message) : base(message) { }
    public CantClaimException(string message, System.Exception inner) : base(message, inner) { }
    protected CantClaimException(System.Runtime.Serialization.SerializationInfo info,
        System.Runtime.Serialization.StreamingContext context)
        : base(info, context)
    {
    }
}


