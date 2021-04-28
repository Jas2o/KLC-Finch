using KLC_Finch.Modules.Registry;
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
    /// Interaction logic for controlRegistry.xaml
    /// </summary>
    public partial class controlRegistry : UserControl {

        private WindowAlternative window;
        private RegistryEditor moduleRegistry;

        public controlRegistry() {
            InitializeComponent();
        }

        private void btnRegistryStart_Click(object sender, RoutedEventArgs e) {
            KLC.LiveConnectSession session = ((WindowAlternative)Window.GetWindow(this)).session;
            if (session != null) {
                btnRegistryStart.IsEnabled = false;
                btnRegistryDeleteKey.IsEnabled = false;
                btnRegistryDeleteValue.IsEnabled = false;
                window = ((WindowAlternative)Window.GetWindow(this));

                moduleRegistry = new RegistryEditor(session, lvRegistryKeys, dgvRegistryValues, txtRegistryPath, txtRegistry);
                session.ModuleRegistryEditor = moduleRegistry;

                chkRegistryEnableDelete.IsChecked = false;
            }
        }

        private void btnRegistryCreate_Click(object sender, RoutedEventArgs e) {
            btnRegistryCreate.ContextMenu.IsOpen = true;
        }

        private void btnRegistryRenameKey_Click(object sender, RoutedEventArgs e) {
            if (moduleRegistry == null)
                return;

            string keyOld = moduleRegistry.GetKey();

            WindowRegistryKey wrk = new WindowRegistryKey(keyOld);
            wrk.Owner = window;
            bool accepted = (bool)wrk.ShowDialog();
            if (accepted) {
                moduleRegistry.RenameKey(keyOld, wrk.ReturnName);
            }
        }

        private void btnRegistryRenameValue_Click(object sender, RoutedEventArgs e) {
            if (moduleRegistry == null || dgvRegistryValues.SelectedItem == null)
                return;

            string lookup = ((System.Data.DataRowView)dgvRegistryValues.SelectedItem).Row.ItemArray[0].ToString();

            RegistryValue rv = moduleRegistry.GetRegistryValue(lookup);
            if (rv != null && rv.Name != "") {
                string valueNameOld = rv.Name;

                WindowRegistryKey wrk = new WindowRegistryKey(rv.Name);
                wrk.Owner = window;
                bool accepted = (bool)wrk.ShowDialog();
                if (accepted) {
                    rv.Name = wrk.ReturnName;
                    moduleRegistry.RenameValue(valueNameOld, rv);
                }
            }
        }

        private void btnRegistryDeleteKey_Click(object sender, RoutedEventArgs e) {
            if (moduleRegistry == null || !(bool)chkRegistryEnableDelete.IsChecked)
                return;

            chkRegistryEnableDelete.IsChecked = false;

            string key = moduleRegistry.GetKey();

            MessageBox.Show("To delete key '" + key + "' you must type it exactly in the next window.");

            WindowRegistryKey wrk = new WindowRegistryKey();
            wrk.Owner = window;
            bool accepted = (bool)wrk.ShowDialog();
            if (accepted) {
                if (wrk.ReturnName == key)
                    moduleRegistry.DeleteKey(wrk.ReturnName);
                else
                    MessageBox.Show("Did not delete key '" + key + "'.");
            }
        }

        private void btnRegistryDeleteValue_Click(object sender, RoutedEventArgs e) {
            if (moduleRegistry == null || !(bool)chkRegistryEnableDelete.IsChecked || dgvRegistryValues.SelectedItem == null)
                return;

            chkRegistryEnableDelete.IsChecked = false;

            string lookup = ((System.Data.DataRowView)dgvRegistryValues.SelectedItem).Row.ItemArray[0].ToString();

            RegistryValue rv = moduleRegistry.GetRegistryValue(lookup);
            if (rv != null && rv.Name != "")
                moduleRegistry.DeleteValue(rv.Name);
            else
                MessageBox.Show("It is a Kaseya bug that the (Default) value cannot be deleted.");
        }

        private void chkRegistryEnableDelete_Checked(object sender, RoutedEventArgs e) {
            btnRegistryDeleteKey.IsEnabled = (bool)chkRegistryEnableDelete.IsChecked;
            btnRegistryDeleteValue.IsEnabled = (bool)chkRegistryEnableDelete.IsChecked;
        }

        private void chkRegistryEnableDelete_Unchecked(object sender, RoutedEventArgs e) {
            btnRegistryDeleteKey.IsEnabled = (bool)chkRegistryEnableDelete.IsChecked;
            btnRegistryDeleteValue.IsEnabled = (bool)chkRegistryEnableDelete.IsChecked;
        }

        private void btnRegistryPathUp_Click(object sender, RoutedEventArgs e) {
            if (moduleRegistry != null)
                moduleRegistry.GoUp();
        }

        private void btnRegistryPathJump_Click(object sender, RoutedEventArgs e) {
            if (moduleRegistry != null)
                moduleRegistry.GoTo(txtRegistryPath.Text);
        }

        private void lvRegistryKeys_MouseDoubleClick(object sender, MouseButtonEventArgs e) {
            if (moduleRegistry == null)
                return;

            if (lvRegistryKeys.SelectedIndex > -1) {
                string selectedkey = lvRegistryKeys.SelectedItem.ToString();

                if (RegistryEditor.LabelForKey.ContainsValue(selectedkey)) {
                    //int hive = RegistryEditor.LabelForKey.First(x => x.Value == selectedkey).Key;
                    //int hive = RegistryEditor.IdForKey[selectedkey];
                    moduleRegistry.SelectKey(selectedkey, true);
                } else {
                    moduleRegistry.SelectKey(selectedkey, false);
                }
            }
        }

        private void menuRegCreateKey_Click(object sender, RoutedEventArgs e) {
            if (moduleRegistry == null)
                return;

            WindowRegistryKey wrk = new WindowRegistryKey();
            wrk.Owner = window;
            bool accept = (bool)wrk.ShowDialog();
            if (accept) {
                moduleRegistry.CreateKey(wrk.ReturnName);
            }
        }

        private void menuRegCreateString_Click(object sender, RoutedEventArgs e) {
            if (moduleRegistry == null)
                return;

            WindowRegistryString wrs = new WindowRegistryString();
            wrs.Owner = window;
            bool accept = (bool)wrs.ShowDialog();
            if (accept) {
                RegistryValue rv = new RegistryValue(wrs.ReturnName, wrs.ReturnValue, false);
                moduleRegistry.CreateValue(rv);
            }
            //RegistryValue rv = new RegistryValue(valueName, "string");
            //moduleRegistry.CreateValue(rv);
        }

        private void menuRegCreateBinary_Click(object sender, RoutedEventArgs e) {
            if (moduleRegistry == null)
                return;

            WindowRegistryBinary wrb = new WindowRegistryBinary();
            wrb.Owner = window;
            bool accept = (bool)wrb.ShowDialog();
            if (accept) {
                RegistryValue rv = new RegistryValue(wrb.ReturnName, wrb.ReturnValue);
                moduleRegistry.CreateValue(rv);
            }
            //RegistryValue rv = new RegistryValue(valueName, "string");
            //moduleRegistry.CreateValue(rv);
        }

        private void menuRegCreateDword_Click(object sender, RoutedEventArgs e) {
            if (moduleRegistry == null)
                return;

            WindowRegistryInt wri = new WindowRegistryInt();
            bool accept = (bool)wri.ShowDialog();
            wri.Owner = window;
            if (accept) {
                RegistryValue rv = new RegistryValue(wri.ReturnName, (int)wri.ReturnValue);
                moduleRegistry.CreateValue(rv);
            }
            //RegistryValue rv = new RegistryValue(valueName, new int);
            //moduleRegistry.CreateValue(rv);
        }

        private void menuRegCreateQword_Click(object sender, RoutedEventArgs e) {
            if (moduleRegistry == null)
                return;

            WindowRegistryInt wri = new WindowRegistryInt();
            wri.Owner = window;
            bool accept = (bool)wri.ShowDialog();
            if (accept) {
                RegistryValue rv = new RegistryValue(wri.ReturnName, (long)wri.ReturnValue);
                moduleRegistry.CreateValue(rv);
            }
            //RegistryValue rv = new RegistryValue(valueName, new long);
            //moduleRegistry.CreateValue(rv);
        }

        private void menuRegCreateMulti_Click(object sender, RoutedEventArgs e) {
            if (moduleRegistry == null)
                return;

            WindowRegistryStringMulti wrsm = new WindowRegistryStringMulti();
            wrsm.Owner = window;
            bool accept = (bool)wrsm.ShowDialog();
            if (accept) {
                RegistryValue rv = new RegistryValue(wrsm.ReturnName, wrsm.ReturnValue);
                moduleRegistry.CreateValue(rv);
            }
            //RegistryValue rv = new RegistryValue(valueName, new string[] { });
            //moduleRegistry.CreateValue(rv);
        }

        private void menuRegCreateExpandable_Click(object sender, RoutedEventArgs e) {
            if (moduleRegistry == null)
                return;

            WindowRegistryString wrs = new WindowRegistryString();
            wrs.Owner = window;
            bool accept = (bool)wrs.ShowDialog();
            if (accept) {
                RegistryValue rv = new RegistryValue(wrs.ReturnName, wrs.ReturnValue, true);
                moduleRegistry.CreateValue(rv);
            }
            //RegistryValue rv = new RegistryValue(valueName, "string", true);
            //moduleRegistry.CreateValue(rv);
        }

        private void dgvRegistryValues_MouseDoubleClick(object sender, MouseButtonEventArgs e) {
            if (moduleRegistry == null || dgvRegistryValues.SelectedItem == null)
                return;

            string lookup = ((System.Data.DataRowView)dgvRegistryValues.SelectedItem).Row.ItemArray[0].ToString();

            RegistryValue rv = moduleRegistry.GetRegistryValue(lookup);
            if (rv == null) //Could be the default value
                rv = new RegistryValue("", "");

            switch (rv.Type) {
                case "REG_SZ":
                case "REG_EXPAND_SZ":
                    WindowRegistryString wrs = new WindowRegistryString(rv);
                    wrs.Owner = window;
                    bool wrsdr = (bool)wrs.ShowDialog();
                    if (wrsdr) {
                        rv.Data = wrs.ReturnValue;
                        moduleRegistry.ModifyValue(rv);
                    }
                    break;
                case "REG_MULTI_SZ":
                    WindowRegistryStringMulti wrsm = new WindowRegistryStringMulti(rv);
                    wrsm.Owner = window;
                    bool wrsmdr = (bool)wrsm.ShowDialog();
                    if (wrsmdr) {
                        rv.Data = wrsm.ReturnValue;
                        moduleRegistry.ModifyValue(rv);
                    }
                    break;
                case "REG_DWORD":
                case "REG_QWORD":
                    WindowRegistryInt wri = new WindowRegistryInt(rv);
                    wri.Owner = window;
                    bool wridr = (bool)wri.ShowDialog();
                    if (wridr) {
                        rv.Data = wri.ReturnValue;
                        moduleRegistry.ModifyValue(rv);
                    }
                    break;
                case "REG_BINARY":
                    WindowRegistryBinary wrb = new WindowRegistryBinary(rv);
                    wrb.Owner = window;
                    bool wrbdr = (bool)wrb.ShowDialog();
                    if (wrbdr) {
                        rv.Data = wrb.ReturnValue;
                        moduleRegistry.ModifyValue(rv);
                    }
                    break;
                default:
                    throw new NotImplementedException();
                    break;
            }
        }
    }
}
