using System;

namespace KLC_Finch.Modules {
    public class ProcessValue {

        public int PID { get; private set; }
        public string DisplayName { get; private set; }
        public string UserName { get; private set; }
        public int Memory { get; private set; } //Normally string
        public int CPU { get; private set; } //Normally string

        public ProcessValue(dynamic p) {
            PID = (int)p["PID"];
            DisplayName = (string)p["DisplayName"];
            UserName = (string)p["UserName"];
            Memory = int.Parse((string)p["Memory"]);
            CPU = (int)Math.Ceiling(double.Parse((string)p["CPU"]));
        }
    }
}