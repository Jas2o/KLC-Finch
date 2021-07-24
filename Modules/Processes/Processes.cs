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
    public class Processes {

        private static readonly string modulename = "processes";
        private IWebSocketConnection serverB;

        private readonly ProcessesData processesData;
        //private DataGrid dgvProcesses;
        //private TextBox txtBox;

        //private List<ProcessValue> listProcessValue;

        public Processes(KLC.LiveConnectSession session, ProcessesData processesData) {
            this.processesData = processesData;
            //this.dgvProcesses = dgvProcesses;
            //this.txtBox = txtBox;
            //listProcessValue = new List<ProcessValue>();

            if (session != null)
                session.WebsocketB.ControlAgentSendTask(modulename);
        }

        public void SetSocket(IWebSocketConnection ServerBsocket) {
            this.serverB = ServerBsocket;
        }

        public void Receive(string message) {
            dynamic temp = JsonConvert.DeserializeObject(message);
            switch (temp["action"].ToString()) {
                case "ScriptReady":
                    RequestListProcesses();
                    break;

                case "ListProcesses":
                case "EndProcess":
                    /*{
                        "action":"ListProcesses",
                        "success":true,
                        "PID":null, //Values of process that admin ended
                        "displayName":null,
                        "contentsList":[
                    */

                    if (temp["contentsList"] != null) {
                        processesData.ProcessesClear();
                        //Probably should update what's already there, but for Processes it's just easier to clear it.

                        foreach (dynamic p in temp["contentsList"].Children()) {
                            ProcessValue pv = new ProcessValue(p);
                            processesData.ProcessesAdd(pv);
                        }
                    }
                    break;

                default:
                    Console.WriteLine("Processes message received: " + message);
                    break;
            }

            /*
            txtBox.Dispatcher.Invoke(new Action(() => {
            }));
            */
        }

        public void RequestListProcesses() {
            JObject jStartData = new JObject {
                ["action"] = "ListProcesses"
            };
            serverB.Send(jStartData.ToString());
        }

        /*
        private void UpdateDisplayValues() {
            dgvProcesses.DataContext = null;

            DataTable dt = new DataTable();
            dt.Columns.Add("DisplayName", typeof(string));
            dt.Columns.Add("PID", typeof(int));
            dt.Columns.Add("UserName", typeof(string));
            dt.Columns.Add("Memory", typeof(string));
            dt.Columns.Add("CPU", typeof(string));

            foreach (ProcessValue value in listProcessValue) {
                DataRow row = dt.NewRow();
                row[0] = value.DisplayName;
                row[1] = value.PID;
                row[2] = value.UserName;
                row[3] = value.Memory;
                row[4] = value.CPU;
                dt.Rows.Add(row);
            }

            dgvProcesses.DataContext = dt;
            //dgvProcesses.AutoResizeColumns();
            //dgvProcesses.Sort(dgvServices.Columns[0], System.ComponentModel.ListSortDirection.Ascending);
        }
        */

        public void EndTask(ProcessValue pv) {
            /* {
              "action": "EndProcess",
              "PID": "6856",
              "displayName": "Portals.exe"
            } */

            JObject jEvent = new JObject {
                ["action"] = "EndProcess",
                ["PID"] = pv.PID.ToString(),
                ["displayName"] = pv.DisplayName
            };
            serverB.Send(jEvent.ToString());
        }

    }
}
