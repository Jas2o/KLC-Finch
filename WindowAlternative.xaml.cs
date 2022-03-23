using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using static LibKaseya.Enums;

namespace KLC_Finch
{
    /// <summary>
    /// Interaction logic for WindowAlternative.xaml
    /// </summary>
    public partial class WindowAlternative : Window
    {

        public KLC.LiveConnectSession session;
        public bool socketActive { get; private set; }
        private string agentID;
        private string shortToken;

        private RC directToMode;
        private bool dashLoaded;

        /*
        RemoteControl moduleRemoteControl;
        Dashboard moduleDashboard;
        CommandTerminal moduleCommand;
        CommandPowershell modulePowershell;
        FileExplorer moduleFileExplorer;
        RegistryEditor moduleRegistry;
        */

        public WindowAlternative()
        {
            InitializeComponent();
        }

        public WindowAlternative(string agentID, string shortToken, bool directToRemoteControl = false, RC directToMode = RC.Shared)
        {
            InitializeComponent();

            if (agentID == null || shortToken == null)
                return;

            this.agentID = agentID;
            this.shortToken = shortToken;
            this.directToMode = directToMode;
            socketActive = true;

            //--

            int vcRuntimeBld = 0;
            try
            {
                using (RegistryKey view32 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
                {
                    RegistryKey subkey = view32.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\X86"); //Actually in WOW6432Node
                    if (subkey != null)
                        vcRuntimeBld = (int)subkey.GetValue("Bld");
                    subkey.Close();
                }
            }
            catch (Exception)
            {
            }
            if (vcRuntimeBld < 23026)
            { //2015
                directToRemoteControl = false;
                new WindowException("Visual C++ Redistributable (x86) is not 2015 or above. You can download from:\r\n\r\nhttps://support.microsoft.com/en-us/topic/the-latest-supported-visual-c-downloads-2647da03-1eea-4433-9aff-95f26a218cc0", "Dependency check").ShowDialog();
            }

            //--

            if (App.Settings.AltModulesStartAuto)
            {
                for (int i = 1; i < tabControl.Items.Count; i++)
                {
                    ((TabItem)tabControl.Items[i]).IsEnabled = false;
                }
            }

            HasConnected callback = (directToRemoteControl ? new HasConnected(ConnectDirect) : new HasConnected(ConnectNotDirect));
            session = new KLC.LiveConnectSession(shortToken, agentID, callback);
            if (session.Eirc == null)
            {
                session = null;
                return;
            }
            this.Title = session.agent.Name + " - KLC-Finch";
            btnRCOneClick.IsEnabled = session.agent.OneClickAccess;

            WindowUtilities.ActivateWindow(this);
        }

        public void Disconnect(string sessionGuid, int reason)
        {
            if (session.RandSessionGuid != sessionGuid)
                return;

            socketActive = false;

            Application.Current.Dispatcher.Invoke((Action)delegate {
                switch (reason)
                {
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
        public void ConnectDirect()
        {
            session.ModuleRemoteControl = new RemoteControl(session, directToMode);
            Application.Current.Dispatcher.Invoke((Action)delegate {
                ConnectUpdateUI();
                session.ModuleRemoteControl.Connect();

                this.Visibility = Visibility.Collapsed;
            });
        }
        public void ConnectNotDirect()
        {
            Application.Current.Dispatcher.Invoke((Action)delegate {
                ConnectUpdateUI();
            });
        }
        private void ConnectUpdateUI()
        {
            if (App.Settings.AltModulesStartAuto)
            {
                for (int i = 1; i < tabControl.Items.Count; i++)
                {
                    ((TabItem)tabControl.Items[i]).IsEnabled = true;
                }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            txtVersion.Text = App.Version;

            if (shortToken == null || agentID == null || session == null)
                return; //Dragablz

            if (session.agent.OSTypeProfile == LibKaseya.Agent.OSProfile.Mac)
            {
                tabCommand.Header = "Terminal";
                ctrlCommand.chkAllowColour.IsChecked = true;

                tabPowershell.Visibility = Visibility.Collapsed;
                tabRegistry.Visibility = Visibility.Collapsed;
                tabEvents.Visibility = Visibility.Collapsed;
            }
            else
            {
                ctrlCommand.btnCommandMacKillKRCH.Visibility = Visibility.Collapsed;
                ctrlCommand.btnCommandMacReleaseFn.Visibility = Visibility.Collapsed;
            }

            if (!System.IO.File.Exists(@"C:\Program Files\Wireshark\Wireshark.exe"))
                btnWiresharkFilter.Visibility = Visibility.Collapsed;
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (session == null)
                return; //Dragablz

            if (session.ModuleRemoteControl != null && session.ModuleRemoteControl.Viewer != null && session.ModuleRemoteControl.Viewer.IsVisible)
            {
                this.Visibility = Visibility.Collapsed;
                e.Cancel = true;
            }
            else
            {
                session.Close();
            }
        }

        private void btnRCShared_Click(object sender, RoutedEventArgs e)
        {
            if (session == null || session.WebsocketB == null || !session.WebsocketB.ControlAgentIsReady())
                return;
            if (session.ModuleRemoteControl != null)
                session.ModuleRemoteControl.CloseViewer();

            session.ModuleRemoteControl = new RemoteControl(session, RC.Shared);
            session.ModuleRemoteControl.Connect();
        }

        private void btnRCPrivate_Click(object sender, RoutedEventArgs e)
        {
            if (session == null || session.WebsocketB == null || !session.WebsocketB.ControlAgentIsReady())
                return;
            if (session.ModuleRemoteControl != null)
                session.ModuleRemoteControl.CloseViewer();

            session.ModuleRemoteControl = new RemoteControl(session, RC.Private);
            session.ModuleRemoteControl.Connect();
        }

        private void btnRCOneClick_Click(object sender, RoutedEventArgs e)
        {
            if (session == null || session.WebsocketB == null || !session.WebsocketB.ControlAgentIsReady())
                return;
            if (session.ModuleRemoteControl != null)
                session.ModuleRemoteControl.CloseViewer();

            session.ModuleRemoteControl = new RemoteControl(session, RC.OneClick);
            session.ModuleRemoteControl.Connect();
        }

        private void btnReconnect_Click(object sender, RoutedEventArgs e)
        {
            if (session != null && session.ModuleRemoteControl != null)
                session.ModuleRemoteControl.CloseViewer();

            WindowState tempState = this.WindowState;
            this.WindowState = WindowState.Normal;
            int tempLeft = (int)this.Left;
            int tempTop = (int)this.Top;
            int tempWidth = (int)this.Width;
            int tempHeight = (int)this.Height;

            App.alternative = new WindowAlternative(agentID, shortToken);
            App.alternative.Show();
            App.alternative.Left = tempLeft;
            App.alternative.Top = tempTop;
            App.alternative.Width = tempWidth;
            App.alternative.Height = tempHeight;
            App.alternative.WindowState = tempState;

            this.Close();
        }

        private void ctrlDashboard_Loaded(object sender, RoutedEventArgs e)
        {
            if (session == null || dashLoaded)
                return;
            dashLoaded = true;

            if (session.agent.RebootLast == default(DateTime))
            {
                ctrlDashboard.txtRebootLast.Text = "Last reboot unknown";
            }
            else
            {
                ctrlDashboard.txtRebootLast.Text = "Last rebooted ~" + KLC.Util.FuzzyTimeAgo(session.agent.RebootLast);
                ctrlDashboard.txtRebootLast.ToolTip = session.agent.RebootLast.ToString();
            }

            ctrlDashboard.UpdateDisplayData();
            //ctrlDashboard.DisplayRAM(session.agent.RAMinGB);
            //ctrlDashboard.DisplayRCNotify(session.RCNotify);
            //ctrlDashboard.DisplayMachineNote(session.agent.MachineShowToolTip, session.agent.MachineNote, session.agent.MachineNoteLink);

            if (App.Settings.AltModulesStartAuto)
            {
                if (session.agent.OSTypeProfile != LibKaseya.Agent.OSProfile.Mac || App.Settings.AltModulesStartAutoMacStaticImage)
                    ctrlDashboard.btnStaticImageStart_Click(sender, e);
                
                ctrlDashboard.btnDashboardStartData_Click(sender, e);
            }
        }

        private void btnWiresharkFilter_Click(object sender, RoutedEventArgs e)
        {
            if (session == null)
                return;

            string filter = session.GetWiresharkFilter();
            if (filter != "")
                Clipboard.SetDataObject(filter);
            //Clipboard.SetText(filter); //Apparently WPF clipboard has issues
        }

        private void btnLaunchKLC_Click(object sender, RoutedEventArgs e)
        {
            if (session == null)
                return;

            LibKaseya.KLCCommand command = LibKaseya.KLCCommand.Example(agentID, shortToken);
            command.SetForLiveConnect();
            command.Launch(false, LibKaseya.LaunchExtra.None);
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5)
            {
                if (tabFiles.IsSelected)
                    ctrlFiles.btnFilesPathJump_Click(sender, null);
                else if (tabRegistry.IsSelected)
                    ctrlRegistry.BtnRegistryPathJump_Click(sender, null);
                else if (tabEvents.IsSelected)
                    ctrlEvents.btnEventsRefresh_Click(sender, null);
                else if (tabServices.IsSelected)
                    ctrlServices.btnServicesRefresh_Click(sender, null);
                else if (tabProcesses.IsSelected)
                    ctrlProcesses.btnProcessesRefresh_Click(sender, null);
            }
        }

        private void btnAltSettings_Click(object sender, RoutedEventArgs e)
        {
            WindowOptions winOptions = new WindowOptions(ref App.Settings, false)
            {
                Owner = this
            };
            winOptions.ShowDialog();
        }

        private void btnRCLogs_Click(object sender, RoutedEventArgs e)
        {
            //Copied from WindowViewerV3
            string logs = App.alternative.session.agent.GetAgentRemoteControlLogs();
            MessageBox.Show(logs, "KLC-Finch: Remote Control Logs");
        }
    }
}
