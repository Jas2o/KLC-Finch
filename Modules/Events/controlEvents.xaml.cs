using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

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
            if (session != null && session.WebsocketB.ControlAgentIsReady()) {
                btnEventsStart.IsEnabled = false;
                lblExtended.Content = "";
                lblExtended.ToolTip = "";
                lblExtended.Visibility = Visibility.Collapsed;

                moduleEvents = new Modules.Events(session, eventsData, cmbLogType);
                session.ModuleEvents = moduleEvents;
            }
        }

        private void cmbLogType_DropDownClosed(object sender, EventArgs e) {
            if (moduleEvents == null || cmbLogType.SelectedValue == null)
                return;

            string value = cmbLogType.SelectedValue.ToString();
            if (value == Modules.Events.LabelExtended) {
                WindowEventsExtended wext = new WindowEventsExtended(lblExtended.ToolTip.ToString());
                wext.Owner = ((WindowAlternative)Window.GetWindow(this));
                bool accept = (bool)wext.ShowDialog();
                if (accept) {
                    moduleEvents.SetLogType(wext.ReturnValue);

                    string[] split = wext.ReturnValue.Split(new char[] { '-' });
                    lblExtended.Content = split.Last();
                    lblExtended.ToolTip = wext.ReturnValue;
                    lblExtended.Visibility = Visibility.Visible;
                }
            } else {
                moduleEvents.SetLogType(value);
                lblExtended.Content = "";
                lblExtended.ToolTip = "";
                lblExtended.Visibility = Visibility.Collapsed;
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

        public void btnEventsRefresh_Click(object sender, RoutedEventArgs e) {
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

        private void txtFilter_TextChanged(object sender, TextChangedEventArgs e) {
            Filter();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e) {
            if (!this.IsVisible)
                return;

            if (App.Settings.AltModulesStartAuto) {
                if (btnEventsStart.IsEnabled) {
                    btnEventsStart_Click(sender, e);
                    btnEventsStart.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void chkEventsFilter_CheckedChanged(object sender, RoutedEventArgs e)
        {
            Filter();
        }

        private void Filter()
        {
            string text = txtFilter.Text;
            bool info = (bool)chkEventsFilterInfo.IsChecked;
            bool warn = (bool)chkEventsFilterWarn.IsChecked;
            bool error = (bool)chkEventsFilterError.IsChecked;

            if (info || warn || error)
            {
                ListCollectionView collectionView = (ListCollectionView)CollectionViewSource.GetDefaultView(dgvEventsValues.ItemsSource);
                collectionView.Filter = new Predicate<object>(x =>
                    (
                        ((Modules.EventValue)x).EventMessage.IndexOf(txtFilter.Text, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        ((Modules.EventValue)x).SourceName.IndexOf(txtFilter.Text, StringComparison.OrdinalIgnoreCase) >= 0
                    ) && (
                        (error && ((Modules.EventValue)x).EventType == 1) ||
                        (warn && ((Modules.EventValue)x).EventType == 2) ||
                        (info && ((Modules.EventValue)x).EventType == 4) ||
                        (info && ((Modules.EventValue)x).EventType == 0)
                    )
                );
            }
            else
            {
                ListCollectionView collectionView = (ListCollectionView)CollectionViewSource.GetDefaultView(dgvEventsValues.ItemsSource);
                collectionView.Filter = new Predicate<object>(x =>
                    ((Modules.EventValue)x).EventMessage.IndexOf(txtFilter.Text, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    ((Modules.EventValue)x).SourceName.IndexOf(txtFilter.Text, StringComparison.OrdinalIgnoreCase) >= 0
                );
                //collectionView.Refresh();
            }
        }
    }
}
