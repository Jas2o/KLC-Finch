using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace KLC_Finch {
    public class DownloadRC : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        //public List<string> Path { get; private set; }
        public string fileName { get; private set; }
        public string writeLocation { get; private set; }

        private FileStream filestream;
        private long bytesWritten;
        public long bytesExpected { get; private set; }
        public int Percentage { get; private set; }
        //public string Status { get; set; }

        public int Chunk;

        public DownloadRC() {
            Chunk = 0;
        }

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private string status;
        public string Status
        {
            get { return status; }
            set
            {
                status = value;
                NotifyPropertyChanged("Status");
            }
        }

        public void Start(string dName, long dSize)
        {
            fileName = dName;
            bytesExpected = dSize;
            Status = "Queued";

            string folder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\KRCTransferFiles\\";
            Directory.CreateDirectory(folder);

            filestream = new FileStream(folder + fileName, FileMode.Create, FileAccess.Write, FileShare.None);
            writeLocation = filestream.Name;
            Console.WriteLine("File download RC: " + dName);
        }

        public long GetFileSize() {
            return new FileInfo(writeLocation).Length;
        }

        public void WriteBlock(byte[] block)
        {
            Status = "Downloading...";

            filestream.Write(block, 0, block.Length);
            bytesWritten += block.Length;

            Percentage = (int)((filestream.Position / (Double)bytesExpected) * 100.0);
        }

        public void Close() {
            Status = "Downloaded";
            filestream.Close();
            Console.WriteLine("File download RC complete: " + fileName + "(" + bytesWritten + " bytes)");
            Percentage = 100;
        }

    }
}
