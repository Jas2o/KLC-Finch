using LibKaseya;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace KLC_Finch {
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application {

        public static WindowViewer viewer;

        public App() : base() {
            if (!Debugger.IsAttached) {
                //Setup exception handling rather than closing rudely (this doesn't really work well).
                AppDomain.CurrentDomain.UnhandledException += (sender, args) => ShowUnhandledException(args.ExceptionObject as Exception, "AppDomain.CurrentDomain.UnhandledException");
                TaskScheduler.UnobservedTaskException += (sender, args) => ShowUnhandledException(args.Exception, "TaskScheduler.UnobservedTaskException");

                Dispatcher.UnhandledException += (sender, args) => {
                    args.Handled = true;
                    ShowUnhandledException(args.Exception, "Dispatcher.UnhandledException");
                };
            }
        }

        public static void ShowUnhandledExceptionFromSrc(Exception e, string source) {
            Application.Current.Dispatcher.Invoke((Action)delegate {
                new WindowException(e, source + " - " + e.GetType().ToString()).Show();
            });
        }

        void ShowUnhandledException(Exception e, string unhandledExceptionType) {
            new WindowException(e, unhandledExceptionType).Show(); //Removed: , Debugger.IsAttached
        }

        private void Application_Startup(object sender, StartupEventArgs e) {
			Kaseya.Start();

            string[] args = Environment.GetCommandLineArgs();
            if (args.Length > 1) {
                KLCCommand command = KLCCommand.NewFromBase64(args[1].Replace("kaseyaliveconnect:///", ""));

                if(command.payload.navId == "remotecontrol/shared") {
                    new WindowAlternative(command.payload.agentId, command.payload.auth.Token, true, false).Show();
                } else if(command.payload.navId == "remotecontrol/private") {
                    new WindowAlternative(command.payload.agentId, command.payload.auth.Token, true, true).Show();
                } else {
                    new WindowAlternative(command.payload.agentId, command.payload.auth.Token).Show();
                }
            } else {
                new MainWindow().Show();
            }
		}

	}
}
