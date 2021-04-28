using Fleck;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web;
using System.Windows.Controls;

namespace KLC_Finch.Modules {
    public class Services {

        private static string modulename = "services";
        private IWebSocketConnection serverB;

        private ServicesData servicesData;

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
                        servicesData.ServicesClear();
                        //Probably should update what's already there

                        foreach (dynamic s in temp["contentsList"].Children()) {
                            ServiceValue sv = new ServiceValue(s);
                            servicesData.ServicesAdd(sv);
                        }

                        //UpdateDisplayValues();
                    }

                    break;

                default:
                    Console.WriteLine("Events message received: " + message);
                    break;
            }
        }

        public void Start(ServiceValue sv) {
            JObject jEvent = new JObject();
            jEvent["action"] = "StartService";
            jEvent["serviceName"] = sv.ServiceName;
            jEvent["displayName"] = sv.DisplayName;
            serverB.Send(jEvent.ToString());
        }

        public void Stop(ServiceValue sv) {
            JObject jEvent = new JObject();
            jEvent["action"] = "StopService";
            jEvent["serviceName"] = sv.ServiceName;
            jEvent["displayName"] = sv.DisplayName;
            serverB.Send(jEvent.ToString());
        }

        public void Restart(ServiceValue sv) {
            JObject jEvent = new JObject();
            jEvent["action"] = "RestartService";
            jEvent["serviceName"] = sv.ServiceName;
            jEvent["displayName"] = sv.DisplayName;
            serverB.Send(jEvent.ToString());
        }

        public void SetAuto(ServiceValue sv) {
            JObject jEvent = new JObject();
            jEvent["action"] = "SetStartupType";
            jEvent["serviceName"] = sv.ServiceName;
            jEvent["displayName"] = sv.DisplayName;
            jEvent["startupType"] = "auto";
            serverB.Send(jEvent.ToString());
        }

        public void SetManual(ServiceValue sv) {
            JObject jEvent = new JObject();
            jEvent["action"] = "SetStartupType";
            jEvent["serviceName"] = sv.ServiceName;
            jEvent["displayName"] = sv.DisplayName;
            jEvent["startupType"] = "manual";
            serverB.Send(jEvent.ToString());
        }

        public void SetDisabled(ServiceValue sv) {
            JObject jEvent = new JObject();
            jEvent["action"] = "SetStartupType";
            jEvent["serviceName"] = sv.ServiceName;
            jEvent["displayName"] = sv.DisplayName;
            jEvent["startupType"] = "disabled";
            serverB.Send(jEvent.ToString());
        }

        public void RequestListServices() {
            JObject jStartData = new JObject();
            jStartData["action"] = "ListServices";
            serverB.Send(jStartData.ToString());
        }

    }
}
