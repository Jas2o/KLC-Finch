using Microsoft.Win32;
using Ookii.Dialogs.Wpf;
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
        private static SolidColorBrush brushBlue1 = new SolidColorBrush(Colors.DeepSkyBlue);
        private static SolidColorBrush brushBlue2 = new SolidColorBrush(Colors.DodgerBlue);

        public KLC.LiveConnectSession session;
        public bool socketActive { get; private set; }
        private string vsa;
        private string agentID;
        private string shortToken;

        private OnConnect directAction;
        private RC directToMode;
        private bool dashLoaded;
        private uint connectionAttempt;

        public WindowAlternative()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Standalone
        /// </summary>
        /// <param name="agentID"></param>
        /// <param name="vsa"></param>
        /// <param name="shortToken"></param>
        /// <param name="directToRemoteControl"></param>
        /// <param name="directToMode"></param>
        public WindowAlternative(string agentID, string vsa, string shortToken, OnConnect directAction = OnConnect.NoAction, RC directToMode = RC.Shared)
        {
            InitializeComponent();

            if (agentID == null || vsa == null || shortToken == null)
                return;

            this.vsa = vsa;
            this.agentID = agentID;
            this.shortToken = shortToken;
            this.directAction = directAction;
            this.directToMode = directToMode;
            socketActive = true;
            connectionAttempt = 0;

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
                this.directAction = OnConnect.NoAction;
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

            ProgressDialog dialog = new ProgressDialog();
            dialog.WindowTitle = "KLC-Finch";
            dialog.Description = "Waiting for VSA...";
            dialog.ShowCancelButton = false;
            dialog.ProgressBarStyle = ProgressBarStyle.MarqueeProgressBar;
            dialog.Show();

            //HasConnected callback = (directToRemoteControl ? new HasConnected(ConnectDirect) : new HasConnected(ConnectNotDirect));
            StatusCallback callback = new StatusCallback(StatusUpdate);
            session = new KLC.LiveConnectSession(vsa, shortToken, agentID, callback);
            dialog.Dispose();
            if (session.Eirc == null)
            {
                session = null;
                return;
            }
            this.Title = session.agent.Name + " - " + vsa + " - KLC-Finch";
            btnRCOneClick.IsEnabled = session.agent.OneClickAccess;

            WindowUtilities.ActivateWindow(this);
        }

        public void Disconnect(string sessionGuid, int reason)
        {
            if (session.RandSessionGuid != sessionGuid)
                return;

            socketActive = false;

            /*
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
                        
                    case 2:
                        txtDisconnected.Text = "Manual Disconnection";
                        borderDisconnected.Background = new SolidColorBrush(Colors.DimGray);
                        break;
                }

                borderDisconnected.Visibility = Visibility.Visible;
            });
            */
        }

        public delegate void StatusCallback(EPStatus status);
        public void StatusUpdate(EPStatus status)
        {
            if (status == EPStatus.Connected && directAction != OnConnect.NoAction)
            {
                if (directToMode == RC.NativeRDP || directToMode == RC.Private || directToMode == RC.OneClick)
                {
                    session.WebsocketB.ControlAgentSendRDP_StateRequest(directToMode);
                    return;
                }
                else
                    session.ModuleRemoteControl = new RemoteControl(session, directToMode);
            }

            Application.Current.Dispatcher.Invoke((Action)delegate
            {
                switch (status)
                {
                    case EPStatus.AttemptingToConnect:
                    case EPStatus.PeerOffline:
                    case EPStatus.PeerToPeerFailure:
                        connectionAttempt++;
                        if (directAction != OnConnect.NoAction)
                            txtStatus.Text = "Attempt " + connectionAttempt + " to connect and open Remote Control... ";
                        else
                            txtStatus.Text = "Attempt " + connectionAttempt + " to connect...";

                        if (connectionAttempt % 2 == 0)
                            borderStatus.Background = brushBlue1;
                        else
                            borderStatus.Background = brushBlue2;
                        borderStatus.Visibility = Visibility.Visible;

                        if (connectionAttempt > 99)
                            session.Close();

                        break;
                    case EPStatus.Connected:
                        txtStatus.Text = "Connected";
                        borderStatus.Background = new SolidColorBrush(Colors.Green);
                        borderStatus.Visibility = Visibility.Collapsed;
                        ConnectUpdateUI();
                        if (directAction != OnConnect.NoAction)
                            session.ModuleRemoteControl.Connect();
                        if (directAction == OnConnect.OnlyRC)
                            this.Visibility = Visibility.Collapsed;
                        break;

                    case EPStatus.UnavailableWsA:
                        txtStatus.Text = "Endpoint Unavailable (Web Socket A)";
                        borderStatus.Background = new SolidColorBrush(Colors.DarkOrange);
                        borderStatus.Visibility = Visibility.Visible;
                        break;

                    case EPStatus.DisconnectedWsB:
                        txtStatus.Text = "Endpoint Disconnected (Web Socket B)";
                        borderStatus.Background = new SolidColorBrush(Colors.Maroon);
                        borderStatus.Visibility = Visibility.Visible;
                        break;

                    case EPStatus.DisconnectedManual:
                        txtStatus.Text = "Manual Disconnection";
                        borderStatus.Background = new SolidColorBrush(Colors.DimGray);
                        borderStatus.Visibility = Visibility.Visible;
                        break;

                    case EPStatus.UnableToStartSession:
                        txtStatus.Text = "Unable to start session with endpoint.";
                        borderStatus.Background = new SolidColorBrush(Colors.DimGray);
                        borderStatus.Visibility = Visibility.Visible;
                        break;

                    case EPStatus.AuthFailed:
                        txtStatus.Text = "Authentication failure or cannot communicate with VSA.";
                        borderStatus.Background = new SolidColorBrush(Colors.DimGray);
                        borderStatus.Visibility = Visibility.Visible;
                        break;

                    case EPStatus.NativeRDPStarting:
                        txtStatus.Text = "Native RDP - Starting TCP Tunneling";
                        borderStatus.Background = new SolidColorBrush(Colors.Green);
                        borderStatus.Visibility = Visibility.Visible;
                        break;
                    case EPStatus.NativeRDPActive:
                        txtStatus.Text = "Native RDP - TCP Tunneling Active";
                        borderStatus.Background = new SolidColorBrush(Colors.Green);
                        borderStatus.Visibility = Visibility.Visible;
                        break;
                    case EPStatus.NativeRDPEnded:
                        txtStatus.Text = "Native RDP - Ended";
                        borderStatus.Background = new SolidColorBrush(Colors.Green);
                        borderStatus.Visibility = Visibility.Collapsed;
                        break;

                    default:
                        Console.WriteLine("Status unknown: " + status);
                        txtStatus.Text = "Status unknown: " + status;
                        borderStatus.Background = new SolidColorBrush(Colors.Magenta);
                        borderStatus.Visibility = Visibility.Visible;
                        break;
                }
            });

            if (status == EPStatus.Connected && directAction != OnConnect.NoAction)
                directAction = OnConnect.NoAction;
        }

        /*
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
        */

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

            if (!App.Settings.AltShowAlphaTab)
                tabAlpha.Visibility = Visibility.Collapsed;

            if (session.agent.OSTypeProfile == LibKaseya.Agent.OSProfile.Mac)
            {
                btnRCNativeRDP.Visibility = Visibility.Collapsed;

                tabCommand.Header = "Terminal";
                ctrlCommand.chkAllowColour.IsChecked = true;

                tabPowershell.Visibility = Visibility.Collapsed;
                tabRegistry.Visibility = Visibility.Collapsed;
                tabEvents.Visibility = Visibility.Collapsed;
                tabServices.Visibility = Visibility.Collapsed;
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

            if (session.ModuleForwarding != null && session.ModuleForwarding.IsRunning())
            {
                using (TaskDialog dialog = new TaskDialog())
                {
                    dialog.WindowTitle = "KLC-Finch";
                    dialog.MainInstruction = "Native RDP is still running";
                    dialog.MainIcon = TaskDialogIcon.Information;
                    dialog.CenterParent = true;
                    dialog.Content = "Are you sure you want to close KLC-Finch and end Native RDP?";

                    TaskDialogButton tdbYes = new TaskDialogButton(ButtonType.Yes);
                    TaskDialogButton tdbCancel = new TaskDialogButton(ButtonType.Cancel);
                    dialog.Buttons.Add(tdbYes);
                    dialog.Buttons.Add(tdbCancel);

                    TaskDialogButton button = dialog.ShowDialog(App.winStandalone);
                    if (button == tdbYes)
                    {
                        session.ModuleForwarding.Close();
                        session.Close();
                    }
                    else
                    {
                        e.Cancel = true;
                    }
                }
            }
            else if (session.ModuleRemoteControl != null && session.ModuleRemoteControl.Viewer != null && session.ModuleRemoteControl.Viewer.IsVisible)
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
            {
                directAction = OnConnect.AlsoRC;
                directToMode = RC.Shared;
                return;
            }
            if (session.ModuleRemoteControl != null)
                session.ModuleRemoteControl.CloseViewer();

            session.ModuleRemoteControl = new RemoteControl(session, RC.Shared);
            session.ModuleRemoteControl.Connect();
        }

        private void btnRCPrivate_Click(object sender, RoutedEventArgs e)
        {
            if (session == null || session.WebsocketB == null || !session.WebsocketB.ControlAgentIsReady())
            {
                directAction = OnConnect.NoAction;
                return;
            }
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

            session.WebsocketB.ControlAgentSendRDP_StateRequest(RC.OneClick);

            session.ModuleRemoteControl = new RemoteControl(session, RC.OneClick);
            session.ModuleRemoteControl.Connect();
        }

        private void btnRCNativeRDP_Click(object sender, RoutedEventArgs e)
        {
            if (session != null && session.WebsocketB.ControlAgentIsReady())
            {
                session.WebsocketB.ControlAgentSendRDP_StateRequest(RC.NativeRDP);
            }
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

            App.winStandalone = new WindowAlternative(agentID, vsa, shortToken);
            App.winStandalone.Show();
            App.winStandalone.Left = tempLeft;
            App.winStandalone.Top = tempTop;
            App.winStandalone.Width = tempWidth;
            App.winStandalone.Height = tempHeight;
            App.winStandalone.WindowState = tempState;

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

            LibKaseya.KLCCommand command = LibKaseya.KLCCommand.Example(vsa, agentID, shortToken);
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
            ctrlDashboard.UpdateTimer();
        }

        private void btnRCLogs_Click(object sender, RoutedEventArgs e)
        {
            if (session == null)
                return;

            string logs = App.winStandalone.session.agent.GetAgentRemoteControlLogs();
            MessageBox.Show(logs, "KLC-Finch: Remote Control Logs");
        }

    }
}
