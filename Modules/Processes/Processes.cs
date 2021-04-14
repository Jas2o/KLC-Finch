﻿using Fleck;
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

        private static string modulename = "processes";
        private IWebSocketConnection serverB;

        private DataGrid dgvProcesses;
        private TextBox txtBox;

        private List<ProcessValue> listProcessValue;

        public Processes(KLC.LiveConnectSession session, DataGrid dgvProcesses, TextBox txtBox = null) {
            this.dgvProcesses = dgvProcesses;
            this.txtBox = txtBox;

            listProcessValue = new List<ProcessValue>();

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
                            listProcessValue.Clear(); //Probably should update what's already there

                            foreach (dynamic p in temp["contentsList"].Children()) {
                                ProcessValue pv = new ProcessValue(p);
                                listProcessValue.Add(pv);
                            }

                            UpdateDisplayValues();
                        }
                        break;

                    default:
                        txtBox.AppendText("Events message received: " + message + "\r\n\r\n");
                        break;
                }
            }));
        }

        public void RequestListProcesses() {
            JObject jStartData = new JObject();
            jStartData["action"] = "ListProcesses";
            serverB.Send(jStartData.ToString());
        }

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

        /*
         * {
  "action": "EndProcess",
  "PID": "6856",
  "displayName": "Portals.exe"
}
        */
    }
}
