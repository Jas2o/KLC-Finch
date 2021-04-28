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
    public partial class controlCommand : UserControl {

        private CommandTerminal moduleCommand;

        public controlCommand() {
            InitializeComponent();
        }

        private void btnCommandStart_Click(object sender, RoutedEventArgs e) {
            KLC.LiveConnectSession session = ((WindowAlternative)Window.GetWindow(this)).session;
            if (session != null) {
                btnCommandStart.IsEnabled = false;

                moduleCommand = new CommandTerminal(session, richCommand);
                session.ModuleCommandTerminal = moduleCommand;

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
    
        private void btnCommandLineMode_Click(object sender, RoutedEventArgs e) {

        }

        private void txtCommandInput_KeyDown(object sender, KeyEventArgs e) {
        }

        private void txtCommandInput_PreviewKeyDown(object sender, KeyEventArgs e) {
            if (moduleCommand == null)
                return;

            switch (e.Key) {
                case Key.Enter:
                    moduleCommand.Send(txtCommandInput.Text);
                    txtCommandInput.Clear();

                    e.Handled = true;
                    break;

                case Key.C:
                    if (Keyboard.IsKeyDown(Key.LeftCtrl) && txtCommandInput.SelectionLength == 0) {
                        moduleCommand.SendKillCommand();
                        e.Handled = true;
                    }
                    break;

                case Key.Up:
                    Console.WriteLine("CMD Up");
                    e.Handled = true;
                    break;

                case Key.Down:
                    Console.WriteLine("Down");
                    e.Handled = true;
                    break;
            }
        }
    }
}
