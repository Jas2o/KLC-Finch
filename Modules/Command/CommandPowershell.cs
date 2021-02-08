using Fleck;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Controls;
using System.Windows.Media;

namespace KLC_Finch {
    public class CommandPowershell {

        private static string modulename = "commandshellvt100";
        private RichTextBox richPowershell;

        private IWebSocketConnection serverB;
        BaseTerm term;

        public CommandPowershell(KLC.LiveConnectSession session, RichTextBox richPowershell) {
            this.richPowershell = richPowershell;
            term = new BaseTerm(richPowershell);

            if (session != null)
                session.WebsocketB.ControlAgentSendTask(modulename);
        }

        public void SetSocket(IWebSocketConnection ServerBsocket) {
            this.serverB = ServerBsocket;
        }

        public void SendKillCommand() {
            if (serverB == null)
                return;

            JObject jAction = new JObject();
            jAction["action"] = "KillCommand";
            serverB.Send(jAction.ToString());
        }

        public void Send(string input) {
            //string inputSafe = input.Replace(@"\", @"\\").Replace("\"", "\\\"");
            //txtCommand.AppendText(input + "\r\n");

            JObject jAction = new JObject();
            jAction["action"] = "ShellInput";
            jAction["input"] = input + "%0D";

            serverB.Send(jAction.ToString());
        }

        public void Receive(string message) {
            richPowershell.Dispatcher.Invoke((Action)delegate {
                dynamic temp = JsonConvert.DeserializeObject(message);
                switch ((string)temp["action"]) {
                    case "ScriptReady":
                        JObject jAction = new JObject();
                        jAction["action"] = "ConnectionOpen";
                        jAction["rows"] = 54;
                        jAction["cols"] = 106;
                        jAction["windowsVT100"] = true;
                        serverB.Send(jAction.ToString());
                        break;
                    case "ShellResponse":
                        //Windows CMD or Powershell
                        term.Append((string)temp["output"]);
                        break;
                    default:
                        term.RichText.AppendText("CommandPowershell message received: " + " - " + message + "\r\n", Colors.Yellow, Colors.Black);
                        break;
                }
            });
        }

        public void SetShowDebug(bool isChecked) {
            term.SHOW_DEBUG_TEXT = isChecked;
        }

        /*
        private WebSocket WS_EdgeRelay(string authPayloadjsonWebToken, string sessionId) {
            string pathModule = Util.EncodeToBase64("/app/" + modulename);

            WebSocket websocket = new WebSocket("wss://vsa-web.company.com.au:443/kaseya/edge/relay?auth=" + authPayloadjsonWebToken + "&relayId=" + sessionId + "|" + pathModule);

            websocket.AutoSendPingInterval = 5;
            websocket.EnableAutoSendPing = true;
            if (term != null) {
                websocket.Opened += (sender, e) => term.RichText.Invoke(new Action(() => {
                    term.RichText.AppendText("CommandPowershell Socket opened: " + sessionId + "\r\n", System.Drawing.Color.Yellow, System.Drawing.Color.Black);
                }));
                websocket.Closed += (sender, e) => term.RichText.Invoke(new Action(() => {
                    term.RichText.AppendText("CommandPowershell Socket closed: " + sessionId + " - " + e.ToString() + "\r\n", System.Drawing.Color.Yellow, System.Drawing.Color.Black);
                }));
                websocket.MessageReceived += (sender, e) => term.RichText.Invoke(new Action(() => {
                    dynamic temp = JsonConvert.DeserializeObject(e.Message);
                    switch ((string)temp["action"]) {
                        
                    }
                }));
                websocket.Error += (sender, e) => term.RichText.Invoke(new Action(() => {
                    term.RichText.AppendText("CommandPowershell Socket error: " + sessionId + " - " + e.Exception.ToString() + "\r\n", System.Drawing.Color.Yellow, System.Drawing.Color.Black);
                }));
            } else {
                websocket.Opened += (sender, e) => Console.WriteLine("CommandPowershell Socket opened: " + sessionId);
                websocket.Closed += (sender, e) => Console.WriteLine("CommandPowershell Socket closed: " + sessionId + " - " + e.ToString());
                websocket.MessageReceived += (sender, e) => Console.WriteLine("CommandPowershell message received: " + sessionId + " - " + e.Message);
                websocket.Error += (sender, e) => Console.WriteLine("CommandPowershell Socket error: " + sessionId + " - " + e.Exception.ToString());
            }

            websocket.Open();
            return websocket;
        }
        */
    }
}
