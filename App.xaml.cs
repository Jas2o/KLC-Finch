using LibKaseya;
using nucs.JsonSettings;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace KLC_Finch {
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application {

        public static string Version;
        public static WindowAlternative winStandalone;
        public static WindowViewerV3 winStandaloneViewer;
        public static Settings Settings;
        public static KLCShared Shared;

        public App() : base() {
            if (!Debugger.IsAttached) {
                //Setup exception handling rather than closing rudely
                AppDomain.CurrentDomain.UnhandledException += (sender, args) => ShowUnhandledException(args.ExceptionObject as Exception, "AppDomain.CurrentDomain.UnhandledException");
                TaskScheduler.UnobservedTaskException += (sender, args) => {
                    if (Settings.AltShowWarnings)
                        ShowUnhandledExceptionFromSrc(args.Exception, "TaskScheduler.UnobservedTaskException");
                    args.SetObserved();
                };

                Dispatcher.UnhandledException += (sender, args) => {
                    args.Handled = true;
                    if (Settings.AltShowWarnings)
                        ShowUnhandledException(args.Exception, "Dispatcher.UnhandledException");
                };
            }

            Version = KLC_Finch.Properties.Resources.BuildDate.Trim();

            //--

            string pathSettings = Path.GetDirectoryName(Environment.ProcessPath) + "\\KLC-Finch-config.json";
            if (File.Exists(pathSettings))
                Settings = JsonSettings.Load<Settings>(pathSettings);
            else
                Settings = JsonSettings.Construct<Settings>(pathSettings);

            string pathShared = Path.GetDirectoryName(Environment.ProcessPath) + @"\KLC-Shared.json";
            if (File.Exists(pathShared))
                Shared = JsonSettings.Load<KLCShared>(pathShared);
            else
                Shared = JsonSettings.Construct<KLCShared>(pathShared);
        }

        public static void ShowUnhandledExceptionFromSrc(Exception e, string source) {
            Application.Current.Dispatcher.Invoke((Action)delegate {
                new WindowException(e, source).Show();
            });
        }

        public static void ShowUnhandledExceptionFromSrc(string error, string source) {
            Application.Current.Dispatcher.Invoke((Action)delegate {
                new WindowException(error, source).Show();
            });
        }

        [STAThread]
        private static void ShowUnhandledException(Exception e, string unhandledExceptionType) {
            new WindowException(e, unhandledExceptionType).Show(); //Removed: , Debugger.IsAttached
        }

        private void Application_Startup(object sender, StartupEventArgs e) {
            foreach (string vsa in App.Shared.VSA)
            {
                Kaseya.Start(vsa, KaseyaAuth.GetStoredAuth(vsa));
            }

            string[] args = Environment.GetCommandLineArgs();
            KLCCommand command = null;
            for (int i = 1; i < args.Length; i++)
            {
                if (args[i].StartsWith("liveconnect:///"))
                {
                    command = KLCCommand.NewFromBase64(args[i].Replace("liveconnect:///", ""));
                }
            }

            if (command != null)
            {
                if (command.payload.navId == "remotecontrol/shared")
                    winStandalone = new WindowAlternative(command.payload.agentId, command.VSA, command.payload.auth.Token, Enums.OnConnect.OnlyRC, Enums.RC.Shared);
                else if (command.payload.navId == "remotecontrol/private")
                    winStandalone = new WindowAlternative(command.payload.agentId, command.VSA, command.payload.auth.Token, Enums.OnConnect.OnlyRC, Enums.RC.Private);
                else if (command.payload.navId.StartsWith("remotecontrol/private/#"))
                    winStandalone = new WindowAlternative(command.payload.agentId, command.VSA, command.payload.auth.Token, Enums.OnConnect.AlsoRC, Enums.RC.NativeRDP);
                else if (command.payload.navId == "remotecontrol/1-click")
                    winStandalone = new WindowAlternative(command.payload.agentId, command.VSA, command.payload.auth.Token, Enums.OnConnect.OnlyRC, Enums.RC.OneClick);
                else
                    winStandalone = new WindowAlternative(command.payload.agentId, command.VSA, command.payload.auth.Token);

                winStandalone.Show();
            }
            else
            {
                new MainWindow().Show();
            }
        }

    }
}
