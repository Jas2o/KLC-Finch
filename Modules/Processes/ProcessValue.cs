using System;

namespace KLC_Finch.Modules {
    public class ProcessValue : IComparable {

        public int PID { get; private set; }
        public string DisplayName { get; private set; }
        public string UserName { get; private set; }
        public ulong Memory { get; private set; } //Normally string
        public int CPU { get; private set; } //Normally string, is %
        public int GpuUtilization { get; private set; } //Normally string, is %
        public ulong DiskUtilization { get; private set; } //Normally string, is MB/s
        public string PType { get; private set; } //Background, Windows (no Foreground?)

        public ProcessValue(dynamic p) {
            PID = (int)p["PID"];
            DisplayName = (string)p["DisplayName"];
            UserName = (string)p["UserName"];
            Memory = ulong.Parse((string)p["Memory"]);
            CPU = (int)Math.Ceiling(double.Parse((string)p["CPU"]));

            //2022-11-12
            if (p["GpuUtilization"] != null)
                GpuUtilization = (int)Math.Ceiling(double.Parse((string)p["GpuUtilization"]));
            if (p["DiskUtilization"] != null)
                DiskUtilization = ulong.Parse((string)p["DiskUtilization"]);
            if (p["Type"] != null)
                PType = (string)p["Type"];
        }

        public int CompareTo(object obj) {
            return DisplayName.CompareTo(((ProcessValue)obj).DisplayName);
        }

        public override string ToString() {
            return DisplayName;
        }
    }
}