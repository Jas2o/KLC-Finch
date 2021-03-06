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
            btnFilesStart.IsEnabled = false;

            KLC.LiveConnectSession session = ((WindowAlternative)Window.GetWindow(this)).session;
            if (session != null) {
                moduleFileExplorer = new FileExplorer(session, listFilesFolders, listFilesFiles, txtFilesPath, txtFiles, progressBar, progressText, btnFilesDownload, btnFilesUpload);
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
            if(listFilesFiles.SelectedIndex > -1) {
                string selectedFile = listFilesFiles.SelectedItem.ToString();

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
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if(openFileDialog.ShowDialog() == true) {
                btnFilesDownload.IsEnabled = false;
                btnFilesUpload.IsEnabled = false;
                moduleFileExplorer.Upload(openFileDialog.FileName);
            }
        }
    }
}
