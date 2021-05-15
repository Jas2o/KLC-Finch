using System;

namespace KLC_Finch.Modules {
    public class ServiceValue : IComparable {

        public int ServiceStatus { get; private set; }
        public string DisplayName { get; private set; }
        public string ServiceName { get; private set; }
        public string Description { get; private set; }
        public string StartupType { get; private set; }
        public string StartName { get; private set; }

        public string StatusDisplay {
            get {
                switch (ServiceStatus) {
                    case 1:
                        return "";
                    case 4:
                        return "Running";
                }
                return ServiceStatus.ToString();
            }
        }

        public ServiceValue(dynamic s) {
            ServiceStatus = (int)s["ServiceStatus"];
            DisplayName = (string)s["DisplayName"];
            ServiceName = (string)s["ServiceName"];
            Description = (string)s["Description"];
            StartupType = (string)s["StartupType"];
            StartName = (string)s["StartName"];
        }

        public int CompareTo(object obj) {
            return DisplayName.CompareTo(((ServiceValue)obj).DisplayName);
        }

        public override string ToString() {
            return DisplayName;
        }
    }
}