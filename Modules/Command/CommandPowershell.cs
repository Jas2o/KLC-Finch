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
using VtNetCore.VirtualTerminal;
using VtNetCore.XTermParser;

namespace KLC_Finch {
    public class CommandPowershell {

        private static readonly string modulename = "commandshellvt100";

        private IWebSocketConnection serverB;

        private readonly VirtualTerminalController vtController;
        private readonly DataConsumer dataPart;

        public CommandPowershell(KLC.LiveConnectSession session, VirtualTerminalController vtController, DataConsumer dataPart) {
            this.vtController = vtController;
            this.dataPart = dataPart;

            if (session != null)
                session.WebsocketB.ControlAgentSendTask(modulename);
        }

        public void SetSocket(IWebSocketConnection ServerBsocket) {
            this.serverB = ServerBsocket;
        }

        public void SendKillCommand() {
            if (serverB == null)
                return;

            JObject jAction = new JObject {
                //["action"] = "KillCommand"
                ["action"] = "ShellInput",
                ["input"] = "%03"
            };
            serverB.Send(jAction.ToString());
        }

        public void Send(string input, bool lineMode=true) {
            //string inputSafe = input.Replace(@"\", @"\\").Replace("\"", "\\\"");
            //txtCommand.AppendText(input + "\r\n");

            input = input.Replace("\r", "");
            input = Uri.EscapeDataString(input);

            JObject jAction = new JObject {
                ["action"] = "ShellInput",
                ["input"] = lineMode ? input + "%0D" : input
            };
            serverB.Send(jAction.ToString());
        }

        public void Receive(string message) {
            App.Current.Dispatcher.Invoke((Action)delegate {
                dynamic temp = JsonConvert.DeserializeObject(message);
                switch ((string)temp["action"]) {
                    case "ScriptReady":
                        JObject jAction = new JObject {
                            ["action"] = "ConnectionOpen",
                            ["rows"] = vtController.VisibleRows,
                            ["cols"] = vtController.VisibleColumns,
                            ["windowsVT100"] = true
                        };
                        serverB.Send(jAction.ToString());
                        break;
                    case "ShellResponse":
                        //Windows CMD or Powershell
                        //term.Append((string)temp["output"]);

                        if((string)temp["output"] != "\u001b[?25l\u001b[?25h\u001b[54;25H")
                            dataPart.Push(Encoding.UTF8.GetBytes((string)temp["output"]));

                        break;
                    default:
                        Console.WriteLine("CommandPowershell unhandled message received: " + " - " + message);
                        break;
                }

                //textPowershell.ScrollToEnd();
            });
        }

    }
}
