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
    public class Services {

        private static string modulename = "services";
        private IWebSocketConnection serverB;

        private DataGrid dgvServices;
        private TextBox txtBox;

        private List<ServiceValue> listServiceValue;

        public Services(KLC.LiveConnectSession session, DataGrid dgvServices, TextBox txtBox = null) {
            this.dgvServices = dgvServices;
            this.txtBox = txtBox;

            listServiceValue = new List<ServiceValue>();

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
                        RequestListServices();
                        break;

                    case "ListServices":
                    case "StartService":
                    case "StopService":
                    case "SetStartupType":
                        //There's no RestartService, you get a Stop and Start response
                        /*{
                           "action":"ListServices",
                           "success":true,
                           "serviceName":null, //Name of service admin changed
                           "displayName":null,
                           "contentsList":[
                        */

                        if (temp["contentsList"] != null) {
                            listServiceValue.Clear(); //Probably should update what's already there

                            foreach (dynamic s in temp["contentsList"].Children()) {
                                ServiceValue sv = new ServiceValue(s);
                                listServiceValue.Add(sv);
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

        public void RequestListServices() {
            JObject jStartData = new JObject();
            jStartData["action"] = "ListServices";
            serverB.Send(jStartData.ToString());
        }

        private void UpdateDisplayValues() {
            dgvServices.DataContext = null;

            DataTable dt = new DataTable();
            dt.Columns.Add("DisplayName", typeof(string));
            dt.Columns.Add("ServiceName", typeof(string));
            dt.Columns.Add("Description", typeof(string));
            dt.Columns.Add("ServiceStatus", typeof(int));
            dt.Columns.Add("StartupType", typeof(string));
            dt.Columns.Add("StartName", typeof(string));

            foreach (ServiceValue value in listServiceValue) {
                DataRow row = dt.NewRow();
                row[0] = value.DisplayName;
                row[1] = value.ServiceName;
                row[2] = value.Description;
                row[3] = value.ServiceStatus;
                row[4] = value.StartupType;
                row[5] = value.StartName;
                dt.Rows.Add(row);
            }

            dgvServices.DataContext = dt;
            //dgvServices.AutoResizeColumns();
            //dgvServices.Sort(dgvServices.Columns[0], System.ComponentModel.ListSortDirection.Ascending);
        }

        /*
         * {
  "action": "StopService",
  "serviceName": "HpTouchpointAnalyticsService",
  "displayName": "HP Analytics service"
}

        {
  "action": "StartService",
  "serviceName": "HpTouchpointAnalyticsService",
  "displayName": "HP Analytics service"
}

        {
  "action": "RestartService",
  "serviceName": "HpTouchpointAnalyticsService",
  "displayName": "HP Analytics service"
}

        {
  "action": "SetStartupType",
  "serviceName": "HpTouchpointAnalyticsService",
  "displayName": "HP Analytics service",
  "startupType": "manual"
}

        {
  "action": "SetStartupType",
  "serviceName": "HpTouchpointAnalyticsService",
  "displayName": "HP Analytics service",
  "startupType": "auto"
}
         */
    }
}
