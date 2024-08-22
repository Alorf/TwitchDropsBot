using TwitchDropsBot.Core.Object;

namespace TwitchDropsBot.Core;

public class SystemLogger
{

    public static void Log(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[{DateTime.Now}] LOG : {message}");
        Console.ResetColor();
    }

    public static void Error(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[{DateTime.Now}] ERROR : {message}");
        Console.ResetColor();
    }

    public static void Error(System.Exception exception)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[{DateTime.Now}] ERROR : {exception.Message}\n{exception.StackTrace}");

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
    }

    public static void Info(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[{DateTime.Now}] INFO : {message}");
        Console.ResetColor();
    }
}