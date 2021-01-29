using Fleck;
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

namespace KLC_Finch {
    public class Dashboard {

        private static string modulename = "dashboard";
        private TextBox txtBox;
        private IWebSocketConnection serverB;

        public Dashboard(KLC.LiveConnectSession session, TextBox txtBox = null) {
            this.txtBox = txtBox;

            if (session != null)
                session.WebsocketB.ControlAgentSendTask(modulename);
        }

        public void SetSocket(IWebSocketConnection ServerBsocket) {
            this.serverB = ServerBsocket;
        }

        public void GetCpuRam() {
            if (serverB != null) {
                JObject jAction = new JObject();
                jAction["action"] = "GetCpuRam";
                serverB.Send(jAction.ToString());
            }
        }

        public void GetTopEvents() {
            if (serverB != null) {
                JObject jAction = new JObject();
                jAction["action"] = "GetTopEvents";
                serverB.Send(jAction.ToString());
            }
        }

        public void GetTopProcesses() {
            if (serverB != null) {
                JObject jAction = new JObject();
                jAction["action"] = "GetTopProcesses";
                serverB.Send(jAction.ToString());
            }
        }

        public void GetVolumes() {
            if (serverB != null) {
                JObject jAction = new JObject();
                jAction["action"] = "GetVolumes";
                serverB.Send(jAction.ToString());
            }
        }

        public void Receive(string message) {
            txtBox.Dispatcher.Invoke(new Action(() => {
                dynamic temp = JsonConvert.DeserializeObject(message);
                string something = (string)temp["action"];
                switch (temp["action"].ToString()) {
                    case "ScriptReady":
                        JObject jStartDashboardData = new JObject();
                        jStartDashboardData["action"] = "StartDashboardData";
                        serverB.Send(jStartDashboardData.ToString());
                        break;
                    default:
                        txtBox.AppendText("Dashboard message received: " + message + "\r\n");
                        break;
                }
            }));
        }

        /*
        private WebSocket WS_EdgeRelay(string authPayloadjsonWebToken, string sessionId) {

            string pathModule = Util.EncodeToBase64("/app/" + modulename);

            WebSocket websocket = new WebSocket("wss://vsa-web.company.com.au:443/kaseya/edge/relay?auth=" + authPayloadjsonWebToken + "&relayId=" + sessionId + "|" + pathModule);

            websocket.AutoSendPingInterval = 5;
            websocket.EnableAutoSendPing = true;
            if (txtBox != null) {
                websocket.Opened += (sender, e) => txtBox.Invoke(new Action(() => {
                    txtBox.AppendText("Dashboard Socket opened: " + sessionId + "\r\n");
                }));
                websocket.Closed += (sender, e) => txtBox.Invoke(new Action(() => {
                    txtBox.AppendText("Dashboard Socket closed: " + sessionId + " - " + e.ToString() + "\r\n");
                }));
                websocket.MessageReceived += (sender, e) => 
                websocket.Error += (sender, e) => txtBox.Invoke(new Action(() => {
                    txtBox.AppendText("Dashboard Socket error: " + sessionId + " - " + e.Exception.ToString() + "\r\n");
                }));
            } else {
                websocket.Opened += (sender, e) => Console.WriteLine("Dashboard Socket opened: " + sessionId);
                websocket.Closed += (sender, e) => Console.WriteLine("Dashboard Socket closed: " + sessionId + " - " + e.ToString());
                websocket.MessageReceived += (sender, e) => Console.WriteLine("Dashboard message received: " + sessionId + " - " + e.Message);
                websocket.Error += (sender, e) => Console.WriteLine("Dashboard Socket error: " + sessionId + " - " + e.Exception.ToString());
            }

            Util.ConfigureProxy(websocket);

            websocket.Open();
            return websocket;
        }
        */
    }
}
