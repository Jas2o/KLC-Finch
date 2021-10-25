using System;
using System.Collections.Generic;
using System.IO;

namespace KLC_Finch {
    public class UploadRC {
        public List<string> Path { get; private set; }
        public string fileName { get; private set; }
        public bool showExplorer { get; private set; }

        private Progress<int> progress;
        private string readLocation;
        private FileStream filestream;
        private long bytesRead;

        public int Chunk;

        public UploadRC(string readLocation, bool showExplorer, Progress<int> progress = null) {
            fileName = System.IO.Path.GetFileName(readLocation);
            this.readLocation = readLocation;
            this.showExplorer = showExplorer;
            this.progress = progress;
            Chunk = 0;

            //filestream = new FileStream(readLocation, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            Console.WriteLine("File upload RC start: " + fileName);
        }

        public bool Open() {
            try {
                filestream = new FileStream(readLocation, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            } catch(Exception) {
                return false;
            }
            return true;
        }

        public long GetFileSize() {
            return new FileInfo(readLocation).Length;
        }

        public byte[] ReadBlock() {
            byte[] data;
            long difference = (filestream.Length - filestream.Position);
            if (difference < 4194304)
                data = new byte[difference];
            else
                data = new byte[4194304];
            filestream.Read(data, 0, data.Length);
            bytesRead += data.Length;

            if (progress != null) {
                int value = (int)((filestream.Position / (Double)filestream.Length) * 100.0);
                ((IProgress<int>)progress).Report(value);
            }

            Console.WriteLine("File upload RC read " + data.Length + " bytes");
            return data;
        }

        public void Close() {
            filestream.Close();
            Console.WriteLine("File upload RC complete: " + fileName + "(" + bytesRead + " bytes)");
            if (progress != null) {
                ((IProgress<int>)progress).Report(100);
            }
        }
    }
}
