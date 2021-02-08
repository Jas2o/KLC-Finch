using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace KLC_Finch {
    /// <summary>
    /// Interaction logic for WindowAlternative.xaml
    /// </summary>
    public partial class WindowAlternative : Window {

        public KLC.LiveConnectSession session;
        private string agentID;
        private string shortToken;

        private System.Windows.Threading.DispatcherTimer timerDirect;
        private bool directHasLaunched;
        private bool directToPrivate;

        /*
        RemoteControl moduleRemoteControl;
        Dashboard moduleDashboard;
        CommandTerminal moduleCommand;
        CommandPowershell modulePowershell;
        FileExplorer moduleFileExplorer;
        RegistryEditor moduleRegistry;
        */

        public WindowAlternative() {
            InitializeComponent();
        }

        public WindowAlternative(string agentID, string shortToken, bool directToRemoteControl=false, bool directToPrivate = false) {
            InitializeComponent();

            if (agentID != null)
                this.agentID = agentID;
            if(shortToken != null)
                this.shortToken = shortToken;

            if (directToRemoteControl) {
                this.directToPrivate = directToPrivate;
                timerDirect = new System.Windows.Threading.DispatcherTimer();
                timerDirect.Tick += new EventHandler(dispatcherTimer_Tick);
                timerDirect.Interval = new TimeSpan(0, 0, 1);
                timerDirect.Start();
            }
        }

        private void dispatcherTimer_Tick(object sender, EventArgs e) {
            if (directHasLaunched) {
                if (!session.ModuleRemoteControl.Viewer.IsVisible)
                    Environment.Exit(0);
            } else {
                if (session != null && session.WebsocketB.ControlAgentIsReady()) {
                    directHasLaunched = true;
                    timerDirect.Interval = new TimeSpan(0, 0, 5);

                    session.ModuleRemoteControl = new RemoteControl(session, directToPrivate);
                    session.ModuleRemoteControl.Connect();

                    this.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            if (shortToken == null || agentID == null)
                return; //Dragablz

            session = new KLC.LiveConnectSession(shortToken, agentID);
            this.Title = session.agent.Name + " - KLC-Finch";

            if (session.agent.IsMac) {
                tabCommand.Header = "Terminal";
                tabPowershell.Visibility = Visibility.Collapsed;
                tabRegistry.Visibility = Visibility.Collapsed;
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e) {
            if (session == null)
                return; //Dragablz

            if (session.ModuleRemoteControl != null && session.ModuleRemoteControl.Viewer.IsVisible) {
                this.Visibility = Visibility.Collapsed;
                e.Cancel = true;
            } else {
                if (timerDirect != null)
                    timerDirect.Stop();
                session.Close();
            }
        }

        private void btnRCShared_Click(object sender, RoutedEventArgs e) {
            if (session == null)
                return;

            session.ModuleRemoteControl = new RemoteControl(session, false);
            session.ModuleRemoteControl.Connect();
        }

        private void btnRCPrivate_Click(object sender, RoutedEventArgs e) {
            if (session == null)
                return;

            session.ModuleRemoteControl = new RemoteControl(session, true);
            session.ModuleRemoteControl.Connect();
        }

        private void btnReconnect_Click(object sender, RoutedEventArgs e) {
            if (session != null && session.ModuleRemoteControl != null)
                session.ModuleRemoteControl.CloseViewer();

            App.alternative = new WindowAlternative(agentID, shortToken);
            App.alternative.Show();
            this.Close();
        }

        private void ctrlDashboard_Loaded(object sender, RoutedEventArgs e) {
            ctrlDashboard.btnStaticImageStart_Click(sender, e);
        }
    }
}
