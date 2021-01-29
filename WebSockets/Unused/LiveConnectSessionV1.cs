using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace KLC {
    public class LiveConnectSession {

        public static List<LiveConnectSession> listSession = new List<LiveConnectSession>();

        public int PortZ; //LiveConnect
        public WsZ WebsocketZ;
        public List<WsY1> listY1Client = new List<WsY1>();
        public List<WsY2> listY2Client = new List<WsY2>();

        //--

        public LiveConnectSession() {
            PortZ = GetNewPort();

            WebsocketZ = new WsZ(this, PortZ);
        }

        //--

        public static LiveConnectSession Create() {
            LiveConnectSession session = new LiveConnectSession();
            listSession.Add(session);
            return session;
        }

        public static int GetNewPort() {
            TcpListener tcpListener = new TcpListener(IPAddress.Parse("127.0.0.1"), 0);
            tcpListener.Start();
            int port = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
            tcpListener.Stop();

            return port;
        }
    }
}
