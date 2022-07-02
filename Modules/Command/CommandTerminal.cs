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
    public class CommandTerminal {

        private readonly string modulename;

        private readonly bool IsMac;
        private IWebSocketConnection serverB;

        private readonly VirtualTerminalController vtController;
        private readonly DataConsumer dataPart;

        /*
        BaseTermRTB term;
        private RichTextBox richCommand;

        public CommandTerminal(KLC.LiveConnectSession session, RichTextBox richCommand) {
            this.richCommand = richCommand;
            term = new BaseTermRTB(richCommand);

            if (session != null) {
                IsMac = session.agent.IsMac;
                modulename = (IsMac ? modulenameMac : modulenameWin);
                session.WebsocketB.ControlAgentSendTask(modulename);
            }
        }
        */

        public CommandTerminal(KLC.LiveConnectSession session, VirtualTerminalController vtController, DataConsumer dataPart) {
            this.vtController = vtController;
            this.dataPart = dataPart;

            if (session != null) {
                IsMac = session.agent.OSTypeProfile == LibKaseya.Agent.OSProfile.Mac;
                modulename = (IsMac ? "terminal" : "commandshell");
                session.WebsocketB.ControlAgentSendTask(modulename);
            }
        }

        public void SetSocket(IWebSocketConnection ServerBsocket) {
            this.serverB = ServerBsocket;
        }

        public void SendKillCommand() {
            if (serverB == null)
                return;

            JObject jAction = new JObject {
                ["action"] = "KillCommand"
            };
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

            JObject jAction;
            if (IsMac) {
                input = Uri.EscapeDataString(input);
                jAction = new JObject {
                    ["action"] = "ShellInput",
                    ["input"] = input + "\n"
                };
                //txtCommand.AppendText(inputOrg + "\r\n");
            } else {
                jAction = new JObject {
                    ["action"] = "ShellCommand",
                    ["command"] = input + "\n"
                };
                //richCommand.AppendText(input + "\r\n", Colors.Cyan); //Wrong background colour
                //richCommand.ScrollToEnd();

                dataPart.Push(Encoding.UTF8.GetBytes(input + "\r\n"));

                if (input.ToLower() == "cls")
                    dataPart.Push(Encoding.UTF8.GetBytes("\u001b[2J"));
            }
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
                            ["cols"] = vtController.VisibleColumns
                        };
                        serverB.Send(jAction.ToString());
                        break;
                    case "ShellOutput":
                        //Mac
                        string output = (string)temp["output"];
                        output = HttpUtility.UrlDecode((string)temp["output"]);

                        dataPart.Push(Encoding.UTF8.GetBytes(output));
                        break;
                    case "ShellResponse":
                        //Windows CMD or Powershell
                        dataPart.Push(Encoding.UTF8.GetBytes((string)temp["output"]));
                        break;
                    default:
                        //term.RichText.AppendText("CommandTerminal message received: " + message + "\r\n", Colors.Yellow, Colors.Black);
                        Console.WriteLine("CommandTerminal message received: " + message);
                        break;
                }

                //txtCommand.ScrollToEnd();
            });
        }
    }
}
