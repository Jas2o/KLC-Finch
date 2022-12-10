using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace KLC_Finch {
    /// <summary>
    /// Interaction logic for controlServices.xaml
    /// </summary>
    public partial class controlServices : UserControl {

        private Modules.Services moduleServices;
        private Modules.ServicesData servicesData;

        public controlServices() {
            servicesData = new Modules.ServicesData();
            this.DataContext = servicesData;
            InitializeComponent();
        }

        private void btnServicesStart_Click(object sender, RoutedEventArgs e) {
            KLC.LiveConnectSession session = ((WindowAlternative)Window.GetWindow(this)).session;
            if (session != null) {
                btnServicesStart.IsEnabled = false;
                ToggleButtons(false);

                moduleServices = new Modules.Services(session, servicesData);
                session.ModuleServices = moduleServices;
            }
        }

        public void btnServicesRefresh_Click(object sender, RoutedEventArgs e) {
            if (moduleServices == null)
                return;

            moduleServices.RequestListServices();
        }

        private void dgvServices_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            Modules.ServiceValue sv = (Modules.ServiceValue)dgvServices.SelectedValue;
            if (sv == null)
                return;

            lblServiceInfo.Text = sv.ServiceName + "\r\n" + sv.Description;
            ToggleButtons(sv.ServiceStatus, sv.StartupType);
        }

        private void btnServicesSelectedStart_Click(object sender, RoutedEventArgs e) {
            Modules.ServiceValue sv = (Modules.ServiceValue)dgvServices.SelectedValue;
            if (sv == null)
                return;

            moduleServices.Start(sv);
            ToggleButtons(false);
        }

        private void btnServicesSelectedStop_Click(object sender, RoutedEventArgs e) {
            Modules.ServiceValue sv = (Modules.ServiceValue)dgvServices.SelectedValue;
            if (sv == null)
                return;

            if (sv.DisplayName == "Kaseya Agent Endpoint")
                return;

            moduleServices.Stop(sv);
            ToggleButtons(false);
        }

        private void btnServicesSelectedRestart_Click(object sender, RoutedEventArgs e) {
            Modules.ServiceValue sv = (Modules.ServiceValue)dgvServices.SelectedValue;
            if (sv == null)
                return;

            if (sv.DisplayName == "Kaseya Agent Endpoint")
                return;

            moduleServices.Restart(sv);
            ToggleButtons(false);
        }

        private void btnServicesSetAuto_Click(object sender, RoutedEventArgs e) {
            Modules.ServiceValue sv = (Modules.ServiceValue)dgvServices.SelectedValue;
            if (sv == null)
                return;

            if (sv.DisplayName.Contains("Kaseya"))
                return;

            moduleServices.SetAuto(sv);
            ToggleButtons(false);
        }

        private void btnServicesSetManual_Click(object sender, RoutedEventArgs e) {
            Modules.ServiceValue sv = (Modules.ServiceValue)dgvServices.SelectedValue;
            if (sv == null)
                return;

            if (sv.DisplayName.Contains("Kaseya"))
                return;

            moduleServices.SetManual(sv);
            ToggleButtons(false);
        }

        private void btnServicesSetDisabled_Click(object sender, RoutedEventArgs e) {
            Modules.ServiceValue sv = (Modules.ServiceValue)dgvServices.SelectedValue;
            if (sv == null)
                return;

            if (sv.DisplayName.Contains("Kaseya"))
                return;

            moduleServices.SetDisabled(sv);
            ToggleButtons(false);
        }

        private void ToggleButtons(bool value) {
            btnServicesSelectedStop.IsEnabled = value;
            btnServicesSelectedStart.IsEnabled = value;
            btnServicesSelectedRestart.IsEnabled = value;

            btnServicesSetAuto.IsEnabled = value;
            btnServicesSetManual.IsEnabled = value;
            btnServicesSetDisabled.IsEnabled = value;
        }

        private void ToggleButtons(int serviceStatus, string StartupType) {
            btnServicesSelectedStop.IsEnabled = (serviceStatus == 4);
            btnServicesSelectedStart.IsEnabled = (serviceStatus == 1);
            btnServicesSelectedRestart.IsEnabled = (serviceStatus == 4);

            btnServicesSetAuto.IsEnabled = (StartupType != "Automatic");
            btnServicesSetManual.IsEnabled = (StartupType != "On demand");
            btnServicesSetDisabled.IsEnabled = (StartupType != "");
        }

        private string typedChars = string.Empty;

        private void dgvServices_PreviewKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Up || e.Key == Key.Down) {
                typedChars = string.Empty;
                e.Handled = false;
                return;
            }

            if (e.Key == Key.Space)
                typedChars += " ";
            else if (Char.IsLetter(e.Key.ToString(), 0))
                typedChars += e.Key.ToString();

            Modules.ServiceValue match = null;
            foreach (Modules.ServiceValue sv in dgvServices.Items) {
                if (sv.DisplayName.StartsWith(typedChars, true, System.Globalization.CultureInfo.InvariantCulture)) {
                    match = sv;
                    break;
                } else if (match != null)
                    break;
            }

            if (match == null)
                return;

            dgvServices.SelectedItem = match;
            dgvServices.ScrollIntoView(match);
        }

        private void dgvServices_PreviewMouseDown(object sender, MouseButtonEventArgs e) {
            typedChars = string.Empty;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e) {
            if (!this.IsVisible)
                return;

            if (App.Settings.AltModulesStartAuto) {
                if (btnServicesStart.IsEnabled) {
                    btnServicesStart_Click(sender, e);
                    btnServicesStart.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void chkServicesFilterAutoNotRunning_CheckedChanged(object sender, RoutedEventArgs e)
        {
            ListCollectionView collectionView = (ListCollectionView)CollectionViewSource.GetDefaultView(dgvServices.ItemsSource);
            if ((bool)chkServicesFilterAutoNotRunning.IsChecked)
            {
                collectionView.Filter = new Predicate<object>(x =>
                    ((Modules.ServiceValue)x).StartupType == "Automatic" && ((Modules.ServiceValue)x).ServiceStatus != 4
                );
            }
            else
            {
                collectionView.Filter = null;
            }
        }

        private void txtFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            ListCollectionView collectionView = (ListCollectionView)CollectionViewSource.GetDefaultView(dgvServices.ItemsSource);
            collectionView.Filter = new Predicate<object>(x =>
                ((Modules.ServiceValue)x).DisplayName.IndexOf(txtFilter.Text, StringComparison.OrdinalIgnoreCase) >= 0
                || ((Modules.ServiceValue)x).ServiceName.IndexOf(txtFilter.Text, StringComparison.OrdinalIgnoreCase) >= 0
                //|| ((Modules.ServiceValue)x).Description.IndexOf(txtFilter.Text, StringComparison.OrdinalIgnoreCase) >= 0
            );
            //collectionView.Refresh();
        }
    }
}
