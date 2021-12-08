using LibKaseya;
using nucs.JsonSettings;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace KLC_Finch {
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application {

        public static string Version;
        public static WindowAlternative alternative;
        public static WindowViewerV3 viewer;
        public static Settings Settings;

        public App() : base() {
            if (!Debugger.IsAttached) {
                //Setup exception handling rather than closing rudely (this doesn't really work well).
                AppDomain.CurrentDomain.UnhandledException += (sender, args) => ShowUnhandledException(args.ExceptionObject as Exception, "AppDomain.CurrentDomain.UnhandledException");
                TaskScheduler.UnobservedTaskException += (sender, args) => {
                    ShowUnhandledException(args.Exception, "TaskScheduler.UnobservedTaskException");
                    args.SetObserved();
                };

                Dispatcher.UnhandledException += (sender, args) => {
                    args.Handled = true;
                    ShowUnhandledException(args.Exception, "Dispatcher.UnhandledException");
                };
            }

            Version = KLC_Finch.Properties.Resources.BuildDate.Trim();

            string pathSettings = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "\\KLC-Finch-config.json";
            if (File.Exists(pathSettings))
                Settings = JsonSettings.Load<Settings>(pathSettings);
            else
                Settings = JsonSettings.Construct<Settings>(pathSettings);
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

        void ShowUnhandledException(Exception e, string unhandledExceptionType) {
            new WindowException(e, unhandledExceptionType).Show(); //Removed: , Debugger.IsAttached
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            Kaseya.Start();

            string[] args = Environment.GetCommandLineArgs();
            if (args.Length > 1)
            {
                KLCCommand command = KLCCommand.NewFromBase64(args[1].Replace("liveconnect:///", ""));

                if (command.payload.navId == "remotecontrol/shared")
                    alternative = new WindowAlternative(command.payload.agentId, command.payload.auth.Token, true, Enums.RC.Shared);
                else if (command.payload.navId == "remotecontrol/private")
                    alternative = new WindowAlternative(command.payload.agentId, command.payload.auth.Token, true, Enums.RC.Private);
                else if (command.payload.navId == "remotecontrol/1-click")
                    alternative = new WindowAlternative(command.payload.agentId, command.payload.auth.Token, true, Enums.RC.OneClick);
                else
                    alternative = new WindowAlternative(command.payload.agentId, command.payload.auth.Token);

                alternative.Show();
            }
            else
            {
                new MainWindow().Show();
            }
        }

    }
}
