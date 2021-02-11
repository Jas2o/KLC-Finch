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

            if (agentID == null || shortToken == null)
                return;

            this.agentID = agentID;
            this.shortToken = shortToken;
            this.directToPrivate = directToPrivate;

            HasConnected callback = (directToRemoteControl ? new HasConnected(ConnectDirect) : null);
            session = new KLC.LiveConnectSession(shortToken, agentID, callback);
            this.Title = session.agent.Name + " - KLC-Finch";
        }

        public delegate void HasConnected();
        public void ConnectDirect() {
            session.ModuleRemoteControl = new RemoteControl(session, directToPrivate);
            Application.Current.Dispatcher.Invoke((Action)delegate {
                session.ModuleRemoteControl.Connect();

                this.Visibility = Visibility.Collapsed;
            });
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            if (shortToken == null || agentID == null)
                return; //Dragablz

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
