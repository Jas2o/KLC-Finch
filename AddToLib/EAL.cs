using System;
using System.Collections.Generic;
using System.Text;

namespace KLC.Structure
{
    public class EAL
    {
        public string auth_jwt;

        public string auth_jwt_p1;
        public string auth_jwt_p2;
        public string auth_jwt_p3;

        public EAL(string IRestContent)
        {
            auth_jwt = IRestContent;
            string[] temp = auth_jwt.Split('.');
            auth_jwt_p1 = Util.DecodeBase64(temp[0]);
            auth_jwt_p2 = Util.DecodeBase64(temp[1]);
            auth_jwt_p3 = temp[2];
            //Part 3 is a JWT signature, as long as you don't manipulate the header (1) or payload (2) it should be fine to replay
        }

    }
}
