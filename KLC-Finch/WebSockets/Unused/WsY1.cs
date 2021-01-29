using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WatsonWebsocket;
using static LibKaseya.Enums;

namespace KLC {
    public class WsY1 {

        private LiveConnectSession Session;
        public int PortY { get; private set; }
        public string Module { get; private set; }

        private WatsonWsClient WebsocketY;
        public string Client;
        public int ClientPort;

        public WsY1(LiveConnectSession session, int portY) {
            //Type1 - is not started straight away
            Session = session;
            PortY = portY;
            Module = "controlagent";

            if (PortY == 0)
                throw new Exception();

            WebsocketY = new WatsonWsClient(new Uri("ws://127.0.0.1:" + PortY + "/control/agent"));

            WebsocketY.ServerConnected += WebsocketY1_ServerConnected;
            WebsocketY.ServerDisconnected += WebsocketY1_ServerDisconnected;
            WebsocketY.MessageReceived += WebsocketY1_MessageReceived;
            WebsocketY.Start();
        }

        private void WebsocketY1_MessageReceived(object sender, MessageReceivedEventArgs e) {
            //Session.Parent.LogText("Y1 Message");

            if (Client == null || Client == "") {
                Console.WriteLine("Y1 Needs to know B's client!");
                //Session.Parent.Log(Side.AdminEndPoint, PortY, PortY, "Needs to know B's client!");
                while (Client == null || Client == "") {
                    Task.Delay(10);
                }
            }

            //string messageY = Encoding.UTF8.GetString(e.Data);
            //Session.Parent.Log(Side.AdminEndPoint, PortY, WebsocketB.PortB, e.Data);

            throw new NotImplementedException();

            //WebsocketB.Send(Client, e.Data);
            //ServerB.SendAsync(Client, eY.Data, System.Net.WebSockets.WebSocketMessageType.Text);
        }

        private void WebsocketY1_ServerDisconnected(object sender, EventArgs e) {
            Console.WriteLine("Y1 Disconnected " + Module);
        }

        private void WebsocketY1_ServerConnected(object sender, EventArgs e) {
            Console.WriteLine("Y1 Connected " + Module);
        }

        public void Send(byte[] data) {
            try {
                WebsocketY.SendAsync(data).Wait();
            } catch (Exception ex) {
                Console.WriteLine(ex.ToString());
            }
        }

        public void Send(string messageB) {
            try {
                WebsocketY.SendAsync(messageB).Wait();
            } catch (Exception ex) {
                Console.WriteLine(ex.ToString());
            }
        }
    }
}
