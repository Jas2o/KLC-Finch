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
                //https://docs.microsoft.com/en-us/dotnet/api/system.serviceprocess.servicecontrollerstatus?view=dotnet-plat-ext-5.0
                switch (ServiceStatus) {
                    case 1:
                        return "";
                    case 2:
                        return "Starting...";
                    case 3:
                        return "Stopping...";
                    case 4:
                        return "Running";
                    case 5:
                        return "CONT_PENDING";
                    case 6:
                        return "PAUSE_PENDING";
                    case 7:
                        return "PAUSED";
                    default:
                        return "UNKNOWN " + ServiceStatus;
                }
            }
        }

        public StatusColours StatusColour {
            get {
                switch (ServiceStatus) {
                    case 1:
                        //Stopped
                        if(StartupType == "Automatic")
                            return StatusColours.Red;
                        else
                            return StatusColours.None;
                    case 2:
                        return StatusColours.Yellow; //Start Pending
                    case 3:
                        return StatusColours.Yellow; //Stop Pending
                    case 4:
                        //KLC shows this as green.
                        return StatusColours.None; //Running
                    case 5:
                        return StatusColours.Yellow; //CONT PENDING
                    case 6:
                        return StatusColours.Yellow; //PAUSE PENDING
                    case 7:
                        return StatusColours.Purple; //SERVICE PAUSED
                    default:
                        return StatusColours.Purple; //Unknown
                }
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

        public enum StatusColours {
            None,
            Red, //Not running when automatic
            Yellow, //Pending
            Green,
            Purple //Unexpected
        }
    }
}