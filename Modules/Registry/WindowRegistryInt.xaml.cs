using System;
using System.Windows;
using System.Windows.Controls;

namespace KLC_Finch.Modules.Registry {
    public partial class WindowRegistryInt : Window {

        // Need to fix pasting in bad values

        public string ReturnName;
        public long ReturnValue;

        public WindowRegistryInt() {
            InitializeComponent();

            btnSave.IsEnabled = false;
        }

        public WindowRegistryInt(RegistryValue rv) : this() {
            InitializeComponent();

            txtName.Text = rv.Name;
            txtName.IsEnabled = false;
            txtValue.Text = rv.Data.ToString("X").ToLower();
        }

        private void radioBaseHex_Checked(object sender, RoutedEventArgs e) {
            if (this.Visibility == Visibility.Visible) {
                try {
                    long decToHex = long.Parse(txtValue.Text);
                    txtValue.Text = decToHex.ToString("X").ToLower();
                } catch(Exception) {
                }
            }
        }

        private void radioBaseDecimal_Checked(object sender, RoutedEventArgs e) {
            if (this.Visibility == Visibility.Visible) {
                try {
                    long hexToDec = long.Parse(txtValue.Text, System.Globalization.NumberStyles.HexNumber);
                    txtValue.Text = hexToDec.ToString();
                } catch (Exception) {
                }
            }
        }

        private void txtValue_TextChanged(object sender, TextChangedEventArgs e) {
            chkConfirmSave.IsChecked = false;
        }

        private void chkConfirmSave_Checked(object sender, RoutedEventArgs e) {
            try {
                if (radioBaseHex.IsChecked == true) {
                    long hexToDec = long.Parse(txtValue.Text, System.Globalization.NumberStyles.HexNumber);
                } else if (radioBaseDecimal.IsChecked == true) {
                    long decToHex = long.Parse(txtValue.Text);
                }

                btnSave.IsEnabled = (bool)chkConfirmSave.IsChecked;
            } catch(Exception) {
            }
        }

        private void chkConfirmSave_Unchecked(object sender, RoutedEventArgs e) {
            btnSave.IsEnabled = (bool)chkConfirmSave.IsChecked;
        }

        private void btnSave_Click(object sender, RoutedEventArgs e) {
            ReturnName = txtName.Text;
            if ((bool)radioBaseDecimal.IsChecked)
                ReturnValue = long.Parse(txtValue.Text);
            else
                ReturnValue = long.Parse(txtValue.Text, System.Globalization.NumberStyles.HexNumber);

            this.DialogResult = true;
            this.Close();
        }

    }
}
