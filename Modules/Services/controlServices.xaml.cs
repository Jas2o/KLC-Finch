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
    /// Interaction logic for controlServices.xaml
    /// </summary>
    public partial class controlServices : UserControl {

        private Modules.Services moduleServices;

        public controlServices() {
            InitializeComponent();
        }

        private void btnServicesStart_Click(object sender, RoutedEventArgs e) {
            btnServicesStart.IsEnabled = false;

            KLC.LiveConnectSession session = ((WindowAlternative)Window.GetWindow(this)).session;
            if (session != null) {
                moduleServices = new Modules.Services(session, dgvServices, txtServices);
                session.ModuleServices = moduleServices;
            }
        }

        private void btnServicesRefresh_Click(object sender, RoutedEventArgs e) {
            moduleServices.RequestListServices();
        }
    }
}
