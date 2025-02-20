using Gtk;
using System;
using System.Reflection;
using TwitchDropsBot.Core;

namespace TwitchDropsBot.GTK
{
    internal class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            Application.Init();

            var app = new Application("org.TwitchDropsBot.GTK.TwitchDropsBot.GTK", GLib.ApplicationFlags.None);
            app.Register(GLib.Cancellable.Current);

            var win = new MainWindow();
            app.AddWindow(win);

            // Enable dark theme
            var settings = Settings.Default;
            settings.SetProperty("gtk-application-prefer-dark-theme", new GLib.Value(true));

            // Load the embedded resource
            var assembly = Assembly.GetExecutingAssembly();

            using (var stream = assembly.GetManifestResourceStream("TwitchDropsBot.GTK.images.logo.png"))
            {
                if (stream != null)
                {
                    win.Icon = new Gdk.Pixbuf(stream);
                }
                else
                {
                    Console.WriteLine("Resource not found.");
                }
            }
            win.Show();

#if RELEASE
            try
            {
                Application.Run();
            }
            catch (Exception e)
            {
                SystemLogger.Error(e);
                Environment.Exit(1);
            }
#else
            Application.Run();
#endif
            
        }
    }
}
