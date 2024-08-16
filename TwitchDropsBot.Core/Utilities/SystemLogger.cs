namespace TwitchDropsBot.Core;

public class SystemLogger : Logger
{
    public static void Log(string message)
    {
        Log(message);
    }
    
    public static void Error(string message)
    {
        Error(message);
    }
    
    public static void Error(System.Exception exception)
    {
        Error(exception);
    }
    
    public static void Info(string message)
    {
        Info(message);
    }
}