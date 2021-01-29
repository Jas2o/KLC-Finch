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

            string[] temp = session_jwt.Split('.');
            session_jwt_p1 = Util.DecodeBase64(temp[0]);
            //Part 1 = {"alg":"RS256","x5u":"https://vsa-web.company.com.au/api/v1.0/endpoints/x5u/a2402b6b-1329-40bd-b7bf-39dd5d5d7e5e/A488AA6011D0AB959722AFE4DF4178BC8BCECE13"}
            session_jwt_p2 = Util.DecodeBase64(temp[1].Replace("_", ""));
            //Part 2 = {"sub":"removed","kaseya_endpoint":"65257dfd-3725-4051-ad10-70b1d81b621d","exp":1578566302,"kaseya_remotecontrolpolicy":{"EmailAddr":null,"AgentGuid":"429424626294329","AdminGroupId":removed,"RemoteControlNotify":1,"NotifyText":"","AskText":"","TerminateNotify":null,"TerminateText":"","RequireRcNote":null,"RequiteFTPNote":null,"RecordSession":null,"OneClickAccess":null,"JotunUserAcceptance":null,"PartitionId":null,"Attributes":null},"iss":"a2402b6b-1329-40bd-b7bf-39dd5d5d7e5e","aud":"kaseya.vsa.authentication"}
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
