using Gtk;
using System;
using TwitchDropsBot.Core.Object.Config;
using TwitchDropsBot.Core.Object;
using UI = Gtk.Builder.ObjectAttribute;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using TwitchDropsBot.Core;
using TwitchDropsBot.Core.Exception;

namespace TwitchDropsBot.GTK
{
    internal class MainWindow : Window
    {

        //Get GUI elements
        [UI] private Notebook usersNotebook = null;

        public MainWindow() : this(new Builder("MainWindow.glade"))
        {
            AppConfig config = AppConfig.GetConfig();

            foreach (ConfigUser user in config.Users)
            {
                TwitchUser twitchUser = new TwitchUser(user.Login, user.Id, user.ClientSecret, user.UniqueId);

                StartBot(twitchUser);
                //tabControl1.TabPages.Add(CreateTabPage(twitchUser));
                usersNotebook.AppendPage(CreateTabPage(twitchUser), new Label(twitchUser.Login));
                usersNotebook.ShowAll();
                

                //InitList();
            }
        }

        private Task StartBot(TwitchUser twitchUser)
        {
            Bot bot = new Bot(twitchUser);
            TimeSpan waitingTime;
            twitchUser.CancellationTokenSource = new CancellationTokenSource();
            return Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        await bot.StartAsync();
                        waitingTime = TimeSpan.FromSeconds(20);
                    }
                    catch (NoBroadcasterOrNoCampaignLeft ex)
                    {
                        twitchUser.Logger.Info(ex.Message);
                        twitchUser.Logger.Info("Waiting 5 minutes before trying again.");
                        waitingTime = TimeSpan.FromMinutes(5);
                    }
                    catch (StreamOffline ex)
                    {
                        twitchUser.Logger.Info(ex.Message);
                        twitchUser.Logger.Info("Waiting 5 minutes before trying again.");
                        waitingTime = TimeSpan.FromMinutes(5);
                    }
                    catch (OperationCanceledException ex)
                    {
                        twitchUser.Logger.Info(ex.Message);
                        twitchUser.CancellationTokenSource = new CancellationTokenSource();
                        waitingTime = TimeSpan.FromSeconds(10);
                    }
                    catch (Exception ex)
                    {
                        twitchUser.Logger.Error(ex);
                        waitingTime = TimeSpan.FromMinutes(5);
                    }

                    twitchUser.StreamURL = null;
                    twitchUser.Status = BotStatus.Idle;

                    await Task.Delay(waitingTime);
                }
            });
        }

        private Widget CreateTabPage(TwitchUser twitchUser)
        {
            //return userNotebook first page
            var page = new TwitchUserTab(twitchUser).GetFirstPage();
            
            return page;
        }

        private MainWindow(Builder builder) : base(builder.GetRawOwnedObject("MainWindow"))
        {
            builder.Autoconnect(this);

            DeleteEvent += Window_DeleteEvent;
        }

        private void Window_DeleteEvent(object sender, DeleteEventArgs a)
        {
            Application.Quit();
        }
    }
}