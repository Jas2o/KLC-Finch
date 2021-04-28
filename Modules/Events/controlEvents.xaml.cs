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
        private Modules.EventsData eventsData;

        public controlEvents() {
            eventsData = new Modules.EventsData();
            this.DataContext = eventsData;
            InitializeComponent();
        }

        private void btnEventsStart_Click(object sender, RoutedEventArgs e) {
            KLC.LiveConnectSession session = ((WindowAlternative)Window.GetWindow(this)).session;
            if (session != null) {
                btnEventsStart.IsEnabled = false;
                lblExtended.Content = "";

                moduleEvents = new Modules.Events(session, eventsData, cmbLogType);
                session.ModuleEvents = moduleEvents;
            }
        }

        private void cmbLogType_DropDownClosed(object sender, EventArgs e) {
            if (moduleEvents == null)
                return;

            string value = ((ComboBox)sender).SelectedValue.ToString();
            if (value == Modules.Events.LabelExtended) {
                WindowEventsExtended wext = new WindowEventsExtended(lblExtended.Content.ToString());
                wext.Owner = ((WindowAlternative)Window.GetWindow(this));
                bool accept = (bool)wext.ShowDialog();
                if (accept) {
                    moduleEvents.SetLogType(wext.ReturnValue);
                    lblExtended.Content = wext.ReturnValue;
                }
            } else {
                moduleEvents.SetLogType(value);
                lblExtended.Content = "";
            }
        }

        private void btnEventsMore_Click(object sender, RoutedEventArgs e) {
            if (moduleEvents == null)
                return;

            //txtEvents.Focus();
            //txtEvents.CaretIndex = txtEvents.Text.Length;
            //txtEvents.ScrollToEnd();

            moduleEvents.GetMoreEvents();
        }

        private void btnEventsRefresh_Click(object sender, RoutedEventArgs e) {
            if (moduleEvents == null)
                return;

            moduleEvents.Refresh();
        }

        private void dgvEventsValues_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            Modules.EventValue ev = (Modules.EventValue)dgvEventsValues.SelectedValue;
            if (ev == null)
                return;

            txtEventDescription.Text = ev.EventMessage;
            txtEventComputer.Content = ev.Computer;
            txtEventUser.Content = ev.User;
            txtEventNumber.Content = ev.RecordNumber;
            txtEventQualifiers.Content = ev.EventQualifiers;
            txtEventCategory.Content = ev.Category;
        }

    }
}
