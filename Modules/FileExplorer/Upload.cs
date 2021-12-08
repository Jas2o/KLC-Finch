using System;
using System.Collections.Generic;
using System.IO;

namespace KLC_Finch {
    class Upload {
        public List<string> Path { get; private set; }
        public string fileName { get; private set; }
        public long fileID { get; private set; }
        public string type { get; private set; }

        private Progress<int> progress;
        private string readLocation;
        private FileStream filestream;
        private long bytesRead;

        public Upload(List<string> remotePath, string fileName, string readLocation, long fileID, string type, Progress<int> progress = null) {
            this.Path = remotePath;
            this.fileName = fileName;
            this.fileID = fileID;
            this.type = type;
            this.readLocation = readLocation;
            this.progress = progress;

            //filestream = new FileStream(readLocation, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            Console.WriteLine("File upload start: " + fileName);
        }

        public void Open() {
            filestream = new FileStream(readLocation, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }

        public long GetFileSize() {
            return new FileInfo(readLocation).Length;
        }

        public byte[] ReadBlock() {
            byte[] data;
            long difference = (filestream.Length - filestream.Position);
            if (difference < 131116)
                data = new byte[difference];
            else
                data = new byte[131116];
            filestream.Read(data, 0, data.Length);
            bytesRead += data.Length;

            if (progress != null) {
                int value = (int)((filestream.Position / (Double)filestream.Length) * 100.0);
                ((IProgress<int>)progress).Report(value);
            }

            Console.WriteLine("File upload read " + data.Length + " bytes");
            return data;
        }

        public void Close() {
            filestream.Close();
            Console.WriteLine("File upload complete: " + fileName + "(" + bytesRead + " bytes)");
            if (progress != null) {
                ((IProgress<int>)progress).Report(100);
            }
        }
    }
}
