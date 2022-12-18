using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;

namespace KLC_Finch.Modules.RemoteControl.Transfer
{
    public class RCFile : INotifyPropertyChanged
    {
        //No download queue because Kaseya does not tell us
        public readonly Queue<UploadRC> queueUpload;

        public DownloadRC activeDownload;

        public UploadRC activeUpload;

        public long DownloadTotalSize;

        public long DownloadTotalWritten;

        public long UploadQueueSize;

        private string progressTextDownload;

        private string progressTextUpload;

        private int progressValueDownload;

        private int progressValueUpload;
        public WinRCFileTransfer.WinRCFileCallback Callback;

        public RCFile(bool IsMac)
        {
            queueUpload = new Queue<UploadRC>();
            HistoryDownload = new ObservableCollection<DownloadRC>();
            HistoryUpload = new ObservableCollection<UploadRC>();

            if (IsMac)
                RemoteDestination = "/Library/Kaseya/kworking/KRCTransferFiles/";
            else
                RemoteDestination = "C:\\kworking\\KRCTransferFiles\\";
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public ObservableCollection<DownloadRC> HistoryDownload { get; set; }
        public ObservableCollection<UploadRC> HistoryUpload { get; set; }

        public string RemoteDestination { get; private set; }

        public string ProgressTextDownload
        {
            get { return progressTextDownload; }
            set
            {
                progressTextDownload = value;
                NotifyPropertyChanged("ProgressTextDownload");
            }
        }

        public string ProgressTextUpload
        {
            get { return progressTextUpload; }
            set
            {
                progressTextUpload = value;
                NotifyPropertyChanged("ProgressTextUpload");
            }
        }

        public int ProgressValueDownload
        {
            get { return progressValueDownload; }
            set
            {
                progressValueDownload = value;
                NotifyPropertyChanged("ProgressValueDownload");
            }
        }

        public int ProgressValueUpload
        {
            get { return progressValueUpload; }
            set
            {
                progressValueUpload = value;
                NotifyPropertyChanged("ProgressValueUpload");
            }
        }

        public void DownloadCancelled()
        {
            if (activeDownload != null)
            {
                activeDownload.Status = "Download cancelled.";

                //queueDownload.Clear();
                activeDownload.Close();
                System.IO.File.Delete(activeDownload.writeLocation);
                activeDownload = null;
            }

            ProgressValueDownload = 0;
            ProgressTextDownload = "";
        }

        public void DownloadChunk(byte[] remaining)
        {
            if (activeDownload == null)
                return;

            try
            {
                activeDownload.WriteBlock(remaining);
                DownloadTotalWritten += remaining.Length;

                StringBuilder totalSb = new StringBuilder(32);
                FormatKbSizeConverter.StrFormatByteSizeW(activeDownload.bytesExpected, totalSb, totalSb.Capacity);

                ProgressValueDownload = activeDownload.Percentage;
                ProgressTextDownload = activeDownload.Percentage + "% of " + totalSb.ToString();
            }
            catch (Exception)
            {
                //Probably got cancelled
            }
        }

        public void FileTransferDownloadComplete()
        {
            if (activeDownload != null)
            {
                activeDownload.Close();
                System.Diagnostics.Process.Start("explorer.exe", "/select, \"" + activeDownload.writeLocation + "\"");
                activeDownload = null;
            }
            ProgressValueDownload = 0;
            ProgressTextDownload = "";

            Callback.Invoke(0); //Close
        }

        public void FileTransferUploadComplete()
        {
            ProgressValueUpload = 0;
            ProgressTextUpload = "";

            Callback.Invoke(0); //Close
        }
        public void HistoryAddDownload()
        {
            App.Current.Dispatcher.Invoke((Action)delegate
            {
                HistoryDownload.Add(activeDownload);
            });
        }

        public void HistoryAddUpload(UploadRC urc)
        {
            App.Current.Dispatcher.Invoke((Action)delegate
            {
                HistoryUpload.Add(urc);
            });
        }

        public void HistoryClear()
        {
            App.Current.Dispatcher.Invoke((Action)delegate
            {
                HistoryDownload.Clear();
                HistoryUpload.Clear();
            });
        }

        public void HistoryClearDownload()
        {
            if (activeDownload != null)
                return;

            App.Current.Dispatcher.Invoke((Action)delegate
            {
                HistoryDownload.Clear();
            });
        }

        public void HistoryClearUpload()
        {
            if (activeUpload != null)
                return;

            App.Current.Dispatcher.Invoke((Action)delegate
            {
                HistoryUpload.Clear();
            });
        }

        public void StartDownload(string dName, long dSize)
        {
            if (activeDownload != null)
            {
                activeDownload.Close();
                activeDownload = null;
            }
            activeDownload = new DownloadRC();
            HistoryAddDownload();
            activeDownload.Start(dName, dSize);
            ProgressValueDownload = 0;
            ProgressTextDownload = "";

            Callback.Invoke(1); //Start download
        }

        public void StartUploadUI()
        {
            if (activeUpload == null)
                return;
            ProgressValueUpload = 0;
            ProgressTextUpload = "";
            //HideWhenDone = true;

            Callback.Invoke(2); //Start upload
        }
        public void UploadCancelled()
        {
            if (activeUpload != null)
            {
                activeUpload.Status = "Upload cancelled.";
                queueUpload.Clear();
                activeUpload.Close();
                activeUpload = null;
            }

            ProgressValueUpload = 0;
            ProgressTextUpload = "";
        }

        public void UploadChunkUI()
        {
            if (activeUpload == null)
                return;

            StringBuilder totalSb = new StringBuilder(32);
            FormatKbSizeConverter.StrFormatByteSizeW(activeUpload.bytesExpected, totalSb, totalSb.Capacity);

            ProgressValueUpload = activeUpload.Percentage;
            ProgressTextUpload = activeUpload.Percentage + "% of " + totalSb.ToString();
        }

        /*
        private bool hideWhenDone;
        public bool HideWhenDone
        {
            get { return hideWhenDone; }
            set
            {
                hideWhenDone = value;
                NotifyPropertyChanged("HideWhenDone");
            }
        }
        */

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
