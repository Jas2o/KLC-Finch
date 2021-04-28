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
    /// Interaction logic for controlCommand.xaml
    /// </summary>
    public partial class controlPowershell : UserControl {

        private CommandPowershell moduleCommand;

        public controlPowershell() {
            InitializeComponent();
        }

        private void btnCommandStart_Click(object sender, RoutedEventArgs e) {
            KLC.LiveConnectSession session = ((WindowAlternative)Window.GetWindow(this)).session;
            if (session != null) {
                btnCommandStart.IsEnabled = false;

                moduleCommand = new CommandPowershell(session, richCommand);
                session.ModuleCommandPowershell = moduleCommand;

                txtCommandInput.Focus();
            }
        }

        private void btnCommandKill_Click(object sender, RoutedEventArgs e) {
            if (moduleCommand != null)
                moduleCommand.SendKillCommand();
        }

        private void btnCommandClear_Click(object sender, RoutedEventArgs e) {
            richCommand.Document.Blocks.Clear();
        }

        private void btnCommandMacKillKRCH_Click(object sender, RoutedEventArgs e) {
            if (moduleCommand != null)
                moduleCommand.Send("killall KaseyaRemoteControlHost");
            }

        private void txtCommandInput_KeyDown(object sender, KeyEventArgs e) {
            if(e.Key == Key.Enter) {
                moduleCommand.Send(txtCommandInput.Text);
                txtCommandInput.Clear();

                e.Handled = true;
            }
        }

        private void btnCommandLineMode_Click(object sender, RoutedEventArgs e) {

        }

    }
}
