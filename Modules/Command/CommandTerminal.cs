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
    public class CommandTerminal {

        private static string modulenameWin = "commandshell";
        private static string modulenameMac = "terminal";
        private string modulename;
        private RichTextBox richCommand;

        private bool IsMac;
        private IWebSocketConnection serverB;
        BaseTerm term;

        public CommandTerminal(KLC.LiveConnectSession session, RichTextBox richCommand) {
            this.richCommand = richCommand;
            term = new BaseTerm(richCommand);

            if (session != null) {
                IsMac = session.agent.IsMac;
                modulename = (IsMac ? modulenameMac : modulenameWin);
                session.WebsocketB.ControlAgentSendTask(modulename);
            }
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
            if (serverB == null)
                return;

            //string inputSafe = input.Replace(@"\", @"\\").Replace("\"", "\\\"");

            /*
            if (IsMac) {
                //txtCommand.AppendText(inputOrg + "\r\n");
                wsAppCommandshell.Send(string.Format("{{\"action\":\"ShellInput\",\"input\":\"{0}\\n\"}}", inputSafe));
            } else {
                richCommand.AppendText(input + "\r\n", Color.Cyan, richCommand.BackColor);
                wsAppCommandshell.Send(string.Format("{{\"action\":\"ShellCommand\",\"command\":\"{0}\\n\"}}", inputSafe));
            }
            */

            JObject jAction = new JObject();
            if (IsMac) {
                jAction["action"] = "ShellInput";
                jAction["input"] = input + "\n";
                //txtCommand.AppendText(inputOrg + "\r\n");
            } else {
                jAction["action"] = "ShellCommand";
                jAction["command"] = input + "\n";
                richCommand.AppendText(input + "\r\n", Colors.Cyan); //Wrong background colour
                richCommand.ScrollToEnd();
            }

            serverB.Send(jAction.ToString());
        }

        public void Receive(string message) {
            richCommand.Dispatcher.Invoke((Action)delegate {
                dynamic temp = JsonConvert.DeserializeObject(message);
                switch ((string)temp["action"]) {
                    case "ScriptReady":
                        JObject jAction = new JObject();
                        jAction["action"] = "ConnectionOpen";
                        jAction["rows"] = 54;
                        jAction["cols"] = 106;
                        serverB.Send(jAction.ToString());
                        break;
                    case "ShellOutput":
                        //Mac
                        string output = (string)temp["output"];
                        output = HttpUtility.UrlDecode((string)temp["output"]);
                        term.Append(output);
                        break;
                    case "ShellResponse":
                        //Windows CMD or Powershell
                        term.Append((string)temp["output"]);
                        break;
                    default:
                        term.RichText.AppendText("CommandTerminal message received: " + message + "\r\n", Colors.Yellow, Colors.Black);
                        break;
                }
            });
        }

        public void SetShowDebug(bool isChecked) {
            term.SHOW_DEBUG_TEXT = isChecked;
        }

        /*
        private WebSocket WS_EdgeRelay(string authPayloadjsonWebToken, string sessionId) {
            string pathModule = Util.EncodeToBase64("/app/" + (IsMac ? modulenameMac : modulenameWin));

            WebSocket websocket = new WebSocket("wss://vsa-web.company.com.au:443/kaseya/edge/relay?auth=" + authPayloadjsonWebToken + "&relayId=" + sessionId + "|" + pathModule);

            websocket.AutoSendPingInterval = 5;
            websocket.EnableAutoSendPing = true;
            if (term != null) {
                websocket.Opened += (sender, e) => term.RichText.Invoke(new Action(() => {
                    term.RichText.AppendText("CommandTerminal Socket opened: " + sessionId + "\r\n", System.Drawing.Color.Yellow, System.Drawing.Color.Black);
                }));
                websocket.Closed += (sender, e) => term.RichText.Invoke(new Action(() => {
                    term.RichText.AppendText("CommandTerminal Socket closed: " + sessionId + " - " + e.ToString() + "\r\n", System.Drawing.Color.Yellow, System.Drawing.Color.Black);
                }));
                websocket.MessageReceived += (sender, e) => term.RichText.Invoke(new Action(() => {
                    
                }));
                websocket.Error += (sender, e) => term.RichText.Invoke(new Action(() => {
                    term.RichText.AppendText("CommandTerminal Socket error: " + sessionId + " - " + e.Exception.ToString() + "\r\n", System.Drawing.Color.Yellow, System.Drawing.Color.Black);
                }));
            } else {
                websocket.Opened += (sender, e) => Console.WriteLine("CommandTerminal Socket opened: " + sessionId);
                websocket.Closed += (sender, e) => Console.WriteLine("CommandTerminal Socket closed: " + sessionId + " - " + e.ToString());
                websocket.MessageReceived += (sender, e) => Console.WriteLine("CommandTerminal message received: " + sessionId + " - " + e.Message);
                websocket.Error += (sender, e) => Console.WriteLine("CommandTerminal Socket error: " + sessionId + " - " + e.Exception.ToString());
            }

            websocket.Open();
            return websocket;
        }
        */
    }
}
