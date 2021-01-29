using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WatsonWebsocket;
using static LibKaseya.Enums;

namespace KLC {
    public class WsZ {

        private LiveConnectSession Session;
        private WatsonWsClient WebsocketZ;
        
        public int PortZ { get; private set; }
        private string Module;

        public WsZ(LiveConnectSession session, int port) {
            Session = session;
            PortZ = port;
            Module = "controladmin";

            //LC Z - Live Connect found a free port

            //MM Z - We start a WebSocketClient and connect to Z
            WebsocketZ = new WatsonWsClient(new Uri("ws://127.0.0.1:" + PortZ + "/control/admin"));
            //For reasons I do not understand, if you use 'localhost' instead of '127.0.0.1', you'll miss out on the first message.

            WebsocketZ.ServerConnected += WebsocketZ_ServerConnected;
            WebsocketZ.ServerDisconnected += WebsocketZ_ServerDisconnected;
            WebsocketZ.MessageReceived += WebsocketZ_MessageReceived;

            WebsocketZ.Start();
        }

        private void WebsocketZ_ServerConnected(object sender, EventArgs e) {
            Console.WriteLine("Z Connect (server port: " + PortZ + ") /control/admin");
        }

        public void Send(string message) {
            try {
                WebsocketZ.SendAsync(message).Wait();
            } catch(Exception ex) {
                Console.WriteLine(ex.ToString());
            }
        }

        private void WebsocketZ_ServerDisconnected(object sender, EventArgs e) {
            Console.WriteLine("Z Socket closed: " + e.ToString());
        }

        private void WebsocketZ_MessageReceived(object sender, MessageReceivedEventArgs e) {
            //Z - Tells us who it is, and to start a module on port Y
            string message = Encoding.UTF8.GetString(e.Data);
            dynamic json = JsonConvert.DeserializeObject(message);
            if (json["data"]["connectPort"] != null) {
                Console.WriteLine("Z connectPort");

                int portY = json["data"]["connectPort"];

                WsY1 wsy1 = new WsY1(Session, portY);
                Session.listY1Client.Add(wsy1);

                throw new NotImplementedException();

                //WsB wsb = new WsB(Session, wsy1); //This creates Y2
                //Session.listBsocket.Add(wsb);
            } else {
                Console.WriteLine("Z Else");

                throw new NotImplementedException();
                //Session.WebsocketA.Send(message);
            }
            //Y - Tells us more about the module's demands (e.g. remote control)
        }

    }
}
