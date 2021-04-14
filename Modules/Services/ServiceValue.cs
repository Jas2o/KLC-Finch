namespace KLC_Finch.Modules {
    public class ServiceValue {

        public int ServiceStatus;
        public string DisplayName;
        public string ServiceName;
        public string Description;
        public string StartupType;
        public string StartName;

        public ServiceValue(dynamic s) {
            ServiceStatus = (int)s["ServiceStatus"];
            DisplayName = (string)s["DisplayName"];
            ServiceName = (string)s["ServiceName"];
            Description = (string)s["Description"];
            StartupType = (string)s["StartupType"];
            StartName = (string)s["StartName"];
        }
    }
}