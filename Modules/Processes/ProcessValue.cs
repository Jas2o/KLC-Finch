namespace KLC_Finch.Modules {
    public class ProcessValue {

        public int PID;
        public string DisplayName;
        public string UserName;
        public string Memory; //Yep strings...
        public string CPU;

        public ProcessValue(dynamic p) {
            PID = (int)p["PID"];
            DisplayName = (string)p["DisplayName"];
            UserName = (string)p["UserName"];
            Memory = (string)p["Memory"];
            CPU = (string)p["CPU"];
        }
    }
}