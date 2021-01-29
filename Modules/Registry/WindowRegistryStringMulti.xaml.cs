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
using System.Windows.Shapes;

namespace KLC_Finch.Modules.Registry {
    public partial class WindowRegistryStringMulti : Window {

        public string ReturnName;
        public string[] ReturnValue;

        public WindowRegistryStringMulti() {
            InitializeComponent();
            btnSave.IsEnabled = false;
        }

        public WindowRegistryStringMulti(RegistryValue rv) : this() {
            txtName.Text = rv.Name; //We can't change to (Default) as that's a valid name for another value.
            txtName.IsEnabled = false;
            txtInput.Text = string.Join("\r\n", rv.Data);
        }

        private void txtInput_TextChanged(object sender, TextChangedEventArgs e) {
            chkConfirmSave.IsChecked = false;
        }

        private void chkConfirmSave_Checked(object sender, RoutedEventArgs e) {
            btnSave.IsEnabled = (bool)chkConfirmSave.IsChecked;
        }

        private void chkConfirmSave_Unchecked(object sender, RoutedEventArgs e) {
            btnSave.IsEnabled = (bool)chkConfirmSave.IsChecked;
        }

        private void btnSave_Click(object sender, RoutedEventArgs e) {
            ReturnName = txtName.Text;
            ReturnValue = txtInput.Text.Replace("\r", "").Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

            this.DialogResult = true;
            this.Close();
        }

        //--

        string[] GetLinesCollectionFromTextBox(TextBox textBox) {
            // lineCount may be -1 if TextBox layout info is not up-to-date.
            int lineCount = textBox.LineCount;
            if (lineCount == -1)
                lineCount = 0;

            string[] lines = new string[lineCount];

            for (int line = 0; line < lineCount; line++)
                // GetLineText takes a zero-based line index.
                lines[line] = textBox.GetLineText(line);

            return lines;
        }
    }
}
