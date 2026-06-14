using QRCoder;

namespace TwitchDropsBot.Console.Helpers;

public static class QrConsoleHelper
{
    public static void DisplayQrCode(string text, int scale = 1)
    {
        using var qrGenerator = new QRCodeGenerator();
        using var qrData = qrGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        var matrix = qrData.ModuleMatrix;
        int size = matrix.Count;

        for (int row = 0; row < size; row += 2)
        {
            for (int sy = 0; sy < scale; sy++)
            {
                for (int col = 0; col < size; col++)
                {
                    bool top = matrix[row][col];
                    bool bottom = row + 1 < size ? matrix[row + 1][col] : false;

                    for (int sx = 0; sx < scale; sx++)
                    {
                        System.Console.Write(Pixel(top, bottom));
                    }
                }

                if (scale > 1)
                    System.Console.WriteLine();
            }

            System.Console.WriteLine();
        }
    }

    public static void DisplayQrCode(string text, string label, int scale = 1)
    {
        System.Console.WriteLine(label);
        DisplayQrCode(text, scale);
    }

    private static char Pixel(bool top, bool bottom)
    {
        if (top && bottom) return '█';
        if (top && !bottom) return '▀';
        if (!top && bottom) return '▄';
        return ' ';
    }
}
