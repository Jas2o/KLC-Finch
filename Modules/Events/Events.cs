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

        private static string modulename = "events";
        private IWebSocketConnection serverB;

        private DataGrid dgvEventsValues;
        private TextBox txtBox;
        private ComboBox cmbLogTypes, cmbLogTypesExtended;

        private string lastLogType;
        private List<EventValue> listEventValue;
        //private int scan = -1;

        public Events(KLC.LiveConnectSession session, DataGrid dgvEventsValues, TextBox txtBox = null, ComboBox cmbLogTypes=null, ComboBox cmbLogTypesExtended=null) {
            this.dgvEventsValues = dgvEventsValues;
            this.txtBox = txtBox;
            this.cmbLogTypes = cmbLogTypes;
            this.cmbLogTypesExtended = cmbLogTypesExtended;

            listEventValue = new List<EventValue>();

            if (session != null)
                session.WebsocketB.ControlAgentSendTask(modulename);
        }

        public void SetSocket(IWebSocketConnection ServerBsocket) {
            this.serverB = ServerBsocket;
        }

        public void Receive(string message) {
            txtBox.Dispatcher.Invoke(new Action(() => {
                dynamic temp = JsonConvert.DeserializeObject(message);
                string something = (string)temp["action"];
                switch (temp["action"].ToString()) {
                    case "ScriptReady":
                        JObject jStartEventsData = new JObject();
                        jStartEventsData["action"] = "GetLogTypes";
                        serverB.Send(jStartEventsData.ToString());

                        cmbLogTypesExtended.Items.Clear();
                        foreach (string key in EventsExtendedList.Items)
                            cmbLogTypesExtended.Items.Add(key);

                        break;
                    case "GetLogTypes":
                        cmbLogTypes.Items.Clear();
                        if (temp["logs"] != null) {
                            List<string> listTypes = new List<string>();
                            foreach (dynamic key in temp["logs"].Children())
                                listTypes.Add(key.ToString());

                            listTypes.Sort();
                            cmbLogTypes.ItemsSource = listTypes;
                        }

                        //cmbLogTypes.SelectedIndex = 0;
                        cmbLogTypes.SelectedValue = "Application";
                        break;
                    case "setLogType": //Valid
                    case "SetLogType": //Errors
                    case "GetEvents":
                        if (temp["events"] != null) {
                            if (temp["action"].ToString() != "GetEvents") {
                                listEventValue.Clear();
                                txtBox.Text = "";
                            }

                            //if (scan > -1) Console.WriteLine(lastLogType);

                            foreach (dynamic e in temp["events"].Children()) {
                                //txtBox.AppendText(e.ToString() + "\r\n\r\n");
                                EventValue ev = new EventValue(e);
                                listEventValue.Add(ev);
                            }

                            UpdateDisplayValues();
                        } else if (temp["errors"] != null) {
                            txtBox.Text = temp["errors"].ToString();
                        }

                        //if (scan > -1) Scan();
                        break;

                    default:
                        txtBox.AppendText("Events message received: " + message + "\r\n\r\n");
                        break;
                }
            }));
        }

        private void UpdateDisplayValues() {
            dgvEventsValues.DataContext = null;

            DataTable dt = new DataTable();
            dt.Columns.Add("SourceName", typeof(string));
            dt.Columns.Add("Id", typeof(string));
            dt.Columns.Add("EventType", typeof(int));
            dt.Columns.Add("LogType", typeof(string));
            dt.Columns.Add("Category", typeof(string));
            dt.Columns.Add("EventMessage", typeof(string));
            dt.Columns.Add("EventGeneratedTime", typeof(DateTime));
            dt.Columns.Add("RecordNumber", typeof(int));
            dt.Columns.Add("User", typeof(string));
            dt.Columns.Add("Computer", typeof(string));

            foreach (EventValue value in listEventValue) {
                DataRow row = dt.NewRow();
                row[0] = value.SourceName;
                row[1] = value.Id;
                row[2] = value.EventType;
                row[3] = value.LogType;
                row[4] = value.Category;
                row[5] = value.EventMessage;
                row[6] = value.EventGeneratedTime;
                row[7] = value.RecordNumber;
                row[8] = value.User;
                row[9] = value.Computer;
                dt.Rows.Add(row);
            }

            dgvEventsValues.DataContext = dt;
            //dgvEventsValues.AutoResizeColumns();
            //dgvEventsValues.Sort(dgvEventsValues.Columns[0], System.ComponentModel.ListSortDirection.Ascending);
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

            JObject jEvent = new JObject();
            jEvent["action"] = "SetLogType";
            jEvent["logType"] = logType;
            jEvent["numEvents"] = 200;
            jEvent["direction"] = "NewerFirst";
            serverB.Send(jEvent.ToString());
        }

        public void Refresh() {
            SetLogType(lastLogType);
        }

        public void GetMoreEvents() {
            JObject jEvent = new JObject();
            jEvent["action"] = "GetEvents";
            jEvent["numEvents"] = 25;
            jEvent["logType"] = lastLogType;
            serverB.Send(jEvent.ToString());
        }

    }
}
