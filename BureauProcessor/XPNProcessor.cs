using BureauAdaptor.Helper;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static BureauProcessor.XPNClientToken;

namespace BureauAdaptor.BureauProcessor
{
    internal class XPNProcessor
    {
        private readonly ILogger<Worker> _logger;
        private readonly Settings _settings;
        private readonly RestClient _client;
        private static readonly string XpnUrl = "https://uat-us-api.experian.com/consumerservices/credit-profile/v2/credit-report";
        private static readonly int XpnTimeout = 3800;
        public static bool _isSSN = false;


        public XPNProcessor(ILogger<Worker> logger, Settings settings, bool isSSN)
        {
            _logger = logger;
            _settings = settings;
            _isSSN = isSSN;
            if (_client == null)
            {
                var options = new RestClientOptions()
                {
                    BaseUrl = new Uri(settings?.BureauSettings?.URL ?? XpnUrl),
                    MaxTimeout = settings?.BureauSettings?.Timeout ?? XpnTimeout
                };
                _client = new RestClient(options)
                {
                    Authenticator = new XPNAuthenticator(_logger, _settings)
                };
            }
        }

        private RestClient GetRestclient()
        {
            return _client;
        }

        public string ExecuteRequest(string Content, string RemotePointInfo)
        {
            string XPNResponse = string.Empty;
            if (Content.Length == 0)
                return XPNResponse;
            List<string>? errorMsgs = _settings?.BureauSettings?.ErrorEmailMessage?.ToString().Split('|').ToList();

            {
                var XmlRequest = new RestRequest("", RestSharp.Method.Post);
                XmlRequest.AddHeader("Content-Type", "application/json");
                XmlRequest.AddParameter("application/json", Content, ParameterType.RequestBody);
                //XmlRequest.AddParameter("Authorization", "Bearer " + access_token, ParameterType.HttpHeader);
                XmlRequest.Timeout = XpnTimeout;
                //_client.Authenticator = new XPNAuthenticator(_logger, _settings, false);
                RestResponse Response = GetRestclient().ExecuteAsync(XmlRequest).GetAwaiter().GetResult();

                if (Response != null && Response.StatusCode == System.Net.HttpStatusCode.OK && Response.Content != null)
                {
                    if (!_isSSN)
                    {
                        _logger.LogInformation("XPN response received.....for : " + RemotePointInfo);
                        var XPNJsonObject = JObject.Parse(Response.Content);
                        var XPNJsonResponse = XPNJsonObject["arf"]["arfResponse"];
                        XPNResponse = string.Concat(XPNJsonResponse, SegmentInfo.AckSymbol);
                    }
                    else
                    {
                        _logger.LogInformation("SSN response received.....for : " + RemotePointInfo);
                        XPNResponse = Response.Content;
                    }
                }
                else
                {
                    if (Response.StatusCode == System.Net.HttpStatusCode.Unauthorized && Response.Content.Contains("Access token is invalid"))
                    {
                        //access_token = tokenAccess.GenerateAccessToken();

                        XmlRequest = new RestRequest("", RestSharp.Method.Post);
                        XmlRequest.AddHeader("Content-Type", "application/json");
                        XmlRequest.AddParameter("application/json", Content, ParameterType.RequestBody);
                        //XmlRequest.AddParameter("Authorization", "Bearer " + access_token, ParameterType.HttpHeader);
                        XmlRequest.Timeout = XpnTimeout;
                        //_client.Authenticator = new XPNAuthenticator(_logger, _settings, true);
                        Response = GetRestclient().ExecuteAsync(XmlRequest).GetAwaiter().GetResult();
                        if (Response != null && Response.StatusCode == System.Net.HttpStatusCode.OK && Response.Content != null)
                        {

                            if (!_isSSN)
                            {
                                _logger.LogInformation("XPN response received.....for : " + RemotePointInfo);
                                var XPNJsonObject = JObject.Parse(Response.Content);
                                var XPNJsonResponse = XPNJsonObject["arf"]["arfResponse"];
                                XPNResponse = string.Concat(XPNJsonResponse, SegmentInfo.AckSymbol);
                            }
                            else
                            {
                                _logger.LogInformation("SSN response received.....for : " + RemotePointInfo);
                                XPNResponse = Response.Content;
                            }
                        }
                        else
                        {
                            string XPNTransId = ""; // (string)(Response.Headers.Count > 0 ? Response.Headers[2].Value : "");
                            _logger.LogInformation(RemotePointInfo + " : Error XPN response Status Code: " + Response.StatusCode + " | Error Message: " + Response.ErrorMessage + " | Error Response: " + Response.Content + " | Experian Tansaction Id: " + XPNTransId);
                            if (Response.Content != null && Response.Content.Length > 0)
                            {
                                if (!_isSSN)
                                {
                                    string _XPNJsonResponse = Regex.Replace(Response.Content.ToString(), @"\s+(?=(?:(?:[^""]*""){2})*[^""]*$)", "");
                                    if (errorMsgs != null && errorMsgs.Count > 0 && errorMsgs.Where(item => Response.Content.ToString().Contains(item.Trim())).Count() > 0)
                                        _logger.LogInformation(_XPNJsonResponse.ToString());
                                    else
                                        _logger.LogError(_XPNJsonResponse.ToString());
                                    return string.Concat(_XPNJsonResponse, SegmentInfo.AckSymbol);
                                }
                                else
                                    return Response.Content;
                            }
                        }
                    }
                    else
                    {
                        string XPNTransId = ""; // (string)(Response.Headers.Count > 0 ? Response.Headers[2].Value : "");
                        _logger.LogInformation(RemotePointInfo + " : Error XPN response Status Code: " + Response.StatusCode + " | Error Message: " + Response.ErrorMessage + " | Error Response: " + Response.Content + " | Experian Tansaction Id: " + XPNTransId);
                        if (Response.Content != null && Response.Content.Length > 0)
                        {
                            if (!_isSSN)
                            {
                                string _XPNJsonResponse = Regex.Replace(Response.Content.ToString(), @"\s+(?=(?:(?:[^""]*""){2})*[^""]*$)", "");
                                if (errorMsgs != null && errorMsgs.Count > 0 && errorMsgs.Where(item => Response.Content.ToString().Contains(item.Trim())).Count() > 0)
                                    _logger.LogInformation(_XPNJsonResponse.ToString());
                                else
                                    _logger.LogError(_XPNJsonResponse.ToString());
                                return string.Concat(_XPNJsonResponse, SegmentInfo.AckSymbol);
                            }
                            else
                                return Response.Content;
                        }
                    }
                }
            }
            return XPNResponse;
        }
    }
}
