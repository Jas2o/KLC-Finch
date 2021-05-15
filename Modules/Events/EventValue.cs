using System;

namespace KLC_Finch.Modules {
    public class EventValue : IComparable {

        public string SourceName { get; private set; } //Microsoft-Windows-Security-SPP
        public uint IdRaw { get; private set; } //Actually a number, but I don't trust Kaseya - 1073758208,
        public int EventType { get; private set; } //4
        public string LogType { get; private set; } //Application
        public string Category { get; private set; } //None
        public string EventMessage { get; private set; } //Successfully scheduled Software Protection service for re-start at 2121-03-20T00:10:22Z. Reason: RulesEngine.
        public DateTime EventGeneratedTime { get; private set; } //2021-04-13T00:10:22.77Z
        public int RecordNumber { get; private set; } //3767
        public string User { get; private set; } //N/A
        public string Computer { get; private set; } //NB.company.com.au
        
        public int EventId { get; private set; }
        public int EventQualifiers { get; private set; }
        public string EventTypeDisplay {
            get {
                switch (EventType) {
                    case 0:
                        return "Success";
                    case 1:
                        return "Error";
                    case 2:
                        return "Warning";
                    case 4:
                        return "Information";
                }
                return EventType.ToString();
            }
        }

        public EventValue(dynamic e) {
            SourceName = (string)e["sourceName"];
            IdRaw = (uint)e["id"];
            EventType = (int)e["eventType"];
            LogType = (string)e["logType"];
            Category = (string)e["category"];
            EventMessage = (string)e["eventMessage"];
            EventGeneratedTime = ((DateTime)e["eventGeneratedTime"]).ToLocalTime();
            RecordNumber = (int)e["recordNumber"];
            User = (string)e["user"];
            Computer = (string)e["computer"];

            EventId = (int)(IdRaw & 0xFFFF);
            EventQualifiers = (int)((IdRaw >> 16) & 0xFFFF);
        }

        public int CompareTo(object obj) {
            return EventId.CompareTo(((EventValue)obj).EventId);
        }

        public override string ToString() {
            return SourceName;
        }

        /*
         _getEventTypeIcon: function(e) {
			switch (this._getTypeString(e)) {
			case "ET_0":
				return "icons:check-circle";
			case "ET_1":
				return "report";
			case "ET_2":
				return "warning";
			case "ET_4":
				return "info";
			case "ET_8":
				return "assignment-turned-in";
			case "ET_16":
				return "assignment-late";
			default:
				return "icons:help"
			}
        _getIconClass: function(e) {
			switch (this._getTypeString(e)) {
			case "ET_1":
			case "ET_16":
				return "icon-red";
			case "ET_0":
			case "ET_8":
				return "icon-green";
			case "ET_2":
				return "icon-yellow";
			case "ET_4":
				return "icon-blue";
			default:
				return ""
			}
		},
		},
        */
    }
}
