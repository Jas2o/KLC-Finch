using KLC.Structure;
using RestSharp;

namespace KLC {
    class Api15
    {

        public static EAL EndpointsAdminLogin(string vsa, string shorttoken)
        {
            RestResponse response = LibKaseya.Kaseya.GetRequest(vsa, "api/v1.5/endpoints/adminlogon");

            /*
            RestClient K_Client = new RestClient("https://" + vsa)
            {
                Timeout = 5000
            };
            RestRequest request = new RestRequest("api/v1.5/endpoints/adminlogon", Method.GET);
            request.AddHeader("Authorization", "Bearer " + shorttoken);
            RestResponse response = K_Client.Execute(request);
            */

            return new EAL(response.Content);
        }

        public static EIRC EndpointsInitiateRemoteControl(string vsa, string shorttoken, string agentguid)
        {
            RestResponse response = LibKaseya.Kaseya.GetRequest(vsa, "api/v1.5/endpoints/" + agentguid + "/initiateremotecontrol");

            /*
            RestClient K_Client = new RestClient("https://" + vsa)
            {
                Timeout = 5000
            };
            RestRequest request = new RestRequest("api/v1.5/endpoints/" + agentguid + "/initiateremotecontrol", Method.GET);
            request.AddHeader("Authorization", "Bearer " + shorttoken);
            //request.AddParameter("Content-Type", "application/json");
            RestResponse response = K_Client.Execute(request);
            */

            if(response.StatusCode == System.Net.HttpStatusCode.OK)
                return new EIRC(response.Content);

            return null;
        }

    }
}
