namespace TwitchDropsBot.Console.Utils;

public class UserInput
{
    public static string ReadInput(string[] AcceptedValues)
    {
        var input = System.Console.ReadLine()?.Trim()?.ToLower();

        if (string.IsNullOrEmpty(input))
        {
            throw new Exception("No input");
        }

        if (!AcceptedValues.Contains(input))
        {
            throw new Exception("Invalid input");
        }

        return input;
    }

    public static string? ReadInput()
    {
        return System.Console.ReadLine();
    }
}