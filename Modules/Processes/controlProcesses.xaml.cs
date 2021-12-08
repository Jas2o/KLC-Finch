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
        private Modules.ProcessesData processesData;

        public controlProcesses() {
            processesData = new Modules.ProcessesData();
            this.DataContext = processesData;
            InitializeComponent();
        }

        private void btnProcessesStart_Click(object sender, RoutedEventArgs e) {
            KLC.LiveConnectSession session = ((WindowAlternative)Window.GetWindow(this)).session;
            if (session != null && session.WebsocketB.ControlAgentIsReady()) {
                btnProcessesStart.IsEnabled = false;
                btnProcesseEnd.IsEnabled = false;

                moduleProcesses = new Modules.Processes(session, processesData);
                session.ModuleProcesses = moduleProcesses;
            }
        }

        public void btnProcessesRefresh_Click(object sender, RoutedEventArgs e) {
            if (moduleProcesses == null)
                return;

            moduleProcesses.RequestListProcesses();
        }

        private void btnProcesseEnd_Click(object sender, RoutedEventArgs e) {
            btnProcesseEnd.IsEnabled = false;

            Modules.ProcessValue pv = (Modules.ProcessValue)dgvProcesses.SelectedValue;
            if (pv == null)
                return;

            moduleProcesses.EndTask(pv);
        }

        private void dgvProcesses_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            btnProcesseEnd.IsEnabled = false;
        }

        private void btnProcesseSafety_Click(object sender, RoutedEventArgs e) {
            btnProcesseEnd.IsEnabled = true;
        }

        private string typedChars = string.Empty;

        private void dgvProcesses_PreviewKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Up || e.Key == Key.Down) {
                typedChars = string.Empty;
                e.Handled = false;
                return;
            }

            if (e.Key == Key.Space)
                typedChars += " ";
            else if (Char.IsLetter(e.Key.ToString(), 0))
                typedChars += e.Key.ToString();

            Modules.ProcessValue match = null;
            foreach (Modules.ProcessValue pv in dgvProcesses.Items) {
                if (pv.DisplayName.StartsWith(typedChars, true, System.Globalization.CultureInfo.InvariantCulture)) {
                    match = pv;
                    break;
                } else if (match != null)
                    break;
            }

            if (match == null)
                return;

            dgvProcesses.SelectedItem = match;
            dgvProcesses.ScrollIntoView(match);
        }

        private void dgvProcesses_PreviewMouseDown(object sender, MouseButtonEventArgs e) {
            typedChars = string.Empty;
        }

        private void txtFilterName_TextChanged(object sender, TextChangedEventArgs e) {
            ListCollectionView collectionView = (ListCollectionView)CollectionViewSource.GetDefaultView(dgvProcesses.ItemsSource);
            collectionView.Filter = new Predicate<object>(x => ((Modules.ProcessValue)x).DisplayName.IndexOf(txtFilterName.Text, StringComparison.OrdinalIgnoreCase) >= 0);
            //collectionView.Refresh();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e) {
            if (!this.IsVisible)
                return;

            if (App.Settings.AltModulesStartAuto) {
                if (btnProcessesStart.IsEnabled) {
                    btnProcessesStart_Click(sender, e);
                    btnProcessesStart.Visibility = Visibility.Collapsed;
                }
            }
        }
    }
}
