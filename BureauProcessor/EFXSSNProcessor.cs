using BureauAdaptor.Helper;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BureauAdaptor.BureauProcessor
{
    internal class EFXSSNProcessor
    {
        private readonly ILogger<Worker> _logger;
        private readonly Settings _settings;
        private readonly RestClient _client;
        
        private static readonly string _efxssnUrl = "https://api.uat.equifax.com/business/dtec/v1/report-requests";
        private static readonly int _efxssnTimeout = 3800;

        public EFXSSNProcessor(ILogger<Worker> logger, Settings settings)
        {
            _logger = logger;
            _settings = settings;
            if (_client == null)
            {
                var options = new RestClientOptions()
                {
                    BaseUrl = new Uri(settings?.BureauSettings?.URL ?? _efxssnUrl),
                    MaxTimeout = settings?.BureauSettings?.Timeout ?? _efxssnTimeout
                };
                _client = new RestClient(options)
                {
                    Authenticator = new EFXSSNAuthenticator(_logger, _settings)
                };
            }
        }

        private RestClient GetRestclient()
        {
            return _client;
        }

        public string ExecuteRequest(string Content, string RemotePointInfo)
        {
            string EFXSSNResponse = string.Empty;
            if (Content.Length == 0)
                return EFXSSNResponse;
            var XmlRequest = new RestRequest("", RestSharp.Method.Post);
            XmlRequest.AddHeader("Content-Type", "application/json");
            XmlRequest.AddHeader("Accept", "application/json");
            XmlRequest.AddParameter("application/json", Content, ParameterType.RequestBody);
            XmlRequest.Timeout = _efxssnTimeout;
            
            RestResponse Response = GetRestclient().ExecuteAsync(XmlRequest).GetAwaiter().GetResult();

            if (Response != null && Response.StatusCode == System.Net.HttpStatusCode.OK && Response.Content != null)
            {
                _logger.LogInformation("EFXSSN response received.....for : " + RemotePointInfo);
                EFXSSNResponse = Response.Content;
            }
            else
            {
                if (Response.StatusCode == System.Net.HttpStatusCode.Unauthorized && Response.Content.Contains("Access token is invalid"))
                {
                    XmlRequest = new RestRequest("", RestSharp.Method.Post);
                    XmlRequest.AddHeader("Content-Type", "application/json");
                    XmlRequest.AddParameter("application/json", Content, ParameterType.RequestBody);
                    XmlRequest.Timeout = _efxssnTimeout;
                    Response = GetRestclient().ExecuteAsync(XmlRequest).GetAwaiter().GetResult();
                    if (Response != null && Response.StatusCode == System.Net.HttpStatusCode.OK && Response.Content != null)
                    { 
                        _logger.LogInformation("EFXSSN response received.....for : " + RemotePointInfo);
                        EFXSSNResponse = Response.Content;
                        
                    }
                    else
                    {
                        _logger.LogInformation(RemotePointInfo + " : Error EFXSSN response Status Code: " + Response.StatusCode + " | Error Message: " + Response.ErrorMessage + " | Error Response: " + Response.Content);
                        if (Response.Content != null && Response.Content.Length > 0)
                        {
                            return Response.Content;
                        }
                    }
                }
                else
                {
                    _logger.LogInformation(RemotePointInfo + " : Error EFXSSN response Status Code: " + Response.StatusCode + " | Error Message: " + Response.ErrorMessage + " | Error Response: " + Response.Content);
                    if (Response.Content != null && Response.Content.Length > 0)
                    {
                        return Response.Content;
                    }
                }
            }

            return EFXSSNResponse;
        }
    }
}
