using KLC_Finch.Modules.Registry;
using Microsoft.Win32;
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
    /// Interaction logic for controlFiles.xaml
    /// </summary>
    public partial class controlFiles : UserControl {

        private WindowAlternative window;
        private FileExplorer moduleFileExplorer;

        public controlFiles() {
            InitializeComponent();
        }

        private void btnFilesPathUp_Click(object sender, RoutedEventArgs e) {
            if (moduleFileExplorer != null)
                moduleFileExplorer.GoUp();
        }

        private void btnFilesPathJump_Click(object sender, RoutedEventArgs e) {
            if (moduleFileExplorer != null)
                moduleFileExplorer.GoTo(txtFilesPath.Text);
        }

        private void btnFilesStart_Click(object sender, RoutedEventArgs e) {
            KLC.LiveConnectSession session = ((WindowAlternative)Window.GetWindow(this)).session;
            if (session != null) {
                btnFilesStart.IsEnabled = false;
                btnFilesFolderDelete.IsEnabled = false;
                btnFilesFileDelete.IsEnabled = false;
                window = ((WindowAlternative)Window.GetWindow(this));

                moduleFileExplorer = new FileExplorer(session);
                moduleFileExplorer.LinkToUI(listFilesFolders, dgvFilesFiles, txtFilesPath, txtFiles, progressBar, progressText, btnFilesDownload, btnFilesUpload);
                session.ModuleFileExplorer = moduleFileExplorer;
            }
        }

        private void listFilesFolders_MouseDoubleClick(object sender, MouseButtonEventArgs e) {
            if (listFilesFolders.SelectedIndex > -1) {
                string selectedkey = listFilesFolders.SelectedItem.ToString();

                moduleFileExplorer.SelectPath(selectedkey);
            }
        }

        private void btnFilesDownload_Click(object sender, RoutedEventArgs e) {
            if (moduleFileExplorer == null || dgvFilesFiles.SelectedItem == null)
                return;

            string selectedFile = ((System.Data.DataRowView)dgvFilesFiles.SelectedItem).Row.ItemArray[0].ToString();

            if (selectedFile != null && selectedFile.Length > 0) {
                SaveFileDialog saveFileDialog = new SaveFileDialog();
                saveFileDialog.FileName = selectedFile;
                if (saveFileDialog.ShowDialog() == true) {
                    btnFilesDownload.IsEnabled = false;
                    btnFilesUpload.IsEnabled = false;
                    moduleFileExplorer.Download(selectedFile, saveFileDialog.FileName);
                }
            }
        }

        private void btnFilesUpload_Click(object sender, RoutedEventArgs e) {
            if (moduleFileExplorer == null || moduleFileExplorer.GetSelectedPathLength() == 0)
                return;

            OpenFileDialog openFileDialog = new OpenFileDialog();
            if(openFileDialog.ShowDialog() == true) {
                bool isValid = moduleFileExplorer.Upload(openFileDialog.FileName);
                btnFilesDownload.IsEnabled = !isValid;
                btnFilesUpload.IsEnabled = !isValid;
                if (isValid)
                    txtFiles.Text = "Starting upload: " + openFileDialog.FileName;
            }
        }

        private void btnFilesFolderCreate_Click(object sender, RoutedEventArgs e) {
            if (moduleFileExplorer == null)
                return;

            WindowRegistryKey wrk = new WindowRegistryKey();
            wrk.Owner = window;
            bool accept = (bool)wrk.ShowDialog();
            if (accept) {
                moduleFileExplorer.CreateFolder(wrk.ReturnName);
            }
        }

        private void btnFilesFolderRename_Click(object sender, RoutedEventArgs e) {
            if (moduleFileExplorer == null)
                return;
            if (listFilesFolders.SelectedIndex > -1) {
                string selectedkey = listFilesFolders.SelectedItem.ToString();

                WindowRegistryKey wrk = new WindowRegistryKey(selectedkey);
                wrk.Owner = window;
                bool accepted = (bool)wrk.ShowDialog();
                if (accepted) {
                    moduleFileExplorer.RenameFileOrFolder(selectedkey, wrk.ReturnName);
                }
            }
        }

        private void btnFilesFolderDelete_Click(object sender, RoutedEventArgs e) {
            if (moduleFileExplorer == null || !(bool)chkFilesEnableDelete.IsChecked)
                return;

            chkFilesEnableDelete.IsChecked = false;

            string selectedkey = listFilesFolders.SelectedItem.ToString();

            MessageBox.Show("To delete key '" + selectedkey + "' you must type it exactly in the next window.");

            WindowRegistryKey wrk = new WindowRegistryKey();
            wrk.Owner = window;
            bool accepted = (bool)wrk.ShowDialog();
            if (accepted) {
                if (wrk.ReturnName == selectedkey) {
                    KLCFile lookup = moduleFileExplorer.GetKLCFolder(selectedkey);
                    moduleFileExplorer.DeleteFolder(lookup);
                } else
                    MessageBox.Show("Did not delete key '" + selectedkey + "'.");
            }
        }

        private void btnFilesFileRename_Click(object sender, RoutedEventArgs e) {
            if (moduleFileExplorer == null || dgvFilesFiles.SelectedItem == null)
                return;

            string valueNameOld = ((System.Data.DataRowView)dgvFilesFiles.SelectedItem).Row.ItemArray[0].ToString();

            if (valueNameOld != null && valueNameOld.Length > 0) {
                WindowRegistryKey wrk = new WindowRegistryKey(valueNameOld);
                wrk.Owner = window;
                bool accepted = (bool)wrk.ShowDialog();
                if (accepted)
                    moduleFileExplorer.RenameFileOrFolder(valueNameOld, wrk.ReturnName);
            }
        }

        private void btnFilesFileDelete_Click(object sender, RoutedEventArgs e) {
            if (moduleFileExplorer == null || !(bool)chkFilesEnableDelete.IsChecked || dgvFilesFiles.SelectedItem == null)
                return;

            chkFilesEnableDelete.IsChecked = false;

            string selectedFile = ((System.Data.DataRowView)dgvFilesFiles.SelectedItem).Row.ItemArray[0].ToString();
            KLCFile lookup = moduleFileExplorer.GetKLCFile(selectedFile);

            MessageBoxResult result = MessageBox.Show(lookup.Name, "Delete file?", MessageBoxButton.YesNo);
            if(result == MessageBoxResult.Yes)
                moduleFileExplorer.DeleteFile(lookup);
        }

        private void chkFilesEnableDelete_Checked(object sender, RoutedEventArgs e) {
            btnFilesFolderDelete.IsEnabled = (bool)chkFilesEnableDelete.IsChecked;
            btnFilesFileDelete.IsEnabled = (bool)chkFilesEnableDelete.IsChecked;
        }

        private void chkFilesEnableDelete_Unchecked(object sender, RoutedEventArgs e) {
            btnFilesFolderDelete.IsEnabled = (bool)chkFilesEnableDelete.IsChecked;
            btnFilesFileDelete.IsEnabled = (bool)chkFilesEnableDelete.IsChecked;
        }

        private void txtFilesPath_PreviewKeyDown(object sender, KeyEventArgs e) {
            if (moduleFileExplorer == null)
                return;

            if (e.Key == Key.Enter) {
                moduleFileExplorer.GoTo(txtFilesPath.Text);
                e.Handled = true;
            }
        }
    }
}
