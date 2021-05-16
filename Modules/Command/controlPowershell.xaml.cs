using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using VtNetCore.VirtualTerminal;
using VtNetCore.XTermParser;
using System.Windows.Shapes;

namespace KLC_Finch {
    /// <summary>
    /// Interaction logic for controlCommand.xaml
    /// </summary>
    public partial class controlPowershell : UserControl {

        private CommandPowershell moduleCommand;
        private bool lineMode;

        System.Timers.Timer timerRefresh;
        private VirtualTerminalController vtController;
        private DataConsumer dataPart;
        private bool colourized;

        public controlPowershell() {
            InitializeComponent();
            lineMode = true;

            timerRefresh = new System.Timers.Timer(500);
            timerRefresh.Elapsed += TimerRefresh_Elapsed;
        }

        private void TimerRefresh_Elapsed(object sender, ElapsedEventArgs e) {
            App.Current.Dispatcher.Invoke((Action)delegate {
                if (vtController.Changed) {
                    if (vtController.VisibleRows == vtController.MaximumHistoryLines)
                        vtController.ResizeView(vtController.VisibleColumns, 54);
                    colourized = false;

                    richCommand.Visibility = Visibility.Hidden;
                    txtCommand.Visibility = Visibility.Visible;

                    txtCommand.Text = vtController.GetScreenText().Trim();
                    txtCommand.ScrollToEnd();

                    vtController.ClearChanges();
                } else if(!colourized) {
                    colourized = true;
                    if (!(bool)chkAllowColour.IsChecked)
                        return;

                    richCommand.Document.Blocks.Clear();

                    int start = Math.Max(0, vtController.BottomRow - vtController.VisibleRows);
                    List<VtNetCore.VirtualTerminal.Layout.LayoutRow> rows = vtController.GetPageSpans(start, vtController.VisibleRows);

                    int lastGoodRow = 0;
                    for (int i = 0; i < rows.Count; i++) {
                        foreach (VtNetCore.VirtualTerminal.Layout.LayoutSpan something in rows[i].Spans) {
                            if (something.Text.Trim().Length > 0)
                                lastGoodRow = i;
                            break;
                        }
                    }

                    Color fgColor, bgColor;
                    for (int i = 0; i <= lastGoodRow; i++) {
                        foreach (VtNetCore.VirtualTerminal.Layout.LayoutSpan something in rows[i].Spans) {
                            fgColor = (Color)ColorConverter.ConvertFromString(something.ForgroundColor);
                            if (fgColor.R == 205 && fgColor.G == 205 && fgColor.B == 205)
                                fgColor = Colors.White;
                            bgColor = (Color)ColorConverter.ConvertFromString(something.BackgroundColor);
                            if (bgColor == Colors.Black)
                                bgColor = Colors.MidnightBlue;
                            richCommand.AppendText(something.Text, fgColor, bgColor);
                        }

                        richCommand.AppendText("\r\n");
                    }

                    richCommand.ScrollToEnd();
                    richCommand.Visibility = Visibility.Visible;
                    txtCommand.Visibility = Visibility.Hidden;
                }
            });
        }

        private void btnCommandStart_Click(object sender, RoutedEventArgs e) {
            KLC.LiveConnectSession session = ((WindowAlternative)Window.GetWindow(this)).session;
            if (session != null) {
                btnCommandStart.IsEnabled = false;
                richCommand.Visibility = Visibility.Hidden;

                vtController = new VirtualTerminalController();
                vtController.NullAttribute.BackgroundColor = VtNetCore.VirtualTerminal.Enums.ETerminalColor.Blue;

                //vtController.MaximumHistoryLines = 685; // Kaseya native
                vtController.MaximumHistoryLines = 2966; //To match real Powershell
                vtController.ResizeView(106, 54);
                dataPart = new VtNetCore.XTermParser.DataConsumer(vtController);

                moduleCommand = new CommandPowershell(session, vtController, dataPart);
                session.ModuleCommandPowershell = moduleCommand;

                timerRefresh.Start();
                txtCommandInput.Focus();
            }
        }

        private void btnCommandKill_Click(object sender, RoutedEventArgs e) {
            if (moduleCommand != null)
                moduleCommand.SendKillCommand();
        }

        private void btnCommandClear_Click(object sender, RoutedEventArgs e) {
            //txtCommand.Clear();
            richCommand.Document.Blocks.Clear();
        }

        private void btnCommandMacKillKRCH_Click(object sender, RoutedEventArgs e) {
            if (moduleCommand != null)
                moduleCommand.Send("killall KaseyaRemoteControlHost", true);
        }

        private void txtCommandInput_PreviewKeyDown(object sender, KeyEventArgs e) {
            if (moduleCommand == null)
                return;

            if (lineMode) {
                switch (e.Key) {
                    case Key.Enter:
                        if (Keyboard.IsKeyDown(Key.LeftShift)) {
                            txtCommandInput.AppendText("\n");
                            txtCommandInput.CaretIndex = txtCommandInput.Text.Length;
                        } else {
                            moduleCommand.Send(txtCommandInput.Text, true);
                            txtCommandInput.Clear();
                        }

                        e.Handled = true;
                        break;

                    case Key.C:
                        if (Keyboard.IsKeyDown(Key.LeftCtrl) && txtCommandInput.SelectionLength == 0) {
                            moduleCommand.SendKillCommand();
                            e.Handled = true;
                        }
                        break;

                    case Key.Up:
                        Console.WriteLine("PS Up");
                        e.Handled = false;
                        break;

                    case Key.Down:
                        Console.WriteLine("PS Down");
                        e.Handled = false;
                        break;
                }
            } else {
                if (e.Key == Key.Up)
                    moduleCommand.Send("%1B%5BA", false); //^[A
                else if (e.Key == Key.Down)
                    moduleCommand.Send("%1B%5BB", false); //^[B
                else if (e.Key == Key.Left)
                    moduleCommand.Send("%1B%5BC", false); //^[C
                else if (e.Key == Key.Right)
                    moduleCommand.Send("%1B%5BD", false); //^[D
                else if (e.Key == Key.Tab)
                    moduleCommand.Send("%09", false);
                else if (e.Key == Key.Back)
                    moduleCommand.Send("%7F", false);
                else if (e.Key == Key.Enter)
                    moduleCommand.Send("", true);
                else {
                    moduleCommand.Send(e.Key.ToString(), false);
                    Console.WriteLine(e.Key.ToString());
                }
                txtCommandInput.Clear();

                e.Handled = true;
            }
        }

        private void btnCommandLineMode_Click(object sender, RoutedEventArgs e) {
            lineMode = !lineMode;

            if (lineMode)
                btnCommandLineMode.Content = "Line Mode";
            else
                btnCommandLineMode.Content = "Game Mode";
        }

        private void btnCommandScrollback_Click(object sender, RoutedEventArgs e) {
            if (moduleCommand == null)
                return;

            richCommand.Visibility = Visibility.Hidden;
            txtCommand.Visibility = Visibility.Visible;

            int start = Math.Max(0, vtController.BottomRow - 740);
            txtCommand.Text = vtController.GetText(0, start, vtController.VisibleColumns, vtController.BottomRow).Trim();
            txtCommand.ScrollToEnd();

            /*
            richCommand.Document.Blocks.Clear();
            richCommand.Document.Blocks.Add(new Paragraph(new Run(vtController.GetText(0, start, vtController.VisibleColumns, vtController.BottomRow).Trim())));
            richCommand.ScrollToEnd();
            */
        }

        private void btnCommandScrollbackSlow_Click(object sender, RoutedEventArgs e) {
            if (moduleCommand == null)
                return;

            txtCommand.Background = new SolidColorBrush(Colors.Indigo);
            richCommand.Background = new SolidColorBrush(Colors.Indigo);

            MessageBoxResult result = MessageBox.Show("Render 2000 lines? You will not be able to interact until rendering completes, which can take a long time.", "KLC-Finch Powershell", MessageBoxButton.YesNo);
            if(result == MessageBoxResult.Yes) {
                richCommand.Visibility = Visibility.Hidden;
                txtCommand.Visibility = Visibility.Visible;

                int start = Math.Max(0, vtController.BottomRow - 2000);
                txtCommand.Text = vtController.GetText(0, start, vtController.VisibleColumns, vtController.BottomRow).Trim();
                txtCommand.ScrollToEnd();

                /*
                richCommand.Document.Blocks.Clear();
                richCommand.Document.Blocks.Add(new Paragraph(new Run(vtController.GetText(0, start, vtController.VisibleColumns, vtController.BottomRow).Trim())));
                richCommand.ScrollToEnd();
                */
            }

            txtCommand.Background = new SolidColorBrush(Colors.MidnightBlue);
            richCommand.Background = new SolidColorBrush(Colors.MidnightBlue);
        }
    }
}
