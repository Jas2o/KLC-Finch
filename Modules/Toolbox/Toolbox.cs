using Fleck;
using LibKaseya;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web;
using System.Windows.Controls;
using System.IO;

namespace KLC_Finch {
    public class Toolbox {

        private static readonly string modulename = "toolbox";
        private IWebSocketConnection serverB;
        private readonly ToolboxData toolboxData;
        private readonly string AgentID;
        private readonly string vsa;

        public Toolbox(KLC.LiveConnectSession session, ToolboxData toolboxData) {
            this.toolboxData = toolboxData;

            if (session != null) {
                session.WebsocketB.ControlAgentSendTask(modulename);
                AgentID = session.agent.ID;
                vsa = session.agent.VSA;
            }
        }

        public void SetSocket(IWebSocketConnection ServerBsocket) {
            this.serverB = ServerBsocket;
        }

        public void Receive(string message) {
            dynamic temp = JsonConvert.DeserializeObject(message);
            switch (temp["action"].ToString()) {
                case "ScriptReady":
                    Console.WriteLine("Toolbox ready\r\n");

                    IRestResponse response = Kaseya.GetRequest(vsa, "api/v1.0/assetmgmt/customextensions/" + AgentID + "/folder//");
                    dynamic first = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(response.Content);

                    toolboxData.Clear();
                    foreach (dynamic second in first["Result"].Children()) {
                        if (second["isFile"] == false) {
                            string name = (string)second["Name"];

                            IRestResponse response2 = Kaseya.GetRequest(vsa, "api/v1.0/assetmgmt/customextensions/" + AgentID + "/folder//" + name);
                            dynamic third = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(response2.Content);

                            foreach (dynamic forth in third["Result"].Children()) {
                                toolboxData.Add(new ToolboxValue(forth));
                            }
                        }
                    }

                    break;

                case "FileDownloaded":
                    //{"action":"FileDownloaded","success":true}
                    toolboxData.Status = "File downloaded.";
                    break;

                case "FileExecuted":
                    //{ "action":"FileExecuted","success":true}
                    toolboxData.Status = "File executed.";
                    break;

                default:
                    Console.WriteLine("Toolbox message: " + message + "\r\n");
                    break;
            }
        }

        public void Execute(ToolboxValue tv) {
            IRestResponse response = Kaseya.GetRequest(vsa, "api/v1.0/assetmgmt/customextensions/" + AgentID + "/endpointref/" + tv.ParentPath + "/" + tv.NameActual);
            dynamic result = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(response.Content);

            /*{
              "Result": "80a531e8-a74a-4704-bb83-f86b8676072f",
              "ResponseCode": 0,
              "Status": "OK",
              "Error": "None"
            }*/

            if ((string)result["Error"] == "None") {
                JObject jData = new JObject {
                    ["action"] = "ExecuteFile",
                    ["fileGuid"] = (string)result["Result"],
                    ["fileName"] = tv.NameDisplay
                };
                serverB.Send(jData.ToString());
            }
        }

        public void Download(ToolboxValue tv, string folder) {
            string output = folder + "\\" + tv.NameDisplay;
            if (!File.Exists(output)) {
                //The missing slash is intentional
                IRestResponse response = Kaseya.GetRequest(vsa, "api/v1.0/assetmgmt/customextensions/" + AgentID + "/file" + tv.ParentPath + "/" + tv.NameActual);

                if (response.StatusCode == System.Net.HttpStatusCode.OK) {
                    FileStream fs = new FileStream(folder + "\\" + tv.NameDisplay, FileMode.Create);
                    fs.Write(response.RawBytes, 0, response.RawBytes.Length);
                    fs.Close();
                }
            }

            System.Diagnostics.Process.Start("explorer.exe", "/select, \"" + output + "\"");
        }
    }
}
