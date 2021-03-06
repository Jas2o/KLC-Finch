using System;
using System.Collections.Generic;
using System.IO;

namespace KLC_Finch {
    public class Download {
        public List<string> Path;
        public string fileName;
        public long fileID;
        public string type;

        private FileStream filestream;
        private long bytesWritten;

        public Download(List<string> remotePath, string fileName, string saveLocation, long fileID, string type) {
            this.Path = remotePath;
            this.fileName = fileName;
            this.fileID = fileID;
            this.type = type;

            filestream = new FileStream(saveLocation, FileMode.Create);
            Console.WriteLine("File download start: " + fileName);
        }

        public long GetCurrentSize() {
            return filestream.Length;
        }

        public void WriteData(byte[] data) {
            filestream.Write(data, 0, data.Length);
            bytesWritten += data.Length;

            Console.WriteLine("File download wrote " + data.Length + " bytes");
        }

        public void Close() {
            filestream.Flush();
            filestream.Close();

            Console.WriteLine("File download complete: " + fileName + "(" + bytesWritten + " bytes)");
        }
    }
}