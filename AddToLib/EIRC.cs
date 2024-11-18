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
            string[] temp = session_jwt.Replace('-', '+').Replace('_', '/').Split('.');
            session_jwt_p1 = Util.DecodeBase64(temp[0]);
            session_jwt_p2 = Util.DecodeBase64(temp[1]);
            session_jwt_p3 = temp[2];
			//Part 3 is a JWT signature, as long as you don't manipulate the header (1) or payload (2) it should be fine to replay

            endpoint_id = (string)JSON["endpoint_id"];
            session_token_id = (string)JSON["session_token_id"];
        }
    }
}
