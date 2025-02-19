using Fleck;
using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;

namespace KLC {

    //M for Muhahaha

    //This requires a modified version of Fleck which responds with x-kaseya headers

    public class WsM {

        private static string pathCertificate = @"C:\Users\jas2o\Desktop\Kaseya\lms-jason.example.p12";

        private LiveConnectSession Session;

        private WebSocketServer ServerM;
        private IWebSocketConnection ServerMsocket;
        public int PortM { get; private set; }
        //private string Module;

        public WsM(LiveConnectSession session, int portM) {
            Session = session;
            //Module = "M";

            //A - Find a free port for me
            PortM = portM;

            //A - new WebSocketServer (my port A)
            ServerM = new WebSocketServer("wss://0.0.0.0:" + PortM);
            ServerM.EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12;
            ServerM.Certificate = new X509Certificate2(pathCertificate, "changc");

            //ServerA.RestartAfterListenError = true;
            ServerM.Start(socket => {
                ServerMsocket = socket;

                socket.OnOpen = () => {
                    Console.WriteLine("M Open (server port: " + PortM + ") " + socket.ConnectionInfo.Path);

                };
                socket.OnClose = () => {
                    Console.WriteLine("M Close");
                };
                socket.OnMessage = message => {
                    throw new NotImplementedException();
                };
                socket.OnPing = byteA => {
                    //Session.Parent.LogText("A Ping");
                    //Required to stay connected longer than 2 min 10 sec
                    socket.SendPong(byteA);
                };
                socket.OnBinary = byteA => {
                    if (byteA.Length == 1 && byteA[0] == 0xA5) {
                        socket.Send(new byte[] { 0xA5 });
                    } else {
                        string hex = BitConverter.ToString(byteA).Replace("-", string.Empty);
                        Console.WriteLine(hex + "\r\n");
                    }
                };
                socket.OnError = ex => {
                    Console.WriteLine("M Error: " + ex.ToString());
                };
            });

        }

        public static bool CertificateExists() {
            return File.Exists(pathCertificate);
        }

        public void Close() {
            ServerMsocket.Close();
            ServerM.ListenerSocket.Close();
        }

        public void Send(string message) {
            try {
                ServerMsocket.Send(message).Wait();
            } catch (Exception ex) {
                Console.WriteLine(ex.ToString());
            }
        }

    }
}
