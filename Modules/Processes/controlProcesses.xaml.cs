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
    /// Interaction logic for controlProcesses.xaml
    /// </summary>
    public partial class controlProcesses : UserControl {
        
        private Modules.Processes moduleProcesses;

        public controlProcesses() {
            InitializeComponent();
        }

        private void btnProcessesStart_Click(object sender, RoutedEventArgs e) {
            btnProcessesStart.IsEnabled = false;

            KLC.LiveConnectSession session = ((WindowAlternative)Window.GetWindow(this)).session;
            if (session != null) {
                moduleProcesses = new Modules.Processes(session, dgvProcesses, txtProcesses);
                session.ModuleProcesses = moduleProcesses;
            }
        }

        private void btnProcessesRefresh_Click(object sender, RoutedEventArgs e) {
            moduleProcesses.RequestListProcesses();
        }
    }
}
