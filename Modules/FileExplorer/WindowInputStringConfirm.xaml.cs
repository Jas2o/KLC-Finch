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

namespace KLC_Finch {
    public partial class WindowInputStringConfirm : Window {

        public string ReturnName;

        public WindowInputStringConfirm(string title, string label, string value) {
            InitializeComponent();
            btnSave.IsEnabled = false;
            Title = "KLC-Finch: " + title;
            txtName.Text = value;
            lblLabel.Content = label;
            if (label == "")
                lblLabel.Visibility = Visibility.Collapsed;
        }

        private void txtName_TextChanged(object sender, TextChangedEventArgs e) {
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

            this.DialogResult = true;
            this.Close();
        }

    }
}
