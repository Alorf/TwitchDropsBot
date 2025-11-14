using TwitchDropsBot.Core;
using TwitchDropsBot.Core.Platform.Shared.Services;

namespace TwitchDropsBot.WinForms
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();

#if RELEASE
            try
            {

                Application.Run(new MainForm());
            }
            catch (Exception e)
            {
                SystemLoggerService.Logger.Error(e, e.Message);
                Environment.Exit(1);
            }
#else

            Application.Run(new MainForm());

#endif

        }
    }
}