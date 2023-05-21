using Fleck;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web;
using System.Windows.Controls;

namespace KLC_Finch.Modules {
    public class Events {

        public const string LabelExtended = "! Extended";
        private static readonly string modulename = "events";
        private IWebSocketConnection serverB;

        private readonly EventsData eventsData;
        private readonly ComboBox cmbLogTypes;
        //private readonly ComboBox cmbLogTypesExtended;

        private string lastLogType;
        //private int scan = -1;
        private WindowAlternative.ErrorCallback CallbackE;

        public Events(KLC.LiveConnectSession session, EventsData eventsData, ComboBox cmbLogTypes=null) {
            CallbackE = session.CallbackE;
            this.eventsData = eventsData;
            this.cmbLogTypes = cmbLogTypes;
            //this.cmbLogTypesExtended = cmbLogTypesExtended;

            if (session != null)
                session.WebsocketB.ControlAgentSendTask(modulename);
        }

        public void SetSocket(IWebSocketConnection ServerBsocket) {
            this.serverB = ServerBsocket;
        }

        public void Receive(string message) {
            dynamic temp = JsonConvert.DeserializeObject(message);
            string something = (string)temp["action"];
            switch (temp["action"].ToString()) {
                case "ScriptReady":
                    JObject jStartEventsData = new JObject { ["action"] = "GetLogTypes" };
                    serverB.Send(jStartEventsData.ToString());
                    break;
                case "GetLogTypes":
                    cmbLogTypes.Dispatcher.Invoke(new Action(() => {
                        cmbLogTypes.Items.Clear();
                        if (temp["logs"] != null) {
                            List<string> listTypes = new List<string> {
                                LabelExtended
                            };
                            foreach (dynamic key in temp["logs"].Children())
                                listTypes.Add(key.ToString());

                            listTypes.Sort();
                            cmbLogTypes.ItemsSource = listTypes;
                        }

                        //cmbLogTypes.SelectedIndex = 0;
                        cmbLogTypes.SelectedValue = "Application";
                        SetLogType("Application");
                    }));
                    break;
                case "setLogType": //Valid
                case "SetLogType": //Errors
                case "GetEvents":
                    if (temp["events"] != null) {
                        if (temp["action"].ToString() != "GetEvents") {
                            eventsData.EventsClear();
                            //txtBox.Text = "";
                        }

                        //if (scan > -1) Console.WriteLine(lastLogType);

                        foreach (dynamic e in temp["events"].Children()) {
                            eventsData.EventsAdd(new EventValue(e));
                        }
                    } else if (temp["errors"] != null) {
                        CallbackE("Events: " + temp["errors"].ToString());
                    }

                    //if (scan > -1) Scan();
                    break;

                default:
                    CallbackE("Events unhandled: " + temp["errors"].ToString());
                    //txtBox.AppendText("Events message received: " + message + "\r\n\r\n");
                    break;
            }
        }

        /*
        public void Scan() {
            if(scan == -1)
                Console.WriteLine("###### SCAN START ######");
            scan++;
            if (scan < EventsExtendedList.Items.Count)
                SetLogType(EventsExtendedList.Items[scan]);
            else
                Console.WriteLine("###### SCAN DONE ######");
        }
        */

        public void SetLogType(string logType) {
            lastLogType = logType;
            /*
            logType = "Microsoft-Windows-PowerShell%4Operational";
            if (logType.Contains("%4"))
                logType = logType.Replace("%4", "/");
            */

            JObject jEvent = new JObject {
                ["action"] = "SetLogType",
                ["logType"] = logType,
                ["numEvents"] = 200,
                ["direction"] = "NewerFirst"
            };
            serverB.Send(jEvent.ToString());
        }

        public void Refresh() {
            SetLogType(lastLogType);
        }

        public void GetMoreEvents() {
            JObject jEvent = new JObject {
                ["action"] = "GetEvents",
                ["numEvents"] = 25,
                ["logType"] = lastLogType
            };
            serverB.Send(jEvent.ToString());
        }

    }
}
