using Ookii.Dialogs.Wpf;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace KLC_Finch {
    /// <summary>
    /// Interaction logic for controlToolbox.xaml
    /// </summary>
    public partial class controlToolbox : UserControl {

        private Toolbox moduleToolbox;
        private ToolboxData toolboxData;

        public controlToolbox() {
            toolboxData = new ToolboxData();
            this.DataContext = toolboxData;
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e) {
            if (!this.IsVisible)
                return;

            if (App.Settings.AltModulesStartAuto) {
                if (btnToolboxStart.IsEnabled) {
                    btnToolboxStart_Click(sender, e);
                    btnToolboxStart.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void btnToolboxStart_Click(object sender, RoutedEventArgs e) {
            KLC.LiveConnectSession session = ((WindowAlternative)Window.GetWindow(this)).session;
            if (session != null && session.WebsocketB.ControlAgentIsReady()) {
                btnToolboxStart.IsEnabled = false;
                btnToolboxDownload.IsEnabled = false;
                btnToolboxExecute.IsEnabled = false;

                moduleToolbox = new Toolbox(session, toolboxData);
                session.ModuleToolbox = moduleToolbox;
            }
        }

        private void btnToolboxExecute_Click(object sender, RoutedEventArgs e) {
            ToolboxValue tv = (ToolboxValue)dgvToolbox.SelectedValue;
            if (tv == null)
                return;

            btnToolboxExecute.IsEnabled = false;
            moduleToolbox.Execute(tv);
        }

        private void btnToolboxDownload_Click(object sender, RoutedEventArgs e) {
            ToolboxValue tv = (ToolboxValue)dgvToolbox.SelectedValue;
            if (tv == null)
                return;

            VistaFolderBrowserDialog folderDialog = new VistaFolderBrowserDialog();
            if (folderDialog.ShowDialog() == true) {
                btnToolboxDownload.IsEnabled = false;
                moduleToolbox.Download(tv, folderDialog.SelectedPath);
            }
        }

        private void dgvToolbox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            btnToolboxDownload.IsEnabled = true;
            btnToolboxExecute.IsEnabled = true;
            toolboxData.Status = "";
        }
    }
}
