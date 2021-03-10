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
    /// Interaction logic for controlDashboard.xaml
    /// </summary>
    public partial class controlDashboard : UserControl {

        private Dashboard moduleDashboard;
        private StaticImage moduleStaticImage;

        public controlDashboard() {
            InitializeComponent();
        }

        private void btnDashboardStartData_Click(object sender, RoutedEventArgs e) {
            btnDashboardStartData.IsEnabled = false;

            KLC.LiveConnectSession session = ((WindowAlternative)Window.GetWindow(this)).session;
            if (session != null) {
                moduleDashboard = new Dashboard(session, txtDashboard);
                session.ModuleDashboard = moduleDashboard;
            }
        }

        private void btnDashboardGetCpuRam_Click(object sender, RoutedEventArgs e) {
            if (moduleDashboard != null) {
                moduleDashboard.GetCpuRam();
                //moduleDashboard.GetTopEvents();
                //moduleDashboard.GetTopProcesses();
            }
        }

        private void btnDashboardGetVolumes_Click(object sender, RoutedEventArgs e) {
            if (moduleDashboard != null)
                moduleDashboard.GetVolumes();
        }

        public void btnStaticImageStart_Click(object sender, RoutedEventArgs e) {
            btnStaticImageStart.IsEnabled = false;

            KLC.LiveConnectSession session = ((WindowAlternative)Window.GetWindow(this)).session;
            if (session != null) {
                moduleStaticImage = new StaticImage(session, imgScreenPreview);
                session.ModuleStaticImage = moduleStaticImage;
            }
        }

        private void btnStaticImageRefresh_Click(object sender, RoutedEventArgs e) {
            moduleStaticImage.RequestRefresh();
        }

        private void btnStaticImageRefreshFull_Click(object sender, RoutedEventArgs e) {
            moduleStaticImage.RequestRefreshFull();
        }
    }
}
