using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace KLC.Structure
{
    public class EIRC //EndpointsInitiateRemoteControl
    {
        public string json;

        public string session_jwt;
        public string endpoint_id;
        public string session_token_id; //Not too useful

        public string session_jwt_p1;
        public string session_jwt_p2;
        public string session_jwt_p3;

        public EIRC(string IRestContent)
        {
            json = IRestContent;
            dynamic JSON = JsonConvert.DeserializeObject(IRestContent);

            session_jwt = (string)JSON["session_jwt"];
            //"removed.removed.removed"

            string[] temp = session_jwt.Replace('-', '+').Replace('_', '/').Split('.');
            session_jwt_p1 = Util.DecodeBase64(temp[0]);
            //Part 1 = {"alg":"RS256","x5u":"https://vsa-web.company.com.au/api/v1.0/endpoints/x5u/a2402b6b-1329-40bd-b7bf-39dd5d5d7e5e/A488AA6011D0AB959722AFE4DF4178BC8BCECE13"}
            session_jwt_p2 = Util.DecodeBase64(temp[1]);
            //Part 2 = {"sub":"removed","kaseya_endpoint":"85ecef8d-d2f4-418b-90cb-debe9861003b","exp":1627378824,"kaseya_remotecontrolpolicy":{"EmailAddr":null,"AgentGuid":"111111111111111","AdminGroupId":0,"RemoteControlNotify":2,"NotifyText":"The system administrator company.com.au/username is about to remote control your computer.","AskText":"The system administrator company.com.au/username is asking to remotely control your computer. OK to allow remote control?","TerminateNotify":0,"TerminateText":"The system administrator company.com.au/username has terminated the remote control session.","RequireRcNote":0,"RequiteFTPNote":0,"RecordSession":0,"OneClickAccess":1,"JotunUserAcceptance":null,"DisallowFileTransfers":0,"PartitionId":null,"Attributes":null},"userName":"KaseyaAdmin","passWord":"","adminIp":"120.150.78.96","remoteControlSettings":"{\"tsConsole\":0,\"tsFullScreen\":1,\"tsDisks\":0,\"tsPrinters\":0,\"tsWallpaper\":0,\"deskWidth\":1024,\"deskHeight\":768,\"sharedDiskList\":\"\"}","iss":"a2402b6b-1329-40bd-b7bf-39dd5d5d7e5e","aud":"kaseya.vsa.authentication"}

            session_jwt_p3 = temp[2];
            //Part 3 ??? = removed
			
			//Part 3 is a JWT signature, as long as you don't manipulate the header (1) or payload (2) it should be fine to replay

            endpoint_id = (string)JSON["endpoint_id"];
            //"65257dfd-3725-4051-ad10-70b1d81b621d"

            session_token_id = (string)JSON["session_token_id"];
            //"6c0e02b3-b7ad-4d39-bceb-ed6d56f49e5a"
            //For now the session_token_id isn't useful
        }
    }
}
