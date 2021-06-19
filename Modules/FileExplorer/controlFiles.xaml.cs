using KLC_Finch.Modules.Registry;
using Microsoft.Win32;
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

namespace KLC_Finch {
    /// <summary>
    /// Interaction logic for controlFiles.xaml
    /// </summary>
    public partial class controlFiles : UserControl {

        private WindowAlternative window;
        private FileExplorer moduleFileExplorer;
        private Modules.FilesData filesData;

        public controlFiles() {
            filesData = new Modules.FilesData();
            this.DataContext = filesData;
            InitializeComponent();
        }

        private void btnFilesPathUp_Click(object sender, RoutedEventArgs e) {
            if (moduleFileExplorer != null)
                moduleFileExplorer.GoUp();
        }

        public void btnFilesPathJump_Click(object sender, RoutedEventArgs e) {
            if (moduleFileExplorer != null)
                moduleFileExplorer.GoTo(txtFilesPath.Text);
        }

        private void btnFilesStart_Click(object sender, RoutedEventArgs e) {
            KLC.LiveConnectSession session = ((WindowAlternative)Window.GetWindow(this)).session;
            if (session != null && session.WebsocketB.ControlAgentIsReady()) {
                btnFilesStart.IsEnabled = false;
                btnFilesFolderDelete.IsEnabled = false;
                btnFilesFileDelete.IsEnabled = false;
                window = ((WindowAlternative)Window.GetWindow(this));

                moduleFileExplorer = new FileExplorer(session);
                moduleFileExplorer.LinkToUI(listFilesFolders, txtFilesPath, progressBar, progressText, btnFilesDownload, btnFilesUpload, filesData);
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
            if (moduleFileExplorer == null || dgvFilesFiles.SelectedItems.Count == 0)
                return;

            if(dgvFilesFiles.SelectedItems.Count == 1) {
                string selectedFile = ((KLCFile)dgvFilesFiles.SelectedItem).Name;

                if (selectedFile != null && selectedFile.Length > 0) {
                    SaveFileDialog saveFileDialog = new SaveFileDialog();
                    saveFileDialog.FileName = selectedFile;
                    if (saveFileDialog.ShowDialog() == true)
                        moduleFileExplorer.Download(selectedFile, saveFileDialog.FileName);
                }
            } else {
                VistaFolderBrowserDialog folderDialog = new VistaFolderBrowserDialog();
                if (folderDialog.ShowDialog() == true) {
                    foreach (object selectedItem in dgvFilesFiles.SelectedItems) {
                        string selectedFile = ((KLCFile)selectedItem).Name;

                        if (selectedFile != null && selectedFile.Length > 0)
                            moduleFileExplorer.Download(selectedFile, folderDialog.SelectedPath + "\\" + selectedFile);
                    }
                }
            }
        }

        private void btnFilesUpload_Click(object sender, RoutedEventArgs e) {
            if (moduleFileExplorer == null || moduleFileExplorer.GetSelectedPathLength() == 0)
                return;

            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Multiselect = true;
            if (openFileDialog.ShowDialog() == true) {
                foreach (string fileName in openFileDialog.FileNames)
                    moduleFileExplorer.Upload(fileName);
                //btnFilesDownload.IsEnabled = !isValid;
                //btnFilesUpload.IsEnabled = !isValid;
                //if (isValid) txtFiles.Text = "Starting upload: " + openFileDialog.FileName;
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
                KLCFile lookup = moduleFileExplorer.GetKLCFolder(selectedkey);
                if (lookup == null)
                    return;

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
            KLCFile lookup = moduleFileExplorer.GetKLCFolder(selectedkey);
            if (lookup == null)

            MessageBox.Show("To delete key '" + selectedkey + "' you must type it exactly in the next window.");

            WindowRegistryKey wrk = new WindowRegistryKey();
            wrk.Owner = window;
            bool accepted = (bool)wrk.ShowDialog();
            if (accepted) {
                if (wrk.ReturnName == selectedkey)
                    moduleFileExplorer.DeleteFolder(lookup);
                else
                    MessageBox.Show("Did not delete key '" + selectedkey + "'.");
            }
        }

        private void btnFilesFileRename_Click(object sender, RoutedEventArgs e) {
            if (moduleFileExplorer == null || dgvFilesFiles.SelectedItem == null)
                return;

            string valueNameOld = ((KLCFile)dgvFilesFiles.SelectedItem).Name;

            if (valueNameOld != null && valueNameOld.Length > 0) {
                WindowRegistryKey wrk = new WindowRegistryKey(valueNameOld);
                wrk.Owner = window;
                bool accepted = (bool)wrk.ShowDialog();
                if (accepted)
                    moduleFileExplorer.RenameFileOrFolder(valueNameOld, wrk.ReturnName);
            }
        }

        private void btnFilesFileDelete_Click(object sender, RoutedEventArgs e) {
            if (moduleFileExplorer == null || !(bool)chkFilesEnableDelete.IsChecked || dgvFilesFiles.SelectedItems.Count == 0)
                return;

            chkFilesEnableDelete.IsChecked = false;

            if (dgvFilesFiles.SelectedItems.Count == 1) {
                KLCFile lookup = (KLCFile)dgvFilesFiles.SelectedItem;

                MessageBoxResult result = MessageBox.Show(lookup.Name, "Delete file?", MessageBoxButton.YesNo);
                if (result == MessageBoxResult.Yes)
                    moduleFileExplorer.DeleteFile(lookup);
            } else {
                StringBuilder sb = new StringBuilder();

                for(int i = 0; i < dgvFilesFiles.SelectedItems.Count; i++) {
                    KLCFile lookup = (KLCFile)dgvFilesFiles.SelectedItems[i];
                    sb.AppendLine(lookup.Name);

                    if(i == 9 && dgvFilesFiles.SelectedItems.Count != 10) {
                        sb.AppendLine("... and " + (dgvFilesFiles.SelectedItems.Count - (i+1)) + " more.");
                        break;
                    }
                }

                MessageBoxResult result = MessageBox.Show(sb.ToString(), "Delete " + dgvFilesFiles.SelectedItems.Count + " files?", MessageBoxButton.YesNo);
                if (result == MessageBoxResult.Yes) {
                    foreach (object selectedItem in dgvFilesFiles.SelectedItems) {
                        KLCFile lookup = (KLCFile)selectedItem;
                        moduleFileExplorer.DeleteFile(lookup);
                    }
                }
            }
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

        private void btnFilesFolderDownload_Click(object sender, RoutedEventArgs e) {
            if (moduleFileExplorer == null)
                return;
            if (listFilesFolders.SelectedIndex > -1) {
                string selectedkey = listFilesFolders.SelectedItem.ToString();
                KLCFile lookup = moduleFileExplorer.GetKLCFolder(selectedkey);
                if (lookup == null)
                    return;

                VistaFolderBrowserDialog folderDialog = new VistaFolderBrowserDialog();
                if (folderDialog.ShowDialog() == true)
                    moduleFileExplorer.DownloadFolder(lookup, folderDialog.SelectedPath);
            }
        }

        private void dgvFilesFiles_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e) {
            if(dgvFilesFiles.SelectedItems.Count == 1) {
                btnFilesFileRename.IsEnabled = true;
                txtFilesSelected.Content = "1 file selected";
            } else {
                btnFilesFileRename.IsEnabled = false;
                txtFilesSelected.Content = dgvFilesFiles.SelectedItems.Count + " files selected";
            }
        }
    }
}
