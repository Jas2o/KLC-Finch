using System;
using System.Collections.Generic;
using System.IO;

namespace KLC_Finch {
    public class Download {
        public List<string> Path { get; private set; }
        public string fileName { get; private set; }
        public string saveLocation { get; private set; }
        public long fileID { get; private set; }
        public string type { get; private set; }

        private FileStream filestream;
        private long bytesWritten;

        public Download(List<string> remotePath, string fileName, string saveLocation, string type) {
            this.Path = remotePath;
            this.fileName = fileName;
            this.saveLocation = saveLocation;
            this.type = type;
        }

        public void GenFileId() {
            fileID = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        public long GetCurrentSize() {
            return filestream.Length;
        }

        public void Open() {
            filestream = new FileStream(saveLocation, FileMode.Create);
            Console.WriteLine("File download start: " + fileName);
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