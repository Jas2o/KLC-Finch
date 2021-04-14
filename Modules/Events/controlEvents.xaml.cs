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
    /// Interaction logic for controlEvents.xaml
    /// </summary>
    public partial class controlEvents : UserControl {

        private Modules.Events moduleEvents;

        public controlEvents() {
            InitializeComponent();
        }

        private void btnEventsStart_Click(object sender, RoutedEventArgs e) {
            btnEventsStart.IsEnabled = false;

            KLC.LiveConnectSession session = ((WindowAlternative)Window.GetWindow(this)).session;
            if (session != null) {
                moduleEvents = new Modules.Events(session, dgvEventsValues, txtEvents, cmbLogType, cmbLogTypeExtended);
                session.ModuleEvents = moduleEvents;
            }
        }

        private void cmbLogType_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            moduleEvents.SetLogType(((ComboBox)sender).SelectedValue.ToString());
        }

        private void btnEventsMore_Click(object sender, RoutedEventArgs e) {
            txtEvents.Focus();
            txtEvents.CaretIndex = txtEvents.Text.Length;
            txtEvents.ScrollToEnd();

            moduleEvents.GetMoreEvents();
        }

        private void btnEventsRefresh_Click(object sender, RoutedEventArgs e) {
            moduleEvents.Refresh();
        }
    }
}
