using Fleck;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace KLC_Finch.Modules {
    public class Services {

        private static readonly string modulename = "services";
        private IWebSocketConnection serverB;

        private readonly ServicesData servicesData;

        public Services(KLC.LiveConnectSession session, ServicesData servicesData) {
            this.servicesData = servicesData;

            if (session != null)
                session.WebsocketB.ControlAgentSendTask(modulename);
        }

        public void SetSocket(IWebSocketConnection ServerBsocket) {
            this.serverB = ServerBsocket;
        }

        public void Receive(string message) {
            dynamic temp = JsonConvert.DeserializeObject(message);
            switch (temp["action"].ToString()) {
                case "ScriptReady":
                    RequestListServices();
                    break;

                case "ListServices":
                case "StartService":
                case "StopService":
                case "SetStartupType":
                    //There's no RestartService, you get a Stop and Start response
                    /*{
                        "action":"ListServices",
                        "success":true,
                        "serviceName":null, //Name of service admin changed
                        "displayName":null,
                        "contentsList":[
                    */

                    if (temp["contentsList"] != null) {
                        //servicesData.ServicesClear(); //Probably should update what's already there
                        foreach (dynamic s in temp["contentsList"].Children()) {
                            ServiceValue sv = new ServiceValue(s);
                            servicesData.ServicesAdd(sv);
                        }
                    }

                    break;

                default:
                    Console.WriteLine("Events message received: " + message);
                    break;
            }
        }

        public void Start(ServiceValue sv) {
            JObject jEvent = new JObject {
                ["action"] = "StartService",
                ["serviceName"] = sv.ServiceName,
                ["displayName"] = sv.DisplayName
            };
            serverB.Send(jEvent.ToString());
        }

        public void Stop(ServiceValue sv) {
            JObject jEvent = new JObject {
                ["action"] = "StopService",
                ["serviceName"] = sv.ServiceName,
                ["displayName"] = sv.DisplayName
            };
            serverB.Send(jEvent.ToString());
        }

        public void Restart(ServiceValue sv) {
            JObject jEvent = new JObject {
                ["action"] = "RestartService",
                ["serviceName"] = sv.ServiceName,
                ["displayName"] = sv.DisplayName
            };
            serverB.Send(jEvent.ToString());
        }

        public void SetAuto(ServiceValue sv) {
            JObject jEvent = new JObject {
                ["action"] = "SetStartupType",
                ["serviceName"] = sv.ServiceName,
                ["displayName"] = sv.DisplayName,
                ["startupType"] = "auto"
            };
            serverB.Send(jEvent.ToString());
        }

        public void SetManual(ServiceValue sv) {
            JObject jEvent = new JObject {
                ["action"] = "SetStartupType",
                ["serviceName"] = sv.ServiceName,
                ["displayName"] = sv.DisplayName,
                ["startupType"] = "manual"
            };
            serverB.Send(jEvent.ToString());
        }

        public void SetDisabled(ServiceValue sv) {
            JObject jEvent = new JObject {
                ["action"] = "SetStartupType",
                ["serviceName"] = sv.ServiceName,
                ["displayName"] = sv.DisplayName,
                ["startupType"] = "disabled"
            };
            serverB.Send(jEvent.ToString());
        }

        public void RequestListServices() {
            JObject jStartData = new JObject {
                ["action"] = "ListServices"
            };
            serverB.Send(jStartData.ToString());
        }

    }
}
