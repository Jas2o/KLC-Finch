using System;
using System.Collections.Generic;
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

namespace KLC_Finch.Modules.RemoteControl {
    /// <summary>
    /// Interaction logic for WindowOptions.xaml
    /// </summary>
    public partial class WindowOptions : Window {
        private Settings settings;

        public WindowOptions() {
            InitializeComponent();
            Title += " (" + App.Version + ")";
            btnSaveSettings.IsEnabled = false;
        }

        public WindowOptions(ref Settings settings) {
            InitializeComponent();
            Title += " (" + App.Version + ")";
            DataContext = this.settings = settings;
            txtSizeWidth.Text = this.settings.RemoteControlWidth.ToString();
            txtSizeHeight.Text = this.settings.RemoteControlHeight.ToString();
        }

        private void btnSaveSettings_Click(object sender, RoutedEventArgs e) {
            uint width = 1370; //Kaseya defaults
            uint height = 800;
            bool validW = uint.TryParse(txtSizeWidth.Text, out width);
            bool validH = uint.TryParse(txtSizeHeight.Text, out height);

            settings.RemoteControlWidth = Math.Max(width, 800);
            settings.RemoteControlHeight = Math.Max(height, 500);

            try {
                settings.Save();
                Close();
            } catch (Exception ex) {
                App.ShowUnhandledExceptionFromSrc("Seems we don't have permission to write to " + settings.FileName + "\r\n\r\n" + ex.ToString(), "Exception for Save Settings");
            }
        }

        private void btnPresetRecommended_Click(object sender, RoutedEventArgs e) {
            settings.AutotypeSkipLengthCheck = false;
            settings.StartControlEnabled = false;
            settings.ClipboardSyncEnabled = false;
            settings.KeyboardHook = false;
            settings.MacSwapCtrlWin = true;
            settings.StartMultiScreen = true;
            settings.MultiAltFit = true;
            settings.MultiShowCursor = true;
            settings.UseYUVShader = true;
            settings.ForceCanvas = false;

            txtSizeWidth.Text = "1370";
            txtSizeHeight.Text = "800";

            DataContext = null;
            DataContext = settings;
        }

        private void btnPresetKaseya_Click(object sender, RoutedEventArgs e) {
            settings.StartControlEnabled = true;
            settings.ClipboardSyncEnabled = true;
            settings.DisplayOverlayMouse = false;
            settings.DisplayOverlayKeyboardMod = false;
            settings.DisplayOverlayKeyboardOther = false;
            settings.DisplayOverlayKeyboardHook = false;
            settings.KeyboardHook = true;
            settings.MacSwapCtrlWin = false;
            settings.StartMultiScreen = false;
            settings.MultiAltFit = false;
            settings.MultiShowCursor = false;
            settings.UseYUVShader = true;
            settings.ForceCanvas = false;

            txtSizeWidth.Text = "1370";
            txtSizeHeight.Text = "800";

            DataContext = null;
            DataContext = settings;
        }

    }
}
