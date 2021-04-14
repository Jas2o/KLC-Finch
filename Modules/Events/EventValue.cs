using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KLC_Finch.Modules {
    public class EventValue {

        public string SourceName; //Microsoft-Windows-Security-SPP
        public string Id; //Actually a number, but I don't trust Kaseya - 1073758208,
        public int EventType; //4
        public string LogType; //Application
        public string Category; //None
        public string EventMessage; //Successfully scheduled Software Protection service for re-start at 2121-03-20T00:10:22Z. Reason: RulesEngine.
        public DateTime EventGeneratedTime; //2021-04-13T00:10:22.77Z
        public int RecordNumber; //3767
        public string User; //N/A
        public string Computer; //NB.company.com.au

        public EventValue(dynamic e) {
            SourceName = (string)e["sourceName"];
            Id = (string)e["id"];
            EventType = (int)e["eventType"];
            LogType = (string)e["logType"];
            Category = (string)e["category"];
            EventMessage = (string)e["eventMessage"];
            EventGeneratedTime = (DateTime)e["eventGeneratedTime"];
            RecordNumber = (int)e["recordNumber"];
            User = (string)e["user"];
            Computer = (string)e["computer"];
        }
    }
}
