using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KLC_Finch
{
    /// <summary>
    /// Interaction logic for WinRCFileTransfer.xaml
    /// </summary>
    public partial class WinRCFileTransfer : Window
    {
        private IRemoteControl rc;

        public WinRCFileTransfer(IRemoteControl RC)
        {
            InitializeComponent();
            rc = RC;
            this.DataContext = rc.Files;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (rc.IsMac)
                btnAcceptDownload.Content = "Accept (Mac unsupported)";

            rc.Files.Callback = new WinRCFileCallback(RCFileCallback);
        }

        private void RCFileCallback(int action)
        {
            Application.Current.Dispatcher.Invoke((Action)delegate
            {
                switch (action)
                {
                    case 0:
                        this.Close();
                        break;
                    case 1:
                        OnlyShowDownload();
                        break;
                    case 2:
                        OnlyShowUpload();
                        break;
                    default:
                        Debug.WriteLine("Unexpected");
                        break;
                }
            });
        }

        public void OnlyShowDownload()
        {
            expDownload.IsExpanded = true;
            expUpload.IsExpanded = false;
        }

        public void OnlyShowUpload()
        {
            expUpload.IsExpanded = true;
            expDownload.IsExpanded = false;
        }

        private void btnAcceptDownload_Click(object sender, RoutedEventArgs e)
        {
            rc.FileTransferDownload();
        }

        private void btnClearDownloadHistory_Click(object sender, RoutedEventArgs e)
        {
            rc.Files.HistoryClearDownload();
        }

        private void btnClearUploadHistory_Click(object sender, RoutedEventArgs e)
        {
            rc.Files.HistoryClearUpload();
        }

        private void btnCancelDownload_Click(object sender, RoutedEventArgs e)
        {
            rc.FileTransferDownloadCancel();
            rc.Files.DownloadCancelled();
        }

        private void btnCancelUpload_Click(object sender, RoutedEventArgs e)
        {
            rc.FileTransferUploadCancel();
            rc.Files.UploadCancelled();
        }

        private void dgvDownload_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (rc == null || dgvDownload.SelectedItem == null)
                return;

            DownloadRC drc = (DownloadRC)dgvDownload.SelectedItem;
            Process.Start("explorer.exe", string.Format("/select,\"{0}\"", drc.writeLocation));
        }

        private void dgvUpload_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (rc == null || dgvUpload.SelectedItem == null)
                return;

            UploadRC urc = (UploadRC)dgvUpload.SelectedItem;
            Process.Start("explorer.exe", string.Format("/select,\"{0}\"", urc.readLocation));
        }

        private void expDownload_Collapsed(object sender, RoutedEventArgs e)
        {
            if (!expUpload.IsExpanded)
                expUpload.IsExpanded = true;
        }

        private void expUpload_Collapsed(object sender, RoutedEventArgs e)
        {
            if (!expDownload.IsExpanded)
                expDownload.IsExpanded = true;
        }

        private void LblRemoteDest_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Clipboard.SetDataObject(rc.Files.RemoteDestination);
            lblRemoteDest.Foreground = Brushes.MidnightBlue;
        }

        private void lblRemoteDest_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            lblRemoteDest.Foreground = Brushes.Blue;
        }

        private void lblRemoteDest_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            lblRemoteDest.Foreground = Brushes.Black;
        }

        private void LblLocalDest_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Clipboard.SetDataObject("%userprofile%\\Documents\\KRCTransferFiles\\");
            lblLocalDest.Foreground = Brushes.MidnightBlue;
        }

        private void lblLocalDest_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            lblLocalDest.Foreground = Brushes.Blue;
        }

        private void lblLocalDest_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            lblLocalDest.Foreground = Brushes.Black;
        }

        public delegate void WinRCFileCallback(int action);
    }
}
