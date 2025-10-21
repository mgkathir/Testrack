using BureauAdaptor.Helper;
using Newtonsoft.Json;
using RestSharp;
using RestSharp.Authenticators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BureauAdaptor.BureauProcessor
{
    internal class EFXProcessor
    {
        private readonly ILogger<Worker> _logger;
        private readonly Settings _settings;
        private readonly RestClient _client;
        private static readonly string EfxUrl = "https://api.uat.equifax.com/business/sts-reports/v1/report";
        private static readonly string EfxUserName = "J3jB7ZFKGWhamKrINNoodHG7jbO6Oeol";
        private static readonly string EfxPassword = "TIpjexgDcC9C1M8H";
        private static readonly int EfxTimeout = 3800;

        public EFXProcessor(ILogger<Worker> logger, Settings settings)
        {
            _logger = logger;
            _settings = settings;
            if (_client == null)
            {
                var options = new RestClientOptions()
                {
                    BaseUrl = new Uri(settings?.BureauSettings?.URL ?? EfxUrl),
                    MaxTimeout = settings?.BureauSettings?.Timeout ?? EfxTimeout,
                    ThrowOnAnyError = true
                };
                _client = new RestClient(options);
            }
        }

        public RestClient GetRestclient()
        {
            return _client;
        }

        public string PostRequest(string Content,string RemotePointInfo)
        {
            string EFXResponse = string.Empty;

            if (Content.Length == 0)
                return EFXResponse;

            string efxusername = _settings?.BureauSettings?.UserName ?? EfxUserName;
            string efxPassword = _settings?.BureauSettings?.Password ?? EfxPassword;

            JsonRequestResponse Request = new JsonRequestResponse(Content.Substring(6));
            string jsondata = JsonConvert.SerializeObject(Request);

            var XmlRequest = new RestRequest("", RestSharp.Method.Post);
            XmlRequest.AddHeader("Content-Type", "application/vnd.equifax.json+fff");
            _client.Authenticator = new HttpBasicAuthenticator(efxusername, efxPassword);
            XmlRequest.AddParameter("text/xml", jsondata, ParameterType.RequestBody);

            RestResponse Response = GetRestclient().PostAsync(XmlRequest).GetAwaiter().GetResult();

            if (Response != null && Response.StatusCode == System.Net.HttpStatusCode.OK && Response.Content != null)
            {
                _logger.LogInformation("EFX response received.....for : "+ RemotePointInfo);
                JsonRequestResponse? response = JsonConvert.DeserializeObject<JsonRequestResponse>(Response.Content);

                EFXResponse = response?.payload.Length.ToString("000000") + response?.payload;
            }
            else
            {
                _logger.LogInformation("EFX Error Response for client: "+ RemotePointInfo +" : " + Response?.Content);
            }
            return EFXResponse;
        }

        public string ExecuteRequest(string Content, string RemotePointInfo)
        {
            string EFXResponse = string.Empty;

            if (Content.Length == 0)
                return EFXResponse;

            string efxusername = _settings?.BureauSettings?.UserName ?? EfxUserName;
            string efxPassword = _settings?.BureauSettings?.Password ?? EfxPassword;
            int efxTimeOut = _settings?.BureauSettings?.Timeout ?? EfxTimeout;

            JsonRequestResponse Request = new JsonRequestResponse(Content.Substring(6));
            string jsondata = JsonConvert.SerializeObject(Request);

            var XmlRequest = new RestRequest("", RestSharp.Method.Post);
            XmlRequest.AddHeader("Content-Type", "application/vnd.equifax.json+fff");
            _client.Authenticator = new HttpBasicAuthenticator(efxusername, efxPassword);
            XmlRequest.AddParameter("text/xml", jsondata, ParameterType.RequestBody);
            XmlRequest.Timeout = efxTimeOut;

            RestResponse Response = GetRestclient().ExecuteAsync(XmlRequest).GetAwaiter().GetResult();

            if (Response != null && Response.StatusCode == System.Net.HttpStatusCode.OK && Response.Content != null)
            {
                _logger.LogInformation("EFX response received.....for : "+ RemotePointInfo);
                JsonRequestResponse? response = JsonConvert.DeserializeObject<JsonRequestResponse>(Response.Content);

                EFXResponse = response?.payload.Length.ToString("000000") + response?.payload;
            }
            else
            {
                _logger.LogInformation("EFX Error Response for client: "+ RemotePointInfo +" : " + Response?.Content);
            }
            return EFXResponse;
        }
    }
}
