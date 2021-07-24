using Fleck;
using KLC_Finch;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using static LibKaseya.Enums;

namespace KLC {

    public class WsA {
        
        private readonly LiveConnectSession Session;

        private readonly WebSocketServer ServerA;
        private IWebSocketConnection ServerAsocket;
        public int PortA { get; private set; }
        private readonly int PortB;
        //private string Module;

        public bool HasCompleted { get; private set; }

        public static bool useInternalMITM = false; //Hawk
        private readonly bool useExternalMITM = false; //Port M

        public WsA(LiveConnectSession session, int portA, int portB) {
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

                    if(App.alternative != null) {
                        App.alternative.Disconnect(Session.RandSessionGuid, 0);
                    }
                    if (Session.ModuleRemoteControl != null) {
                        string sessionId = socket.ConnectionInfo.Path.Replace("/app/remotecontrol/", "").Replace("?Y2", "");
                        Session.ModuleRemoteControl.Disconnect(sessionId);
                    }
                };
                socket.OnMessage = message => {
                    if (message.Contains("PeerOffline")) {
                        //{"agentId":"429424626294329","type":"UserInterfaceStatus","data":{"relayError":"PeerOffline","sessionId":"y1d+uY2pEsC5dmpm43UjGg==","status":"ConnectedWithError"}}
#if DEBUG
                        Console.WriteLine("Closing A because the agent is offline.");
#endif
                        HasCompleted = true;
                        Close();
                    } else {
#if DEBUG
                        Console.WriteLine("Unexpected A message: " + message);
#endif
                        //throw new NotImplementedException();
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
            string file1 = @"C:\Program Files\Kaseya Live Connect\Kaseya.AdminEndpoint.org.exe";
            string file2 = @"C:\Program Files\Kaseya Live Connect\Kaseya.AdminEndpoint.exe";
            if(useInternalMITM) {
                file1 = @"C:\Program Files\Kaseya Live Connect-MITM\Kaseya.AdminEndpoint.exe";
                file2 = @"C:\Program Files\Kaseya Live Connect-MITM\Kaseya.AdminEndpoint.org.exe";
            } /* else if (WsM.CertificateExists()) {
                file1 = @"C:\Program Files\Kaseya Live Connect-MITM\Kaseya.AdminEndpoint.org.exe";
                file2 = @"C:\Program Files\Kaseya Live Connect-MITM\Kaseya.AdminEndpoint.exe";
            }*/
            Process process = new Process();
            process.StartInfo.FileName = (File.Exists(file1) ? file1 : file2);
            process.StartInfo.Arguments = "-viewerport " + PortA;
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            process.Start();

            //--

            //Y - When socket1 is told a module is connecting
            //socketN = new WebSocketClient(what LiveConnect said the module is)

            //new WebSocketServer
            //Tell AdminEndPoint
        }

        public void Close() {
            if (ServerAsocket != null)
                ServerAsocket.Close();
            if(ServerA.ListenerSocket.Connected)
                ServerA.ListenerSocket.Close();
        }

        public void Send(string message) {
            try {
                ServerAsocket.Send(message).Wait();
            } catch (Exception ex) {
#if DEBUG
                Console.WriteLine("[WsA:Send] " + ex.ToString());
#endif
            }
        }

        private void ServerOnOpen(IWebSocketConnection socket) {
            if (useExternalMITM) {
                int newPort = Session.WebsocketM.PortM;

                string jsonM1 = "{\"data\":{\"adminId\":\"" + Session.Auth.AdminId + "\",\"adminName\":\"" + Session.Auth.UserName + "\",\"jsonWebToken\":\"" + Session.Eal.auth_jwt + "\",\"server\":\"lms-jason.example\",\"serverPort\":" + newPort + ",\"tenantId\":\"1\"},\"type\":\"AdminIdentity\"}";
                string jsonM2 = "{\"data\":{\"agentId\":\"" + Session.agentGuid + "\",\"connectPort\":" + PortB + ",\"endpointId\":\"" + Session.Eirc.endpoint_id + "\",\"jsonWebToken\":\"" + Session.Eirc.session_jwt + "\",\"policy\":0,\"tenantId\":\"" + Session.Auth.TenantId + "\"},\"p2pConnectionId\":\"" + Session.RandSessionGuid + "\",\"type\":\"AgentIdentity\"}";
                socket.Send(jsonM1);
                socket.Send(jsonM2);

                //--
                return;
            }

            string json1 = "{\"data\":{\"adminId\":\"" + Session.Auth.AdminId + "\",\"adminName\":\"" + Session.Auth.UserName + "\",\"jsonWebToken\":\"" + Session.Eal.auth_jwt + "\",\"server\":\"vsa-web.company.com.au\",\"serverPort\":443,\"tenantId\":\"1\"},\"type\":\"AdminIdentity\"}";
            string json2 = "{\"data\":{\"agentId\":\"" + Session.agentGuid + "\",\"connectPort\":" + PortB + ",\"endpointId\":\"" + Session.Eirc.endpoint_id + "\",\"jsonWebToken\":\"" + Session.Eirc.session_jwt + "\",\"policy\":0,\"tenantId\":\"" + Session.Auth.TenantId + "\"},\"p2pConnectionId\":\"" + Session.RandSessionGuid + "\",\"type\":\"AgentIdentity\"}";

            socket.Send(json1);
            socket.Send(json2);
#if DEBUG
            Console.WriteLine("Sent the A payload");
#endif
        }
    }
}
