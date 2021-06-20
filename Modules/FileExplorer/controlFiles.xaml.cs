﻿using KLC_Finch.Modules.Registry;
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
            if (moduleFileExplorer == null)
                return;

            string selectedkey = listFilesFolders.SelectedItem.ToString();
            KLCFile lookup = moduleFileExplorer.GetKLCFolder(selectedkey);
            if (lookup == null)
                return;

            //--

            using (TaskDialog dialog = new TaskDialog()) {
                dialog.WindowTitle = "KLC-Finch: Folder";
                dialog.MainInstruction = "Delete folder?";
                dialog.MainIcon = TaskDialogIcon.Warning;
                dialog.CenterParent = true;
                dialog.Content = selectedkey;
                dialog.VerificationText = "Confirm";
                dialog.VerificationClicked += DestructiveDialog_VerificationClicked;

                TaskDialogButton tdbDelete = new TaskDialogButton("Delete");
                tdbDelete.Enabled = false;
                TaskDialogButton tdbCancel = new TaskDialogButton(ButtonType.Cancel);
                tdbCancel.Default = true;
                dialog.Buttons.Add(tdbDelete);
                dialog.Buttons.Add(tdbCancel);

                TaskDialogButton button = dialog.ShowDialog(App.alternative);
                if (button == tdbDelete)
                    moduleFileExplorer.DeleteFolder(lookup);
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
            if (moduleFileExplorer == null || dgvFilesFiles.SelectedItems.Count == 0)
                return;

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < dgvFilesFiles.SelectedItems.Count; i++) {
                KLCFile lookup = (KLCFile)dgvFilesFiles.SelectedItems[i];
                if(i == 0)
                    sb.Append(lookup.Name);
                else
                    sb.Append(", " + lookup.Name);
            }

            using (TaskDialog dialog = new TaskDialog()) {
                string word = (dgvFilesFiles.SelectedItems.Count == 1 ? "file" : "files");
                dialog.WindowTitle = "KLC-Finch: Files";
                dialog.MainInstruction = string.Format("Delete {0} {1}?", dgvFilesFiles.SelectedItems.Count, word);
                dialog.MainIcon = TaskDialogIcon.Warning;
                dialog.CenterParent = true;
                dialog.Content = sb.ToString();
                dialog.VerificationText = "Confirm";
                dialog.VerificationClicked += DestructiveDialog_VerificationClicked;

                TaskDialogButton tdbDelete = new TaskDialogButton("Delete");
                tdbDelete.Enabled = false;
                TaskDialogButton tdbCancel = new TaskDialogButton(ButtonType.Cancel);
                tdbCancel.Default = true;
                dialog.Buttons.Add(tdbDelete);
                dialog.Buttons.Add(tdbCancel);

                TaskDialogButton button = dialog.ShowDialog(App.alternative);
                if (button == tdbDelete) {
                    foreach (object selectedItem in dgvFilesFiles.SelectedItems) {
                        KLCFile lookup = (KLCFile)selectedItem;
                        moduleFileExplorer.DeleteFile(lookup);
                    }
                }
            }
        }

        private void DestructiveDialog_VerificationClicked(object sender, EventArgs e) {
            TaskDialog td = (TaskDialog)sender;
            td.Buttons[0].Enabled = td.IsVerificationChecked;
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
