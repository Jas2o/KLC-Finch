using Fleck;
using KLC_Finch;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using static LibKaseya.Enums;

namespace KLC
{

    public class WsA
    {

        private readonly LiveConnectSession Session;

        private readonly WebSocketServer ServerA;
        private IWebSocketConnection ServerAsocket;
        public int PortA { get; private set; }
        private readonly int PortB;
        //private string Module;

        public bool HasCompleted { get; private set; }

        public static bool useInternalMITM = false; //Hawk
        private readonly bool useExternalMITM = false; //Port M

        public WsA(LiveConnectSession session, int portA, int portB)
        {
            Session = session;
            //Module = "A";

            //A - Find a free port for me
            PortA = portA;
            PortB = portB;

            //A - new WebSocketServer (my port A)
            ServerA = new WebSocketServer("ws://0.0.0.0:" + PortA);

            //ServerA.RestartAfterListenError = true;
            ServerA.Start(socket => {
                ServerAsocket = socket;

                socket.OnOpen = () => {
#if DEBUG
                    Console.WriteLine("A Open (server port: " + PortA + ") " + socket.ConnectionInfo.Path);
#endif
                    ServerOnOpen(socket);
                };
                socket.OnClose = () => {
#if DEBUG
                    Console.WriteLine("A Close");
#endif

                    if (App.winStandalone != null)
                    {
                        Session.Callback?.Invoke(EPStatus.UnavailableWsA);
                        App.winStandalone.Disconnect(Session.RandSessionGuid, 0);
                    }
                    if (Session.ModuleRemoteControl != null)
                    {
                        string sessionId = socket.ConnectionInfo.Path.Replace("/app/remotecontrol/", "").Replace("?Y2", "");
                        Session.ModuleRemoteControl.Disconnect(sessionId);
                    }
                };
                socket.OnMessage = message => {
                    if (message.Contains("PeerOffline"))
                    {
                        //{"agentId":"429424626294329","type":"UserInterfaceStatus","data":{"relayError":"PeerOffline","sessionId":"y1d+uY2pEsC5dmpm43UjGg==","status":"ConnectedWithError"}}
#if DEBUG
                        //Console.WriteLine("Closing A because the agent is offline.");
                        Console.WriteLine("A: Endpoint is offline, will retry.");
#endif
                        Session.Callback?.Invoke(EPStatus.PeerOffline);
                        Task.Delay(10000).Wait(); // 10 seconds
                        ServerOnOpen(socket);
                        //HasCompleted = true;
                        //Close();
                    }
                    else
                    {
                        if (message.Contains("PeerToPeerFailure"))
                        {
#if DEBUG
                            //Console.WriteLine("Closing A because the agent is offline.");
                            Console.WriteLine("A: PeerToPeerFailure");
#endif

                            Session.Callback?.Invoke(EPStatus.PeerToPeerFailure);
                            Task.Delay(10000).Wait(); // 10 seconds
                            ServerOnOpen(socket);
                        }
                        else if (message.Contains("Error"))
                        {
                            App.ShowUnhandledExceptionFromSrc(message, "Websocket A - Unexpected");
                        }
                        else
                        {
#if DEBUG
                            Console.WriteLine("Unexpected A message: " + message);
#endif
                        }
                    }
                };
                socket.OnPing = byteA => {
                    //Session.Parent.LogText("A Ping");
                    //Required to stay connected longer than 2 min 10 sec
                    socket.SendPong(byteA);
                };
                socket.OnBinary = byteA => {
                    throw new NotImplementedException();
                };
                socket.OnError = ex => {
                    if (ex.Message.Contains("forcibly closed"))
                        return;

                    //Console.WriteLine("A Error: " + ex.ToString());
                    App.ShowUnhandledExceptionFromSrc(ex, "Websocket A");
                };
            });

            //A - Run AdminEndpoint (my port A)
            Process process = new Process();

            string[] files;
            if (useInternalMITM) //or WsM.CertificateExists()
            {
                files = new string[] {
                    @"C:\Program Files\Kaseya Live Connect-MITM\Kaseya.AdminEndpoint.exe",
                    Environment.ExpandEnvironmentVariables(@"%localappdata%\Apps\Kaseya Live Connect-MITM\Kaseya.AdminEndpoint.exe")
                };
            }
            else
            {
                files = new string[] {
                    @"C:\Program Files\Kaseya Live Connect\Kaseya.AdminEndpoint.exe",
                    Environment.ExpandEnvironmentVariables(@"%localappdata%\Apps\Kaseya Live Connect\Kaseya.AdminEndpoint.exe")
                };
            }

            foreach (string file in files)
            {
                if (File.Exists(file))
                {
                    process.StartInfo.FileName = file;
                    break;
                }
            }

            if (process.StartInfo.FileName.Length > 0)
            {
                process.StartInfo.Arguments = "-viewerport " + PortA;
                process.StartInfo.CreateNoWindow = true;
                //process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                process.Start();
            }

            //--

            //Y - When socket1 is told a module is connecting
            //socketN = new WebSocketClient(what LiveConnect said the module is)

            //new WebSocketServer
            //Tell AdminEndPoint
        }

        public void Close()
        {
            if (ServerAsocket != null)
                ServerAsocket.Close();
            if (ServerA.ListenerSocket.Connected)
                ServerA.ListenerSocket.Close();
        }

        public void Send(string message)
        {
            try
            {
                ServerAsocket.Send(message).Wait();
            }
            catch (Exception ex)
            {
#if DEBUG
                Console.WriteLine("[WsA:Send] " + ex.ToString());
#endif
            }
        }

        private void ServerOnOpen(IWebSocketConnection socket)
        {
            JObject json1 = new JObject
            {
                ["data"] = new JObject
                {
                    ["adminId"] = Session.Auth.AdminId,
                    ["adminName"] = Session.Auth.UserName,
                    ["jsonWebToken"] = Session.Eal.auth_jwt,
                    ["server"] = Session.agent.VSA,
                    ["serverPort"] = 443,
                    ["tenantId"] = "1"
                },
                ["type"] = "AdminIdentity"
            };

            JObject json2 = new JObject
            {
                ["data"] = new JObject
                {
                    ["agentId"] = Session.agentGuid,
                    ["connectPort"] = PortB,
                    ["endpointId"] = Session.Eirc.endpoint_id,
                    ["jsonWebToken"] = Session.Eirc.session_jwt,
                    ["policy"] = 0,
                    ["tenantId"] = Session.Auth.TenantId
                },
                ["p2pConnectionId"] = Session.RandSessionGuid,
                ["type"] = "AgentIdentity"
            };

            if (useExternalMITM)
            {
                int newPort = Session.WebsocketM.PortM;

                json1["data"]["server"] = "vsa-mitm.example";
                json1["data"]["serverPort"] = newPort;
            }

            socket.Send(json1.ToString());
            socket.Send(json2.ToString());
#if DEBUG
            Console.WriteLine("Sent the A payload");
#endif
        }
    }
}
