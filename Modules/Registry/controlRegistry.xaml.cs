using KLC_Finch.Modules.Registry;
using Ookii.Dialogs.Wpf;
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

namespace KLC_Finch
{
    /// <summary>
    /// Interaction logic for controlRegistry.xaml
    /// </summary>
    public partial class ControlRegistry : UserControl
    {

        private WindowAlternative window;
        private RegistryEditor moduleRegistry;

        public ControlRegistry()
        {
            InitializeComponent();
        }

        private void BtnRegistryStart_Click(object sender, RoutedEventArgs e)
        {
            KLC.LiveConnectSession session = ((WindowAlternative)Window.GetWindow(this)).session;
            if (session != null && session.WebsocketB.ControlAgentIsReady())
            {
                btnRegistryStart.IsEnabled = false;
                window = ((WindowAlternative)Window.GetWindow(this));

                moduleRegistry = new RegistryEditor(session, lvRegistryKeys, dgvRegistryValues, txtRegistryPath);
                session.ModuleRegistryEditor = moduleRegistry;
            }
        }

        private void BtnRegistryCreateKey_Click(object sender, RoutedEventArgs e)
        {
            if (moduleRegistry == null)
                return;

            WindowRegistryKey wrk = new WindowRegistryKey
            {
                Owner = window
            };
            bool accept = (bool)wrk.ShowDialog();
            if (accept)
            {
                moduleRegistry.CreateKey(wrk.ReturnName);
            }
        }

        private void BtnRegistryCreateValue_Click(object sender, RoutedEventArgs e)
        {
            btnRegistryCreateValue.ContextMenu.IsOpen = true;
        }

        private void BtnRegistryRenameKey_Click(object sender, RoutedEventArgs e)
        {
            if (moduleRegistry == null || RegistryDoingSomethingStupid())
                return;

            string keyOld = moduleRegistry.GetKey();

            WindowRegistryKey wrk = new WindowRegistryKey(keyOld)
            {
                Owner = window
            };
            bool accepted = (bool)wrk.ShowDialog();
            if (accepted)
            {
                moduleRegistry.RenameKey(keyOld, wrk.ReturnName);
            }
        }

        private void BtnRegistryRenameValue_Click(object sender, RoutedEventArgs e)
        {
            if (moduleRegistry == null || dgvRegistryValues.SelectedItem == null)
                return;

            string lookup = ((System.Data.DataRowView)dgvRegistryValues.SelectedItem).Row.ItemArray[0].ToString();

            RegistryValue rv = moduleRegistry.GetRegistryValue(lookup);
            if (rv != null && rv.Name != "")
            {
                string valueNameOld = rv.Name;

                WindowRegistryKey wrk = new WindowRegistryKey(rv.Name)
                {
                    Owner = window
                };
                bool accepted = (bool)wrk.ShowDialog();
                if (accepted)
                {
                    rv.Name = wrk.ReturnName;
                    moduleRegistry.RenameValue(valueNameOld, rv);
                }
            }
        }

        private void DestructiveDialog_VerificationClicked(object sender, EventArgs e)
        {
            TaskDialog td = (TaskDialog)sender;
            td.Buttons[0].Enabled = td.IsVerificationChecked;
        }

        private void BtnRegistryDeleteKey_Click(object sender, RoutedEventArgs e)
        {
            if (moduleRegistry == null || RegistryDoingSomethingStupid())
                return;

            string key = moduleRegistry.GetKey();

            using (TaskDialog dialog = new TaskDialog())
            {
                dialog.WindowTitle = "KLC-Finch: Registry";
                dialog.MainInstruction = "READ THIS CAREFULLY";
                //dialog.MainInstruction = "Delete key?";
                //dialog.MainIcon = TaskDialogIcon.Warning; //Overrides custom
                dialog.CustomMainIcon = Properties.Resources.WarningRed;
                dialog.CenterParent = true;
                dialog.Content = "Delete the key you are INSIDE?\r\n" + key;
                dialog.VerificationText = "Confirm";
                dialog.VerificationClicked += DestructiveDialog_VerificationClicked;

                TaskDialogButton tdbDelete = new TaskDialogButton("Delete")
                {
                    Enabled = false
                };
                TaskDialogButton tdbCancel = new TaskDialogButton(ButtonType.Cancel)
                {
                    Default = true
                };
                dialog.Buttons.Add(tdbDelete);
                dialog.Buttons.Add(tdbCancel);

                System.Media.SystemSounds.Beep.Play(); //Custom doesn't beep
                TaskDialogButton button = dialog.ShowDialog(window);
                if (button == tdbDelete)
                    moduleRegistry.DeleteKey(key);
            }
        }

        private bool RegistryDoingSomethingStupid()
        {
            if (txtRegistryPath.Text.EndsWith("CurrentVersion\\ProfileList"))
            {
                MessageBox.Show("Nope, don't be stupid. You probably want to go into a sub-key before making that change.");
                return true;
            }

            return false;
        }

        private void BtnRegistryDeleteValue_Click(object sender, RoutedEventArgs e)
        {
            if (moduleRegistry == null || dgvRegistryValues.SelectedItem == null)
                return;

            string lookup = ((System.Data.DataRowView)dgvRegistryValues.SelectedItem).Row.ItemArray[0].ToString();
            RegistryValue rv = moduleRegistry.GetRegistryValue(lookup);

            using (TaskDialog dialog = new TaskDialog())
            {
                //string word = (dgvFilesFiles.SelectedItems.Count == 1 ? "value" : "values");
                dialog.WindowTitle = "KLC-Finch: Registry";

                if (rv != null && rv.Name != "")
                {
                    dialog.MainInstruction = "Delete value?";
                    dialog.MainIcon = TaskDialogIcon.Warning;
                    dialog.CenterParent = true;
                    dialog.Content = string.Format("{0} ({1})", rv.Name, rv.Type);
                    dialog.VerificationText = "Confirm";
                    dialog.VerificationClicked += DestructiveDialog_VerificationClicked;

                    TaskDialogButton tdbDelete = new TaskDialogButton("Delete")
                    {
                        Enabled = false
                    };
                    TaskDialogButton tdbCancel = new TaskDialogButton(ButtonType.Cancel)
                    {
                        Default = true
                    };
                    dialog.Buttons.Add(tdbDelete);
                    dialog.Buttons.Add(tdbCancel);

                    TaskDialogButton button = dialog.ShowDialog(window);
                    if (button == tdbDelete)
                        moduleRegistry.DeleteValue(rv.Name);
                }
                else
                {
                    dialog.MainInstruction = "Can't delete value!";
                    dialog.MainIcon = TaskDialogIcon.Information;
                    dialog.CenterParent = true;
                    dialog.Content = "It is a Kaseya bug that the (Default) value cannot be deleted.";

                    TaskDialogButton tdbOk = new TaskDialogButton(ButtonType.Ok);
                    dialog.Buttons.Add(tdbOk);
                    //TaskDialogButton button = dialog.ShowDialog(window);
                }
            }
        }

        private void BtnRegistryPathUp_Click(object sender, RoutedEventArgs e)
        {
            if (moduleRegistry != null)
                moduleRegistry.GoUp();
        }

        public void BtnRegistryPathJump_Click(object sender, RoutedEventArgs e)
        {
            if (moduleRegistry != null)
                moduleRegistry.GoTo(txtRegistryPath.Text);
        }

        private void LvRegistryKeys_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (moduleRegistry == null)
                return;

            if (lvRegistryKeys.SelectedIndex > -1)
            {
                string selectedkey = lvRegistryKeys.SelectedItem.ToString();

                if (RegistryEditor.LabelForKey.ContainsValue(selectedkey))
                {
                    //int hive = RegistryEditor.LabelForKey.First(x => x.Value == selectedkey).Key;
                    //int hive = RegistryEditor.IdForKey[selectedkey];
                    moduleRegistry.SelectKey(selectedkey, true);
                }
                else
                {
                    moduleRegistry.SelectKey(selectedkey, false);
                }
            }
        }

        private void MenuRegCreateString_Click(object sender, RoutedEventArgs e)
        {
            if (moduleRegistry == null)
                return;

            WindowRegistryString wrs = new WindowRegistryString
            {
                Owner = window
            };
            bool accept = (bool)wrs.ShowDialog();
            if (accept)
            {
                RegistryValue rv = new RegistryValue(wrs.ReturnName, wrs.ReturnValue, false);
                moduleRegistry.CreateValue(rv);
            }
            //RegistryValue rv = new RegistryValue(valueName, "string");
            //moduleRegistry.CreateValue(rv);
        }

        private void MenuRegCreateBinary_Click(object sender, RoutedEventArgs e)
        {
            if (moduleRegistry == null)
                return;

            WindowRegistryBinary wrb = new WindowRegistryBinary
            {
                Owner = window
            };
            bool accept = (bool)wrb.ShowDialog();
            if (accept)
            {
                RegistryValue rv = new RegistryValue(wrb.ReturnName, wrb.ReturnValue);
                moduleRegistry.CreateValue(rv);
            }
            //RegistryValue rv = new RegistryValue(valueName, "string");
            //moduleRegistry.CreateValue(rv);
        }

        private void MenuRegCreateDword_Click(object sender, RoutedEventArgs e)
        {
            if (moduleRegistry == null)
                return;

            WindowRegistryInt wri = new WindowRegistryInt
            {
                Owner = window
            };
            bool accept = (bool)wri.ShowDialog();
            if (accept)
            {
                RegistryValue rv = new RegistryValue(wri.ReturnName, (int)wri.ReturnValue);
                moduleRegistry.CreateValue(rv);
            }
            //RegistryValue rv = new RegistryValue(valueName, new int);
            //moduleRegistry.CreateValue(rv);
        }

        private void MenuRegCreateQword_Click(object sender, RoutedEventArgs e)
        {
            if (moduleRegistry == null)
                return;

            WindowRegistryInt wri = new WindowRegistryInt
            {
                Owner = window
            };
            bool accept = (bool)wri.ShowDialog();
            if (accept)
            {
                RegistryValue rv = new RegistryValue(wri.ReturnName, (long)wri.ReturnValue);
                moduleRegistry.CreateValue(rv);
            }
            //RegistryValue rv = new RegistryValue(valueName, new long);
            //moduleRegistry.CreateValue(rv);
        }

        private void MenuRegCreateMulti_Click(object sender, RoutedEventArgs e)
        {
            if (moduleRegistry == null)
                return;

            WindowRegistryStringMulti wrsm = new WindowRegistryStringMulti
            {
                Owner = window
            };
            bool accept = (bool)wrsm.ShowDialog();
            if (accept)
            {
                RegistryValue rv = new RegistryValue(wrsm.ReturnName, wrsm.ReturnValue);
                moduleRegistry.CreateValue(rv);
            }
            //RegistryValue rv = new RegistryValue(valueName, new string[] { });
            //moduleRegistry.CreateValue(rv);
        }

        private void MenuRegCreateExpandable_Click(object sender, RoutedEventArgs e)
        {
            if (moduleRegistry == null)
                return;

            WindowRegistryString wrs = new WindowRegistryString
            {
                Owner = window
            };
            bool accept = (bool)wrs.ShowDialog();
            if (accept)
            {
                RegistryValue rv = new RegistryValue(wrs.ReturnName, wrs.ReturnValue, true);
                moduleRegistry.CreateValue(rv);
            }
            //RegistryValue rv = new RegistryValue(valueName, "string", true);
            //moduleRegistry.CreateValue(rv);
        }

        private void DgvRegistryValues_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (moduleRegistry == null || dgvRegistryValues.SelectedItem == null)
                return;

            string lookup = ((System.Data.DataRowView)dgvRegistryValues.SelectedItem).Row.ItemArray[0].ToString();

            RegistryValue rv = moduleRegistry.GetRegistryValue(lookup);
            if (rv == null) //Could be the default value
                rv = new RegistryValue("", "");

            switch (rv.Type)
            {
                case "REG_SZ":
                case "REG_EXPAND_SZ":
                    WindowRegistryString wrs = new WindowRegistryString(rv)
                    {
                        Owner = window
                    };
                    bool wrsdr = (bool)wrs.ShowDialog();
                    if (wrsdr)
                    {
                        rv.Data = wrs.ReturnValue;
                        moduleRegistry.ModifyValue(rv);
                    }
                    break;
                case "REG_MULTI_SZ":
                    WindowRegistryStringMulti wrsm = new WindowRegistryStringMulti(rv)
                    {
                        Owner = window
                    };
                    bool wrsmdr = (bool)wrsm.ShowDialog();
                    if (wrsmdr)
                    {
                        rv.Data = wrsm.ReturnValue;
                        moduleRegistry.ModifyValue(rv);
                    }
                    break;
                case "REG_DWORD":
                case "REG_QWORD":
                    WindowRegistryInt wri = new WindowRegistryInt(rv)
                    {
                        Owner = window
                    };
                    bool wridr = (bool)wri.ShowDialog();
                    if (wridr)
                    {
                        rv.Data = wri.ReturnValue;
                        moduleRegistry.ModifyValue(rv);
                    }
                    break;
                case "REG_BINARY":
                    WindowRegistryBinary wrb = new WindowRegistryBinary(rv)
                    {
                        Owner = window
                    };
                    bool wrbdr = (bool)wrb.ShowDialog();
                    if (wrbdr)
                    {
                        rv.Data = wrb.ReturnValue;
                        moduleRegistry.ModifyValue(rv);
                    }
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        private void TxtRegistryPath_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (moduleRegistry == null)
                return;

            if (e.Key == Key.Enter)
            {
                moduleRegistry.GoTo(txtRegistryPath.Text);
                e.Handled = true;
            }
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (!this.IsVisible)
                return;

            if (App.Settings.AltModulesStartAuto)
            {
                if (btnRegistryStart.IsEnabled)
                {
                    BtnRegistryStart_Click(sender, e);
                    btnRegistryStart.Visibility = Visibility.Collapsed;
                }
            }
        }
    }
}
