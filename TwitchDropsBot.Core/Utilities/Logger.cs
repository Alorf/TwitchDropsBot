using Discord;
using TwitchDropsBot.Core.Object;

namespace TwitchDropsBot.Core;

public class Logger
{
    public TwitchUser TwitchUser { get; set; }

    public event Action<string> OnLog;
    public event Action<string> OnError;
    public event Action<string> OnInfo;

    public void Log(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[{TwitchUser.Login} - {DateTime.Now}] LOG : {message}");
        Console.ResetColor();

        OnLog?.Invoke(message);
    }

    public void Log(string message, string type, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.WriteLine($"[{TwitchUser.Login} - {DateTime.Now}] {type} : {message}");
        Console.ResetColor();

        OnLog?.Invoke(message);
    }

    public void Error(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[{TwitchUser.Login} - {DateTime.Now}] ERROR : {message}");
        Console.ResetColor();

        OnError?.Invoke(message);
    }

    public void Error(System.Exception exception)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[{TwitchUser.Login} - {DateTime.Now}] ERROR : {exception.Message}\n{exception.StackTrace}");

        foreach (var data in exception.Data)
        {
            Console.WriteLine(data);
        }
        
        // print inner exception
        if (exception.InnerException != null)
        {
            Console.WriteLine(exception.InnerException);
        }
        
        Console.ResetColor();

        OnError?.Invoke(exception.Message);
    }

    public void Info(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[{TwitchUser.Login} - {DateTime.Now}] INFO : {message}");
        Console.ResetColor();

        OnInfo?.Invoke(message);
    }
}