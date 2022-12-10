using Ookii.Dialogs.Wpf;
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
using System.Windows.Shapes;
using VtNetCore.VirtualTerminal;
using VtNetCore.XTermParser;

namespace KLC_Finch {
    /// <summary>
    /// Interaction logic for controlCommand.xaml
    /// </summary>
    public partial class controlCommand : UserControl {

        private CommandTerminal moduleCommand;

        System.Timers.Timer timerRefresh;
        private VirtualTerminalController vtController;
        private DataConsumer dataPart;
        private bool colourized;

        private int historyPos;
        private List<string> history;
        private bool historyBlockUpDown;

        public controlCommand() {
            InitializeComponent();

            historyPos = -1;
            history = new List<string>();

            timerRefresh = new System.Timers.Timer(500);
            timerRefresh.Elapsed += TimerRefresh_Elapsed;
        }

        private void btnCommandStart_Click(object sender, RoutedEventArgs e) {
            KLC.LiveConnectSession session = ((WindowAlternative)Window.GetWindow(this)).session;
            if (session != null && session.WebsocketB.ControlAgentIsReady()) {
                btnCommandStart.IsEnabled = false;
                richCommand.Visibility = Visibility.Hidden;

                vtController = new VirtualTerminalController();
                //vtController.MaximumHistoryLines = 685; // Kaseya native
                vtController.MaximumHistoryLines = 2966; //To match real Powershell
                vtController.ResizeView(106, 54);
                dataPart = new VtNetCore.XTermParser.DataConsumer(vtController);

                moduleCommand = new CommandTerminal(session, vtController, dataPart);
                session.ModuleCommandTerminal = moduleCommand;

                timerRefresh.Start();
                txtCommandInput.Focus();
            }
        }

        private void TimerRefresh_Elapsed(object sender, ElapsedEventArgs e) {
            App.Current.Dispatcher.Invoke((Action)delegate {
                if (vtController.Changed) {
                    if (vtController.VisibleRows == vtController.MaximumHistoryLines)
                        vtController.ResizeView(vtController.VisibleColumns, 54);
                    colourized = false;

                    richCommand.Visibility = Visibility.Hidden;
                    txtCommand.Visibility = Visibility.Visible;

                    //txtCommand.Text = vtController.GetScreenText().Trim();

                    string[] lines = vtController.GetScreenText().TrimEnd().Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None );
                    txtCommand.Clear();
                    foreach(string line in lines)
                        txtCommand.AppendText(line.TrimEnd() + "\n");

                    txtCommand.ScrollToEnd();

                    vtController.ClearChanges();
                } else if (!colourized) {
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
                            //if (fgColor.R == 205 && fgColor.G == 205 && fgColor.B == 205)
                                //fgColor = Colors.White;
                            bgColor = (Color)ColorConverter.ConvertFromString(something.BackgroundColor);
                            //if (bgColor == Colors.Black)
                                //bgColor = Colors.MidnightBlue;

                            if(something == rows[i].Spans.Last())
                                richCommand.AppendText(something.Text.TrimEnd(), fgColor, bgColor);
                            else
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

        public static void SetForWindows() {
            throw new NotImplementedException();
        }

        private void btnCommandKill_Click(object sender, RoutedEventArgs e) {
            if (moduleCommand != null)
                moduleCommand.SendKillCommand();
        }

        private void btnCommandClear_Click(object sender, RoutedEventArgs e) {
            txtCommand.Clear();
            //richCommand.Document.Blocks.Clear();
        }

        private void btnCommandMacKillKRCH_Click(object sender, RoutedEventArgs e) {
            if (moduleCommand != null)
                moduleCommand.Send("killall KaseyaRemoteControlHost");
        }
    
        private void btnCommandLineMode_Click(object sender, RoutedEventArgs e) {

        }

        private void txtCommandInput_PreviewKeyDown(object sender, KeyEventArgs e) {
            if (moduleCommand == null)
                return;

            switch (e.Key) {
                case Key.Enter:
                    if (Keyboard.IsKeyDown(Key.LeftShift)) {
                        txtCommandInput.AppendText("\n");
                        txtCommandInput.CaretIndex = txtCommandInput.Text.Length;
                    } else {
                        if(history.LastOrDefault() != txtCommandInput.Text)
                            history.Add(txtCommandInput.Text);
                        historyPos = history.Count;
                        historyBlockUpDown = false;

                        moduleCommand.Send(txtCommandInput.Text);
                        txtCommandInput.Clear();
                    }

                    e.Handled = true;
                    break;

                case Key.Escape:
                    historyPos = history.Count;
                    historyBlockUpDown = false;
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
                    //Console.WriteLine("CMD Up");
                    if(txtCommandInput.Text.Contains("\n") && historyBlockUpDown) {
                        e.Handled = false;
                    } else {
                        historyPos--;

                        if (historyPos < 0)
                            historyPos = -1;
                        else if (historyPos < history.Count) {
                            txtCommandInput.Text = history[historyPos];
                            txtCommandInput.CaretIndex = txtCommandInput.Text.Length;
                        }
                        
                        e.Handled = true;
                    }
                    break;

                case Key.Down:
                    //Console.WriteLine("CMD Down");
                    if (txtCommandInput.Text.Contains("\n")) {
                        e.Handled = false;
                    } else {
                        historyPos++;

                        if (historyPos > history.Count - 1)
                            historyPos = history.Count;
                        else if (historyPos < history.Count) {
                            txtCommandInput.Text = history[historyPos];
                            txtCommandInput.CaretIndex = txtCommandInput.Text.Length;
                        }

                        e.Handled = true;
                    }
                    break;
            }

            if (txtCommandInput.Text.Contains("\n") && e.Key != Key.Up && e.Key != Key.Down)
                historyBlockUpDown = true;
        }

        private void btnCommandScrollback_Click(object sender, RoutedEventArgs e) {
            if (moduleCommand == null)
                return;

            richCommand.Visibility = Visibility.Hidden;
            txtCommand.Visibility = Visibility.Visible;

            int start = Math.Max(0, vtController.BottomRow - 740);
            //txtCommand.Text = vtController.GetText(0, start, vtController.VisibleColumns, vtController.BottomRow).Trim();

            string[] lines = vtController.GetText(0, start, vtController.VisibleColumns, vtController.BottomRow)
                .TrimEnd().Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            txtCommand.Clear();
            foreach (string line in lines)
                txtCommand.AppendText(line.TrimEnd() + "\n");

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

            using (TaskDialog dialog = new TaskDialog()) {
                dialog.WindowTitle = "KLC-Finch: Command";
                dialog.MainInstruction = "Render 2000 lines?";
                dialog.MainIcon = TaskDialogIcon.Information;
                dialog.CenterParent = true;
                dialog.Content = "You will not be able to interact until rendering completes, which can take a long time.";

                TaskDialogButton tdbYes = new TaskDialogButton(ButtonType.Yes);
                TaskDialogButton tdbCancel = new TaskDialogButton(ButtonType.Cancel);
                dialog.Buttons.Add(tdbYes);
                dialog.Buttons.Add(tdbCancel);

                TaskDialogButton button = dialog.ShowDialog((WindowAlternative)Window.GetWindow(this));
                if (button == tdbYes) {
                    richCommand.Visibility = Visibility.Hidden;
                    txtCommand.Visibility = Visibility.Visible;

                    int start = Math.Max(0, vtController.BottomRow - 2000);
                    //txtCommand.Text = vtController.GetText(0, start, vtController.VisibleColumns, vtController.BottomRow).Trim();

                    string[] lines = vtController.GetText(0, start, vtController.VisibleColumns, vtController.BottomRow)
                    .TrimEnd().Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                    txtCommand.Clear();
                    foreach (string line in lines)
                        txtCommand.AppendText(line.TrimEnd() + "\n");

                    txtCommand.ScrollToEnd();
                }
            }

            txtCommand.Background = new SolidColorBrush(Colors.Black);
            richCommand.Background = new SolidColorBrush(Colors.Black);
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e) {
            if (!this.IsVisible)
                return;

            if (App.Settings.AltModulesStartAuto) {
                if (btnCommandStart.IsEnabled) {
                    btnCommandStart_Click(sender, e);
                    btnCommandStart.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void btnCommandMacReleaseFn_Click(object sender, RoutedEventArgs e)
        {
            if (moduleCommand != null)
            {
                //63 = Fn
                //string command = "osascript -e 'tell application \"System Events\" to key up 63'";
                string command = "su $(stat -f \"%Su\" /dev/console) -c \"osascript -e 'tell application \\\"System Events\\\" to key up 63'\"";
                //Send twice as first time doesn't usually work.
                moduleCommand.Send(command);
                moduleCommand.Send(command);
            }
        }
    }
}
