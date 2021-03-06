﻿using Fleck;
using KLC_Finch;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static LibKaseya.Enums;

namespace KLC {
    public class WsB {

        private LiveConnectSession Session;

        private WebSocketServer ServerB;
        public int PortB { get; private set; }
        //private string Module;

        private IWebSocketConnection ServerBsocketControlAgent;
        private int clientPortControlAgent;
        private int clientPortRemoteControl;

        public WsB(LiveConnectSession session, int portB) {
            Session = session;

            //B - Find a free port me
            PortB = portB;

            //B - new WebSocketServer (my port B)
            ServerB = new WebSocketServer("ws://0.0.0.0:" + PortB);

            ServerB.Start(socket => {
                socket.OnOpen = () => {
                    ServerB_ClientConnected(socket);
                };
                socket.OnClose = () => {
                    ServerB_ClientDisconnected(socket);
                };
                socket.OnMessage = message => {
                    ServerB_MessageReceived(socket, message);
                };
                socket.OnPing = byteB => {
                    socket.SendPong(byteB);
                };
                socket.OnBinary = byteB => {
                    ServerB_BinaryReceived(socket, byteB);
                };
                socket.OnError = ex => {
                    if (ex.Message.Contains("forcibly closed"))
                        return;

                    //Console.WriteLine("B Error: " + ex.ToString());
                    App.ShowUnhandledExceptionFromSrc(ex, "Websocket B");
                };
            });
        }

        private void ServerB_MessageReceived(IWebSocketConnection socket, string message) {
            switch (socket.ConnectionInfo.Path) {
                case "/control/agent":
                    Console.WriteLine("ServerB Message Unhandled [ControlAgent]: " + message);
                    break;

                case "/app/dashboard":
                    Session.ModuleDashboard.Receive(message);
                    break;

                case "/app/staticimage":
                    Session.ModuleStaticImage.Receive(message);
                    break;

                case "/app/commandshell":
                case "/app/terminal":
                    Session.ModuleCommandTerminal.Receive(message);
                    break;

                case "/app/commandshellvt100":
                    Session.ModuleCommandPowershell.Receive(message);
                    break;

                case "/app/files":
                    Session.ModuleFileExplorer.Receive(message);
                    break;

                case "/app/registryeditor":
                    Session.ModuleRegistryEditor.Receive(message);
                    break;

                default:
                    Console.WriteLine("ServerB Message Unhandled: " + socket.ConnectionInfo.Path);
                    break;
            }
        }

        private void ServerB_BinaryReceived(IWebSocketConnection socket, byte[] data) {
            //Session.Parent.LogText("B MSG " + e.IpPor);
            //if (eB.HttpRequest.Url.PathAndQuery == "/control/agent") {

            if (socket.ConnectionInfo.Path == "/app/files/download") {
                if (Session.ModuleFileExplorer != null)
                    Session.ModuleFileExplorer.HandleDownload(data);
            } else if (socket.ConnectionInfo.Path == "/app/files/upload") {
                if (Session.ModuleFileExplorer != null)
                    Session.ModuleFileExplorer.HandleUpload(data);
            }
            else if(socket.ConnectionInfo.Path == "/app/staticimage") {
                if (Session.ModuleStaticImage != null)
                    Session.ModuleStaticImage.HandleBytes(data);
            } else if (socket.ConnectionInfo.ClientPort == clientPortRemoteControl) {
                //string sessionId = socket.ConnectionInfo.Path.Replace("/app/remotecontrol/", "");
                //e.g. /app/remotecontrol/3c3757ca-72f5-4a1a-ac51-5c457e731fd0
                if (Session.ModuleRemoteControl != null)
                    Session.ModuleRemoteControl.HandleBytesFromRC(data);
            } else {
                Console.WriteLine("ServerB Binary Unhandled: " + socket.ConnectionInfo.Path);
            }

            //string messageB = Encoding.UTF8.GetString(e.Data);
            //Console.WriteLine(messageB);

            /*
            string messageB = Encoding.UTF8.GetString(e.Data);
            if (messageB[0] == '{')
                client2.Send(messageB);
            else
                client2.Send(e.Data);
            */
        }

        private void ServerB_ClientDisconnected(IWebSocketConnection socket) {
            Console.WriteLine("B Close");

            if (Session.ModuleRemoteControl != null) {
                string sessionId = socket.ConnectionInfo.Path.Replace("/app/remotecontrol/", "");
                Session.ModuleRemoteControl.Disconnect(sessionId);
            }
        }

        private void ServerB_ClientConnected(IWebSocketConnection socket) {
            //Module = e.HttpRequest.Url.PathAndQuery.Split('/')[2];

            Console.WriteLine("B Connect (server port: " + PortB + ") " + socket.ConnectionInfo.Path);

            int clientPort = socket.ConnectionInfo.ClientPort;

            switch(socket.ConnectionInfo.Path) {
                case "/control/agent":
                    ServerBsocketControlAgent = socket;
                    clientPortControlAgent = clientPort;
                    if(Session.Callback != null)
                        Session.Callback();
                    break;

                case "/app/dashboard":
                    Session.ModuleDashboard.SetSocket(socket);
                    break;

                case "/app/staticimage":
                    Session.ModuleStaticImage.SetSocket(socket);
                    break;

                case "/app/commandshell":
                case "/app/terminal":
                    Session.ModuleCommandTerminal.SetSocket(socket);
                    break;

                case "/app/commandshellvt100":
                    Session.ModuleCommandPowershell.SetSocket(socket);
                    break;

                case "/app/files":
                    Session.ModuleFileExplorer.SetSocket(socket);
                    break;

                case "/app/files/download":
                    Session.ModuleFileExplorer.SetDownloadSocket(socket);
                    break;

                case "/app/files/upload":
                    Session.ModuleFileExplorer.SetUploadSocket(socket);
                    break;

                case "/app/registryeditor":
                    Session.ModuleRegistryEditor.SetSocket(socket);
                    break;

                default:
                    if(socket.ConnectionInfo.Path.StartsWith("/app/remotecontrol/")) {
                        clientPortRemoteControl = clientPort;
                        Session.ModuleRemoteControl.SetSocket(socket, clientPort);
                    } else {
                        Console.WriteLine("Unexpected: " + socket.ConnectionInfo.Path);
                    }
                    break;
            }

        }

        public bool ControlAgentIsReady() {
            return (ServerBsocketControlAgent != null);
        }

        public void Close() {
            //ServerBsocket.Close();
            ServerB.ListenerSocket.Close();
        }

        public void Send(IWebSocketConnection socket, byte[] data) {
            socket.Send(data);
        }

        public void Send(IWebSocketConnection socket, string message) {
            socket.Send(message);
        }

        public string StartModuleRemoteControl(bool modePrivate) {
            string guidGenSessionId = Guid.NewGuid().ToString();
            string guidGenSessionTokenId = Guid.NewGuid().ToString();
            string guidGenId = Guid.NewGuid().ToString();
            string guidGenP2pConnectionId = Guid.NewGuid().ToString();

            string json1 = "{\"data\":{\"rcPolicy\":{\"AdminGroupId\":" + Session.auth.RoleId + ",\"AgentGuid\":\"" + Session.agentGuid + "\",\"AskText\":\"\",\"Attributes\":null,\"EmailAddr\":null,\"JotunUserAcceptance\":null,\"NotifyText\":\"\",\"OneClickAccess\":null,\"RecordSession\":null,\"RemoteControlNotify\":1,\"RequireRcNote\":null,\"RequiteFTPNote\":null,\"TerminateNotify\":null,\"TerminateText\":\"\"},\"sessionId\":\"" + guidGenSessionId + "\",\"sessionTokenId\":\"" + guidGenSessionTokenId + "\",\"sessionType\":\"" + (modePrivate ? "Private" : "Shared") + "\"},\"id\":\"" + guidGenId + "\",\"p2pConnectionId\":\"" + guidGenP2pConnectionId + "\",\"type\":\"RemoteControl\"}";

            if (ServerBsocketControlAgent == null)
                //throw new Exception("Agent offline?");
                return null;

            ServerBsocketControlAgent.Send(json1);

            return guidGenSessionId;

            /*
            JObject jMain = new JObject();
            jMain["id"] = randTaskUUID2; //fixed3
            jMain["p2pConnectionId"] = randSessionGuid; //fixed4
            jMain["type"] = module; //was moduleId?
            JObject jData = new JObject();
            jData["rcPolicy"]
                jData["sessionId"] //fixed1
                jData["sessionTokenId"] //fixed2
                jData["sessionType"] // (Private/Shared)
            jMain["data"] = jData;
            */

            /*
            {
                "data": {
                    "rcPolicy": {
                        "AdminGroupId": removed,
                        "AgentGuid": "429424626294329",
                        "AskText": "",
                        "Attributes": null,
                        "EmailAddr": null,
                        "JotunUserAcceptance": null,
                        "NotifyText": "",
                        "OneClickAccess": null,
                        "RecordSession": null,
                        "RemoteControlNotify": 1,
                        "RequireRcNote": null,
                        "RequiteFTPNote": null,
                        "TerminateNotify": null,
                        "TerminateText": ""
                    },
                    "sessionId": "ad036086-dc95-4ac7-bad6-1df2d00cefef",
                    "sessionTokenId": "d12e3b7e-3756-45bf-a07a-16f9ffb78e8b",
                    "sessionType": "Private"
                },
                "id": "ea0bdb9e-f119-4113-acf8-5eba1b493d37",
                "p2pConnectionId": "b2581441-fca4-4a6c-8ebe-545cee582fde",
                "type": "RemoteControl"
            }
            */
        }

        public void ControlAgentSendTask(string module) {
            string randTaskUUID2 = Guid.NewGuid().ToString(); //Kaseya use a generic UUID v4 generator
            string randSessionGuid = Guid.NewGuid().ToString(); //Not sure if okay to be random

            JObject jMain = new JObject();
            jMain["type"] = "Task";
            jMain["id"] = randTaskUUID2;
            jMain["p2pConnectionId"] = randSessionGuid;
            JObject jData = new JObject();
            jData["moduleId"] = module;
            jData["url"] = "https://KASEYAVSAHOST/api/v1.5/endpoint/download/packages/" + module + "/9.5.0.376/content";
            jMain["data"] = jData;

            if (ServerBsocketControlAgent != null)
                ServerBsocketControlAgent.Send(jMain.ToString());
            //else throw new Exception("Agent offline?");
        }

        public void ControlAgentSendStaticImage(int height, int width) {
            string randTaskUUID2 = Guid.NewGuid().ToString(); //Kaseya use a generic UUID v4 generator
            string randSessionGuid = Guid.NewGuid().ToString(); //Not sure if okay to be random

            JObject jMain = new JObject();
            jMain["type"] = "StaticImage";
            jMain["id"] = randTaskUUID2;
            jMain["p2pConnectionId"] = randSessionGuid;
            JObject jData = new JObject();
            jData["height"] = height;
            jData["width"] = width;
            jMain["data"] = jData;

            if (ServerBsocketControlAgent != null)
                ServerBsocketControlAgent.Send(jMain.ToString());
            else
                throw new Exception("Agent offline?");
        }

    }
}
