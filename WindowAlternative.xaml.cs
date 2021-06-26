﻿using System;
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
        public bool socketActive { get; private set; }
        private string agentID;
        private string shortToken;

        private bool directToPrivate;
        private bool dashLoaded;

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
            socketActive = true;

            HasConnected callback = (directToRemoteControl ? new HasConnected(ConnectDirect) : null);
            session = new KLC.LiveConnectSession(shortToken, agentID, callback);
            this.Title = session.agent.Name + " - KLC-Finch";

            WindowUtilities.ActivateWindow(this);
        }

        public void Disconnect(string sessionGuid, int reason) {
            if (session.randSessionGuid != sessionGuid)
                return;

            socketActive = false;

            Application.Current.Dispatcher.Invoke((Action)delegate {
                switch(reason) {
                    case 0:
                        txtDisconnected.Text = "Endpoint Unavailable (Web Socket A)";
                        borderDisconnected.Background = new SolidColorBrush(Colors.DarkOrange);
                        break;

                    case 1:
                        txtDisconnected.Text = "Endpoint Disconnected (Web Socket B)";
                        borderDisconnected.Background = new SolidColorBrush(Colors.Maroon);
                        break;
                }
                
                borderDisconnected.Visibility = Visibility.Visible;
            });
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
            txtVersion.Text = App.Version;

            if (shortToken == null || agentID == null)
                return; //Dragablz

            if (session.agent.IsMac) {
                tabCommand.Header = "Terminal";
                tabPowershell.Visibility = Visibility.Collapsed;
                tabRegistry.Visibility = Visibility.Collapsed;
                ctrlCommand.chkAllowColour.IsChecked = true;
            } else {
                ctrlCommand.btnCommandMacKillKRCH.Visibility = Visibility.Collapsed;
            }

            if (!System.IO.File.Exists(@"C:\Program Files\Wireshark\Wireshark.exe"))
                btnWiresharkFilter.Visibility = Visibility.Collapsed;
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
            if (session == null || session.WebsocketB == null || !session.WebsocketB.ControlAgentIsReady())
                return;

            session.ModuleRemoteControl = new RemoteControl(session, false);
            session.ModuleRemoteControl.Connect();
        }

        private void btnRCPrivate_Click(object sender, RoutedEventArgs e) {
            if (session == null || session.WebsocketB == null || !session.WebsocketB.ControlAgentIsReady())
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
            if (session == null || dashLoaded)
                return;
            dashLoaded = true;

            if (session.agent.RebootLast == default(DateTime)) {
                ctrlDashboard.txtRebootLast.Text = "Last reboot unknown";
            } else {
                ctrlDashboard.txtRebootLast.Text = "Last rebooted ~" + KLC.Util.FuzzyTimeAgo(session.agent.RebootLast);
                ctrlDashboard.txtRebootLast.ToolTip = session.agent.RebootLast.ToString();
            }

            ctrlDashboard.UpdateDisplayData();
            //ctrlDashboard.DisplayRAM(session.agent.RAMinGB);
            //ctrlDashboard.DisplayRCNotify(session.RCNotify);
            //ctrlDashboard.DisplayMachineNote(session.agent.MachineShowToolTip, session.agent.MachineNote, session.agent.MachineNoteLink);
            ctrlDashboard.btnStaticImageStart_Click(sender, e);
            ctrlDashboard.btnDashboardStartData_Click(sender, e);
        }

        private void btnWiresharkFilter_Click(object sender, RoutedEventArgs e) {
            if (session == null)
                return;

            string filter = session.GetWiresharkFilter();
            if (filter != "")
                Clipboard.SetDataObject(filter);
            //Clipboard.SetText(filter); //Apparently WPF clipboard has issues
        }

        private void btnLaunchKLC_Click(object sender, RoutedEventArgs e) {
            if (session == null)
                return;

            LibKaseya.KLCCommand command = LibKaseya.KLCCommand.Example(agentID, shortToken);
            command.SetForLiveConnect();
            command.Launch(false, LibKaseya.LaunchExtra.None);
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e) {
            if(e.Key == Key.F5) {
                if (tabFiles.IsSelected)
                    ctrlFiles.btnFilesPathJump_Click(sender, null);
                else if (tabRegistry.IsSelected)
                    ctrlRegistry.btnRegistryPathJump_Click(sender, null);
                else if (tabEvents.IsSelected)
                    ctrlEvents.btnEventsRefresh_Click(sender, null);
                else if (tabServices.IsSelected)
                    ctrlServices.btnServicesRefresh_Click(sender, null);
                else if (tabProcesses.IsSelected)
                    ctrlProcesses.btnProcessesRefresh_Click(sender, null);
            }
        }
    }
}
