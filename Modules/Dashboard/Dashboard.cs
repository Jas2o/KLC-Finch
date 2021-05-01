using Fleck;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web;
using System.Windows.Controls;

namespace KLC_Finch {
    public class Dashboard {

        private static string modulename = "dashboard";
        private TextBox txtBox;
        private StackPanel stackDisks;
        private IWebSocketConnection serverB;

        private KLC.LiveConnectSession session;
        System.Timers.Timer timerStart;
        System.Timers.Timer timerRefresh;

        public Dashboard(KLC.LiveConnectSession session, TextBox txtBox = null, StackPanel stackDisks=null) {
            this.session = session;
            this.txtBox = txtBox;
            this.stackDisks = stackDisks;

            timerStart = new System.Timers.Timer(1000);
            timerStart.Elapsed += TimerStart_Elapsed;
            timerStart.Start();

            //timerRefresh = new System.Timers.Timer(30000);
            //timerRefresh.Elapsed += TimerRefresh_Elapsed;
        }

        public void SetSocket(IWebSocketConnection ServerBsocket) {
            this.serverB = ServerBsocket;
        }

        private void TimerStart_Elapsed(object sender, System.Timers.ElapsedEventArgs e) {
            if (session == null) {
                timerStart.Stop();
            } else if (session.WebsocketB.ControlAgentIsReady()) {
                timerStart.Stop();
                session.WebsocketB.ControlAgentSendTask(modulename);
            }
        }

        /*
        private void TimerRefresh_Elapsed(object sender, System.Timers.ElapsedEventArgs e) {
            RequestRefresh();
        }
        */

        public void GetCpuRam() {
            if (serverB != null) {
                JObject jAction = new JObject();
                jAction["action"] = "GetCpuRam";
                serverB.Send(jAction.ToString());
            }
        }

        public void GetTopEvents() {
            if (serverB != null) {
                JObject jAction = new JObject();
                jAction["action"] = "GetTopEvents";
                serverB.Send(jAction.ToString());
            }
        }

        public void GetTopProcesses() {
            if (serverB != null) {
                JObject jAction = new JObject();
                jAction["action"] = "GetTopProcesses";
                serverB.Send(jAction.ToString());
            }
        }

        public void GetVolumes() {
            if (serverB != null) {
                JObject jAction = new JObject();
                jAction["action"] = "GetVolumes";
                serverB.Send(jAction.ToString());
            }
        }

        public void Receive(string message) {
            txtBox.Dispatcher.Invoke(new Action(() => {
                dynamic temp = JsonConvert.DeserializeObject(message);
                switch (temp["action"].ToString()) {
                    case "ScriptReady":
                        JObject jStartDashboardData = new JObject();
                        jStartDashboardData["action"] = "StartDashboardData";
                        serverB.Send(jStartDashboardData.ToString());
                        break;

                    case "VolumesData":
                        //{"action":"VolumesData","data":[{"label":"C:\\","free":129323569152,"total":254956666880,"type":"Fixed"}],"errors":[]}
                        stackDisks.Children.Clear();

                        foreach (dynamic v in temp["data"].Children()) {
                            if ((string)v["type"] != "Fixed")
                                continue;

                            controlDisk disk = new controlDisk((string)v["label"], (long)v["total"], (long)v["free"]);
                            stackDisks.Children.Add(disk);
                        }

                        break;

                    case "EventsData":
                        /*{
                           "action":"EventsData",
                           "data":[
                              {
                                 "type":"Warning",
                                 "timestamp":"2021-05-01T07:20:41.802Z",
                                 "description":"desc here",
                                 "additional":""
                              },
                              {
                                 "type":"Info",
                                 "timestamp":"2021-05-01T07:17:12.019Z",
                                 "description":"desc here",
                                 "additional":""
                              }
                           ],
                           "errors":[]
                        }*/
                        break;

                    case "TopProcData":
                        /* {
                           "action":"TopProcData",
                           "topCpu":[
                              { "pid":"4", "name":"System", "user":"NT AUTHORITY\\SYSTEM", "mem":"0.21", "cpu":"0.7" },
                              { "pid":"33892", "name":"ServiceHub.ThreadedWaitDialog.exe", "user":"company\\username", "mem":"73.54", "cpu":"0.4" }
                           ],
                           "topMem":[
                              { "pid":"5020", "name":"SavService.exe", "user":"NT AUTHORITY\\LOCAL SERVICE", "mem":"353.78", "cpu":"0.0" },
                              { "pid":"34284", "name":"devenv.exe", "user":"company\\username", "mem":"320.46", "cpu":"0.0" }
                           ],
                           "errors":[]
                        } */
                        //break;

                    case "CpuRamData":
                        //{"action":"CpuRamData","data":{"ram":46,"cpu":1.6440618016222541},"errors":[]}
                        //break;

                    default:
                        txtBox.AppendText("Dashboard message: " + message + "\r\n");
                        break;
                }
            }));
        }
    }
}
