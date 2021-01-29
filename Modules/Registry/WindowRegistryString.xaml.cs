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
    public partial class WindowRegistryString : Window {

        public string ReturnName;
        public string ReturnValue;

        public WindowRegistryString() {
            InitializeComponent();
            btnSave.IsEnabled = false;
        }

        public WindowRegistryString(RegistryValue rv) : this() {
            txtName.Text = rv.Name; //We can't change to (Default) as that's a valid name for another value.
            txtName.IsEnabled = false;
            txtInput.Text = rv.Data.ToString();
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
            ReturnValue = txtInput.Text;

            this.DialogResult = true;
            this.Close();
        }
    }
}
