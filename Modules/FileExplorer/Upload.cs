using System;
using System.Collections.Generic;
using System.IO;

namespace KLC_Finch {
    class Upload {
        public List<string> Path;
        public string fileName;
        public long fileID;
        public string type;

        private string readLocation;
        private FileStream filestream;
        private long bytesRead;

        public Upload(List<string> remotePath, string fileName, string readLocation, long fileID, string type) {
            this.Path = remotePath;
            this.fileName = fileName;
            this.fileID = fileID;
            this.type = type;
            this.readLocation = readLocation;

            //filestream = new FileStream(readLocation, FileMode.Open);
            Console.WriteLine("File upload start: " + fileName);
        }

        public void Open() {
            filestream = new FileStream(readLocation, FileMode.Open);
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

            Console.WriteLine("File upload read " + data.Length + " bytes");
            return data;
        }

        public void Close() {
            filestream.Flush();
            filestream.Close();

            Console.WriteLine("File upload complete: " + fileName + "(" + bytesRead + " bytes)");
        }
    }
}
