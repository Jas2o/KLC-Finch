using KLC_Finch;
using LibKaseya;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace KLC {

    public class LiveConnectSession {
        public static List<LiveConnectSession> listSession = new List<LiveConnectSession>();

        public WsM WebsocketM;

        public WsA WebsocketA;
        public WsB WebsocketB;

        public string shorttoken; //KLC would normally change tokens during initial connection
        public string agentGuid;
        public Agent agent;
        public KaseyaAuth Auth { get; private set; }

        public Structure.EAL Eal { get; private set; }
        public Structure.EIRC Eirc { get; private set; }
        public string RandSessionGuid { get; private set; }
        public Enums.NotifyApproval RCNotify { get; private set; }

        public Dashboard ModuleDashboard;
        public StaticImage ModuleStaticImage; //Sub-module of Dashboard
        public RemoteControl ModuleRemoteControl;
        public CommandTerminal ModuleCommandTerminal;
        public CommandPowershell ModuleCommandPowershell;
        public FileExplorer ModuleFileExplorer;
        public RegistryEditor ModuleRegistryEditor;
        public KLC_Finch.Modules.Events ModuleEvents;
        public KLC_Finch.Modules.Services ModuleServices;
        public KLC_Finch.Modules.Processes ModuleProcesses;

        public WindowAlternative.HasConnected Callback;

        public LiveConnectSession(string shortToken, string agentID, WindowAlternative.HasConnected callback = null) {
            agentGuid = agentID;
            shorttoken = shortToken;
            Callback = callback;
            Kaseya.LoadToken(shortToken);
            agent = new Agent(agentGuid);

            Auth = KaseyaAuth.ApiAuthX(shorttoken);
            shortToken = Auth.Token; //Works fine without this line, but it's something KLC does.
            Eal = Api15.EndpointsAdminLogin(shorttoken);

            JObject rcNotifyPolicy = agent.GetRemoteControlNotifyPolicyFromAPI();
            if (rcNotifyPolicy["Result"] != null) {
                if (rcNotifyPolicy["Result"]["RemoteControlNotify"] != null)
                    RCNotify = (Enums.NotifyApproval)(int)rcNotifyPolicy["Result"]["RemoteControlNotify"];
            } else
                return; //AgentID doesn't exist?

            ////dynamic agentSettings = agent.GetAgentSettingsInfoFromAPI(shorttoken);
            ////dynamic auditSummary = agent.GetAgentAuditSummaryFromAPI(shorttoken);

            Eirc = Api15.EndpointsInitiateRemoteControl(shorttoken, agentGuid);
            RandSessionGuid = Guid.NewGuid().ToString();

            //jsonAgentSettings = Api10.AssetmgmtAgentSettings(shorttoken, agentGuid);
            //jsonAgentSummary = Api10.AssetmgmtAuditSummary(shorttoken, agentGuid);
            //jsonRemoteControlNotifyPolicy = Api10.RemoteControlNotifyPolicy(shorttoken, agentGuid);

            int PortB = GetNewPort();

            //if(WsM.CertificateExists()) WebsocketM = new WsM(this, GetNewPort());

            WebsocketB = new WsB(this, PortB);
            WebsocketA = new WsA(this, GetNewPort(), PortB);
        }

        public static int GetNewPort() {
            TcpListener tcpListener = new TcpListener(IPAddress.Parse("127.0.0.1"), 0);
            tcpListener.Start();
            int port = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
            tcpListener.Stop();

            return port;
        }

        public void Close() {
            if (WebsocketB != null)
                WebsocketB.Close();
            if (WebsocketA != null)
                WebsocketA.Close();
        }

        public string GetWiresharkFilter() {
            string filter = string.Format("(tcp.srcport == {0}) || (tcp.dstport == {0})", WebsocketA.PortA);
            filter += string.Format(" || (tcp.srcport == {0}) || (tcp.dstport == {0})", WebsocketB.PortB);

            return filter;
        }
    }
}