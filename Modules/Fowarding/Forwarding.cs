using Fleck;
using LibKaseya;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.Windows.Controls;
using KLC;

namespace KLC_Finch
{
    public class Forwarding
    {
        private static readonly string modulename = "forwarding";
        private static readonly string basePathRDP = @"%localappdata%\Kaseya\Data\KaseyaLiveConnect\RDPConfig\";

        private KLC.LiveConnectSession session;
        private IWebSocketConnection serverB;
        private readonly string AgentID;
        private readonly string IPAddress;
        private readonly int Port;
        private Process mstsc;
        private string pathRDP;

        public TextBox TxtAccess { get; }
        public Label LblStatus { get; }

        public Forwarding(KLC.LiveConnectSession session, string ip, int port)
        {
            this.session = session;
            IPAddress = ip;
            Port = port;

            if (session != null)
            {
                session.WebsocketB.ControlAgentSendTask(modulename);
                AgentID = session.agent.ID;
            }
        }

        public Forwarding(LiveConnectSession session, string ip, int port, TextBox txtAccess, Label lblStatus) : this(session, ip, port)
        {
            TxtAccess = txtAccess;
            LblStatus = lblStatus;
        }

        public void SetSocket(IWebSocketConnection ServerBsocket)
        {
            this.serverB = ServerBsocket;
        }

        public void Receive(string message)
        {
            dynamic temp = JsonConvert.DeserializeObject(message);
            switch (temp["action"].ToString())
            {
                case "ScriptReady":
                    DisplayStatus("Starting...");
                    //Debug.WriteLine("Forwarding ready\r\n");

                    JObject jStartData = new JObject
                    {
                        ["action"] = "SetupForwarding",
                        ["host"] = IPAddress,
                        ["port"] = Port
                    };
                    serverB.Send(jStartData.ToString());
                    break;

                case "SetupForwarding":
                    if (!(bool)temp["success"] == true)
                    {
                        DisplayStatus("Fail");
                        return;
                    }

                    //Assuming success is true
                    JObject jConnect = new JObject
                    {
                        ["action"] = "ConnectForwarding",
                        ["sessionId"] = temp["sessionId"]
                    };
                    serverB.Send(jConnect.ToString());

                    int peerAcceptPort = (int)temp["peerAcceptPort"];

                    DisplayAccess(peerAcceptPort);
                    DisplayStatus("Setting up...");
                    session.CallbackS(Enums.EPStatus.NativeRDPStarting);

                    if (Port == 3389)
                    {
                        pathRDP = Environment.ExpandEnvironmentVariables(basePathRDP) + session.agent.Name + "-" + peerAcceptPort + ".rdp";
                        Directory.CreateDirectory(Environment.ExpandEnvironmentVariables(basePathRDP));
                        StreamWriter sw = new StreamWriter(pathRDP, false);
                        sw.WriteLine("full address:s:127.0.0.1:" + peerAcceptPort);
                        sw.WriteLine("EnableCredSSPSupport:i:0");
                        sw.Close();

                        mstsc = new Process();
                        mstsc.StartInfo.FileName = "mstsc.exe";
                        mstsc.StartInfo.Arguments = "/edit \"" + pathRDP + "\"";
                        mstsc.EnableRaisingEvents = true;
                        mstsc.Exited += Mstsc_Exited;
                        mstsc.Start();
                    }
                    break;

                case "ConnectForwarding":
                    if (!(bool)temp["success"] == true)
                    {
                        DisplayStatus("Fail");
                        return;
                    }

                    session.CallbackS(Enums.EPStatus.NativeRDPActive);
                    DisplayStatus("Active");
                    break;

                default:
                    Debug.WriteLine("Forwarding message: " + message + "\r\n");
                    break;
            }
        }

        private void Mstsc_Exited(object sender, EventArgs e)
        {
            session.CallbackS(Enums.EPStatus.NativeRDPEnded);
            session.WebsocketB.ControlAgentSendRDP_StateRestore();

            if (pathRDP.EndsWith(".rdp"))
                File.Delete(pathRDP);
        }

        public void Close()
        {
            if (mstsc != null)
                mstsc.CloseMainWindow();
            DisplayStatus("Closed");
        }

        public bool IsRunning()
        {
            if (mstsc == null || mstsc.HasExited)
                return false;
            else
                return true;
        }

        private void DisplayAccess(int peerPort)
        {
            if (TxtAccess is null)
                return;

            string prefix = "";
            if (Port == 80)
                prefix = "http://";
            if (Port == 443)
                prefix = "https://";

            TxtAccess.Dispatcher.Invoke(new Action(() => {
                TxtAccess.Text = prefix + "127.0.0.1:" + peerPort;
            }));
        }

        private void DisplayStatus(string status)
        {
            if (LblStatus is null)
                return;

            LblStatus.Dispatcher.Invoke(new Action(() => {
                LblStatus.Content = status;
            }));
        }
    }
}
