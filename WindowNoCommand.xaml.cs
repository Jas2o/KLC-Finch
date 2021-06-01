using LibKaseya;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
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
        public MainWindow() {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            txtVersion.Text = "Build date: " + App.Version;

            string savedAuthToken = KaseyaAuth.GetStoredAuth();
            if (savedAuthToken != null)
                txtAuthToken.Password = savedAuthToken;

            if (!File.Exists(@"C:\Program Files\Kaseya Live Connect-MITM\KaseyaLiveConnect.exe"))
                chkUseMITM.Visibility = Visibility.Collapsed;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            Environment.Exit(0);
        }

        private void btnAgentGuidConnect_Click(object sender, RoutedEventArgs e) {
            if (txtAgentGuid.Text.Trim().Length > 0) {
                App.alternative = new WindowAlternative(txtAgentGuid.Text.Trim(), txtAuthToken.Password);
                App.alternative.Show();
            }
        }

        private void btnLaunchWinTeamviewer_Click(object sender, RoutedEventArgs e) {
            App.alternative = new WindowAlternative("111111111111111", txtAuthToken.Password);
            App.alternative.Show();
        }

        private void btnLaunchWinTeamviewerShared_Click(object sender, RoutedEventArgs e) {
            App.alternative = new WindowAlternative("111111111111111", txtAuthToken.Password, true, false);
            App.alternative.Show();
        }

        private void btnLaunchMacMini_Click(object sender, RoutedEventArgs e) {
            App.alternative = new WindowAlternative("718548734128395", txtAuthToken.Password);
            App.alternative.Show();
        }

        private void btnLaunchRCTest_Click(object sender, RoutedEventArgs e) {
            int width = 800;
            int height = 1080;

            if (App.viewer != null) {
                App.viewer.Close();
                App.viewer = null;
            }
            WindowViewerV2 myViewer = App.viewer = new WindowViewerV2(null, width, height);

            JObject jScreen = new JObject();
            jScreen["screen_id"] = 65539;
            jScreen["screen_name"] = "Test Screen";
            jScreen["screen_width"] = width;
            jScreen["screen_height"] = height;
            jScreen["screen_x"] = 0;
            jScreen["screen_y"] = 0;
            JArray jScreenArray = new JArray();
            jScreenArray.Add(jScreen);
            JObject jDesktop = new JObject();
            jDesktop["default_screen"] = 65539;
            jDesktop["screens"] = jScreenArray;

            myViewer.UpdateScreenLayout(jDesktop);
            //myViewer.AddScreen("0", "Test Screen", height, width, 0, 0, true);
            //myViewer.SetCanvas(0, 0, width, height);
            myViewer.Show();

            Thread threadTest = new Thread(() => {
                System.Drawing.Color[] colors = new System.Drawing.Color[] { //The BIT.TRIP colours!
                    System.Drawing.Color.FromArgb(251, 218, 3), //Yellow
                    System.Drawing.Color.FromArgb(255, 165, 50), //Orange
                    System.Drawing.Color.FromArgb(53, 166, 170), //Teal
                    System.Drawing.Color.FromArgb(220, 108, 167), //Pink
                    System.Drawing.Color.FromArgb(57, 54, 122) //Purple
                };
                Bitmap bTest = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                while (myViewer.IsVisible) {
                    foreach (System.Drawing.Color c in colors) {
                        using (Graphics g = Graphics.FromImage(bTest)) { g.Clear(c); }
                        myViewer.LoadTexture(bTest.Width, bTest.Height, bTest);

                        Thread.Sleep(1500);
                    }
                }
            });
            threadTest.Start();
        }

        private void btnLaunchNull_Click(object sender, RoutedEventArgs e) {
            App.alternative = new WindowAlternative(null, null);
            App.alternative.Show();
        }

        private void btnLaunchThisComputer_Click(object sender, RoutedEventArgs e) {
            string val = "";

            using (RegistryKey view32 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32)) {
                RegistryKey subkey = view32.OpenSubKey(@"SOFTWARE\Kaseya\Agent\AGENT11111111111111"); //Actually in WOW6432Node
                if (subkey != null)
                    val = subkey.GetValue("AgentGUID").ToString();
                subkey.Close();
            }

            if (val.Length > 0) {
                App.alternative = new WindowAlternative(val, txtAuthToken.Password);
                App.alternative.Show();
            }
        }

        private void chkUseMITM_Change(object sender, RoutedEventArgs e) {
            KLC.WsA.useInternalMITM = (bool)chkUseMITM.IsChecked;
        }
    }
}
