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
            //"removed"

            string[] temp = auth_jwt.Split('.');

            auth_jwt_p1 = Util.DecodeBase64(temp[0]);
            //Part 1 = {"alg":"RS256","x5u":"https://vsa-web.company.com.au/api/v1.0/endpoints/x5u/a2402b6b-1329-40bd-b7bf-39dd5d5d7e5e/A488AA6011D0AB959722AFE4DF4178BC8BCECE13"}
            //The same as session_jwt ?

            auth_jwt_p2 = Util.DecodeBase64(temp[1]);
            //Part 2 = {"sub":"removed","exp":1578314228,"kaseya_tenant":"1","kaseya_type":"Administrator","iss":"a2402b6b-1329-40bd-b7bf-39dd5d5d7e5e","aud":"kaseya.vsa.authentication"}

            auth_jwt_p3 = temp[2];
            //Part 3 ??? = removed

            //Part 3 is a JWT signature, as long as you don't manipulate the header (1) or payload (2) it should be fine to replay

            /*
            string certificate = "removed==";

            var publicKey = new X509Certificate2(Convert.FromBase64String(certificate)).GetRSAPublicKey();
            var privateKey = new X509Certificate2(Convert.FromBase64String(certificate)).GetRSAPrivateKey();
            string decoded = JWT.Decode(auth_jwt, publicKey, JwsAlgorithm.RS256);

            string encoded = JWT.Encode(auth_jwt_p2, privateKey, JwsAlgorithm.RS256);

            Console.WriteLine();
            */
        }

    }
}
