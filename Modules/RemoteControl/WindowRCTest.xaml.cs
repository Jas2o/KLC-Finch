using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
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
using System.Windows.Shapes;

namespace KLC_Finch {
    /// <summary>
    /// Interaction logic for WindowRCTest.xaml
    /// </summary>
    public partial class WindowRCTest : Window {

        private readonly int width = 800;
        private readonly int height = 1080;

        private const string exampleDefault = @"{""default_screen"":65539,""screens"":[{""screen_id"":65539,""screen_name"":""Test Screen"",""screen_width"":800,""screen_height"":1080,""screen_x"":0,""screen_y"":0}]}";

        private const string example1 = @"{""default_screen"":131073,""screens"":[{""screen_height"":2160,""screen_id"":131073,""screen_name"":""\\\\.\\DISPLAY4"",""screen_width"":3840,""screen_x"":1920,""screen_y"":-698},{""screen_height"":1080,""screen_id"":196706,""screen_name"":""\\\\.\\DISPLAY5"",""screen_width"":1920,""screen_x"":0,""screen_y"":0}]}";

        private const string example2 = @"{""default_screen"":65539,""screens"":[{""screen_height"":1080,""screen_id"":65539,""screen_name"":""\\\\.\\DISPLAY1"",""screen_width"":1920,""screen_x"":1920,""screen_y"":0},{""screen_height"":1080,""screen_id"":65537,""screen_name"":""\\\\.\\DISPLAY3"",""screen_width"":1920,""screen_x"":0,""screen_y"":0},{""screen_height"":1080,""screen_id"":18446744073389015000,""screen_name"":""\\\\.\\DISPLAY2"",""screen_width"":1920,""screen_x"":-1920,""screen_y"":0},{""screen_height"":1080,""screen_id"":349242001,""screen_name"":""\\\\.\\DISPLAY4"",""screen_width"":1920,""screen_x"":-3840,""screen_y"":0}]}";

        private const string example3 = @"{""default_screen"":131073,""screens"":[{""screen_height"":900,""screen_id"":131073,""screen_name"":""\\\\.\\DISPLAY1"",""screen_width"":1600,""screen_x"":0,""screen_y"":0},{""screen_height"":1080,""screen_id"":1245327,""screen_name"":""\\\\.\\DISPLAY2"",""screen_width"":1920,""screen_x"":1615,""screen_y"":-741},{""screen_height"":1080,""screen_id"":196759,""screen_name"":""\\\\.\\DISPLAY3"",""screen_width"":1920,""screen_x"":-305,""screen_y"":-1080}]}";

        private readonly RemoteControlTest rcTest;
        private WindowViewerV3 myViewer;

        public WindowRCTest() {
            InitializeComponent();
            txtInputJson.Text = exampleDefault;
            cmbRenderer.SelectedIndex = App.Settings.Renderer;

            if (App.viewer != null) {
                App.viewer.Close();
                App.viewer = null;
            }

            rcTest = new RemoteControlTest();
        }

        private void BtnTemplateDefault_Click(object sender, RoutedEventArgs e) {
            txtInputJson.Text = exampleDefault;
        }

        private void BtnTemplate1_Click(object sender, RoutedEventArgs e) {
            //L-NB39
            txtInputJson.Text = example1;
        }

        private void BtnTemplate2_Click(object sender, RoutedEventArgs e) {
            //Monitor-2
            txtInputJson.Text = example2;
        }

        private void BtnTemplate3_Click(object sender, RoutedEventArgs e) {
            //NB at home
            txtInputJson.Text = example3;
        }

        private void BtnTest_Click(object sender, RoutedEventArgs e) {
            try {
                dynamic json = JsonConvert.DeserializeObject(txtInputJson.Text);
                string jsonstr = KLC.Util.JsonPrettify(txtInputJson.Text);
                txtInputJson.Text = jsonstr;

                if (myViewer != null && myViewer.Visibility == Visibility.Visible)
                    myViewer.Close();
                LibKaseya.Agent.OSProfile profile = (bool)chkMac.IsChecked ? LibKaseya.Agent.OSProfile.Mac : LibKaseya.Agent.OSProfile.Other;
                myViewer = App.viewer = new WindowViewerV3(cmbRenderer.SelectedIndex, rcTest, profile);
                if(profile == LibKaseya.Agent.OSProfile.Mac)
                    myViewer.SetTitle("Test Mac", true);
                else
                    myViewer.SetTitle("Test", true);
                myViewer.Show();
                myViewer.UpdateScreenLayout(json, txtInputJson.Text);

                rcTest.LoopStart(myViewer);
            } catch(Exception) {
            }
        }

        private void chkRetina_Changed(object sender, RoutedEventArgs e) {
            rcTest.SetRetina((bool)chkRetina.IsChecked);
        }

    }
}
