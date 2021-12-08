using System;

namespace KLC_Finch {
    public class ToolboxValue : IComparable {

        public string NameDisplay { get; private set; }
        public string NameActual { get; private set; }
        public int Size { get; private set; }
        public DateTime LastUploadTime { get; private set; }
        public string ParentPath { get; private set; }
        //isFile and Attributes left out

        public ToolboxValue(dynamic v) {
            NameActual = (string)v["Name"];
            NameDisplay = NameActual.Replace(".99.99.99.99", "");
            Size = (int)v["Size"];
            LastUploadTime = (DateTime)v["LastUploadTime"];
            ParentPath = (string)v["ParentPath"];
        }

        public int CompareTo(object obj) {
            return NameDisplay.CompareTo(((ToolboxValue)obj).NameDisplay);
        }

        public override string ToString() {
            return NameDisplay;
        }

    }
}
