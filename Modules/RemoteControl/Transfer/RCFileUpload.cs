using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace KLC_Finch {
    public class UploadRC : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        //public List<string> Path { get; private set; }
        public string fileName { get; private set; }
        public string readLocation { get; private set; }

        private FileStream filestream;
        private long bytesRead;
        public long bytesExpected { get; private set; }
        public int Percentage { get; private set; }
        //public string Status { get; set; }
        public int Chunk;

        public UploadRC(string readLocation) {
            fileName = System.IO.Path.GetFileName(readLocation);
            this.readLocation = readLocation;
            bytesExpected = new FileInfo(readLocation).Length;
            Status = "Queued";
            Chunk = 0;

            //filestream = new FileStream(readLocation, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            Console.WriteLine("File upload RC start: " + fileName);
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

        public bool Open() {
            try {
                filestream = new FileStream(readLocation, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                Status = "Uploading...";
            } catch(Exception) {
                return false;
            }
            return true;
        }

        /*
        public long GetFileSize() {
            return new FileInfo(readLocation).Length;
        }
        */

        public byte[] ReadBlock() {
            if (filestream == null)
                throw new NullReferenceException();

            byte[] data;
            long difference = (filestream.Length - filestream.Position);
            if (difference < 4194304)
                data = new byte[difference];
            else
                data = new byte[4194304];
            filestream.Read(data, 0, data.Length);
            bytesRead += data.Length;

            Percentage = (int)((filestream.Position / (Double)bytesExpected) * 100.0);

            Console.WriteLine("File upload RC read " + data.Length + " bytes");
            return data;
        }

        public void Close() {
            Status = "Uploaded";
            filestream.Close();
            Console.WriteLine("File upload RC complete: " + fileName + "(" + bytesRead + " bytes)");
            Percentage = 100;
        }
    }
}
