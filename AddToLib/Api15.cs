using KLC.Structure;
using RestSharp;

namespace KLC {
    class Api15
    {

        public static EAL EndpointsAdminLogin(string shorttoken)
        {
            RestClient K_Client = new RestClient("https://vsa-web.company.com.au");
            RestRequest request = new RestRequest("api/v1.5/endpoints/adminlogon", Method.GET);
            request.AddHeader("Authorization", "Bearer " + shorttoken);
            //request.AddParameter("Content-Type", "application/json");
            IRestResponse response = K_Client.Execute(request);

            return new EAL(response.Content);
        }

        public static EIRC EndpointsInitiateRemoteControl(string shorttoken, string agentguid)
        {
            RestClient K_Client = new RestClient("https://vsa-web.company.com.au");
            RestRequest request = new RestRequest("api/v1.5/endpoints/" + agentguid + "/initiateremotecontrol", Method.GET);
            request.AddHeader("Authorization", "Bearer " + shorttoken);
            //request.AddParameter("Content-Type", "application/json");
            IRestResponse response = K_Client.Execute(request);
            if(response.StatusCode == System.Net.HttpStatusCode.OK)
                return new EIRC(response.Content);

            return null;
        }

    }
}
