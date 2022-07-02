using LibKaseya;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using nucs.JsonSettings;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace KLC_Finch {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {

        private string thisAgentID;
        private WindowRCTest winRCTest;

        public MainWindow() {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            txtVersion.Text = "Build date: " + App.Version;

            string savedAuthToken = KaseyaAuth.GetStoredAuth();
            if (savedAuthToken != null)
                txtAuthToken.Password = savedAuthToken;

            if (!File.Exists(@"C:\Program Files\Kaseya Live Connect-MITM\KaseyaLiveConnect.exe") && !File.Exists(Environment.ExpandEnvironmentVariables(@"%localappdata%\Apps\Kaseya Live Connect-MITM\KaseyaLiveConnect.exe")))
                chkUseMITM.Visibility = Visibility.Collapsed;

            foreach (Bookmark bm in Bookmarks.List)
            {
                if(bm.Type == "Agent")
                    cmbBookmarks.Items.Add(bm);
            }

            #region This Agent ID
            try
            {
                using (RegistryKey view32 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
                {
                    RegistryKey subkey = view32.OpenSubKey(@"SOFTWARE\Kaseya\Agent\AGENT11111111111111"); //Actually in WOW6432Node
                    if (subkey != null)
                        thisAgentID = subkey.GetValue("AgentGUID").ToString();
                    subkey.Close();
                }
            }
            catch (Exception)
            {
            }
            #endregion
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            Environment.Exit(0);
        }

        private void BtnAgentGuidConnect_Click(object sender, RoutedEventArgs e) {
            if (txtAgentGuid.Text.Trim().Length > 0) {
                App.alternative = new WindowAlternative(txtAgentGuid.Text.Trim(), txtAuthToken.Password);
                App.alternative.Show();
            }
        }

        private void BtnLaunchThisComputer_Click(object sender, RoutedEventArgs e)
        {
            if (thisAgentID != null)
            {
                App.alternative = new WindowAlternative(thisAgentID, txtAuthToken.Password);
                App.alternative.Show();
            }
        }

        private void BtnLaunchThisComputerShared_Click(object sender, RoutedEventArgs e)
        {
            if (thisAgentID != null)
            {
                App.alternative = new WindowAlternative(thisAgentID, txtAuthToken.Password, Enums.OnConnect.OnlyRC, Enums.RC.Shared);
                App.alternative.Show();
            }
        }

        private void BtnLaunchRCTest_Click(object sender, RoutedEventArgs e)
        {
            if (winRCTest != null)
                winRCTest.Close();
            winRCTest = new WindowRCTest();
            winRCTest.Show();
        }

        private void BtnLaunchNull_Click(object sender, RoutedEventArgs e)
        {
            App.alternative = new WindowAlternative(null, null);
            App.alternative.Show();
        }

        /*
        private void BtnLaunchWinTeamviewer_Click(object sender, RoutedEventArgs e) {
            App.alternative = new WindowAlternative("111111111111111", txtAuthToken.Password);
            App.alternative.Show();
        }

        private void BtnLaunchWinTeamviewerShared_Click(object sender, RoutedEventArgs e) {
            App.alternative = new WindowAlternative("111111111111111", txtAuthToken.Password, true, Enums.RC.Shared);
            App.alternative.Show();
        }

        private void BtnLaunchMacMini_Click(object sender, RoutedEventArgs e) {
            App.alternative = new WindowAlternative("428588645237770", txtAuthToken.Password);
            App.alternative.Show();
        }
        */

        private void ChkUseMITM_Change(object sender, RoutedEventArgs e) {
            KLC.WsA.useInternalMITM = (bool)chkUseMITM.IsChecked;
        }

        private void BtnRCSettings_Click(object sender, RoutedEventArgs e) {
            WindowOptions winOptions = new WindowOptions(ref App.Settings, true) {
                Owner = this
            };
            winOptions.ShowDialog();
        }

        private void cmbBookmarks_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            Bookmark selected = (Bookmark)cmbBookmarks.SelectedItem;
            if (selected == null)
                return;

            if (Keyboard.IsKeyDown(Key.LeftShift))
                App.alternative = new WindowAlternative(selected.Value, txtAuthToken.Password, Enums.OnConnect.OnlyRC, Enums.RC.Shared);
            else
                App.alternative = new WindowAlternative(selected.Value, txtAuthToken.Password);
            App.alternative.Show();
        }
    }
}
