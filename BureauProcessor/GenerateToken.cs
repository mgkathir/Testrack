using BureauAdaptor.Helper;
using BureauProcessor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using RestSharp.Authenticators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using static BureauProcessor.XPNClientToken;

namespace BureauAdaptor.BureauProcessor
{
    internal class GenerateToken
    {
        private readonly ILogger<Worker> _logger;
        private readonly Settings _settings;
        private readonly RestClient _client;
        private static string ClientID = "AzK3lpYSjRr6mHThGQIuPXLztoswH9QH";
        private static string ClientSecretCode = "niby7C1JyYfafFGE";
        private static string XpnUserName = "uatkashs@informativeresearch.com";
        private static string XpnPassWord = "Informative2023!";
        private static string XpnTokenUrl = "https://uat-us-api.experian.com/oauth2/v1/token";
        public static XPNToken objToken = new XPNToken();

        internal GenerateToken(ILogger<Worker> logger, Settings settings)
        {
            this._logger = logger;
            this._settings = settings;
            if (_client == null)
            {
                var options = new RestClientOptions()
                {
                    BaseUrl = new Uri(settings?.BureauSettings?.TokenURL ?? XpnTokenUrl),
                    ThrowOnAnyError = true
                };
                _client = new RestClient(options);
            }
        }
        private RestClient GetRestclient()
        {
            return _client;
        }
        public string GetAccessToken()
        {
            _logger.LogInformation("Begin GetAccessToken - Returning the Access Token");
            return objToken.access_token;
        }
        public string GenerateAccessToken(string RemotePointInfo = null)
        {
            try
            {
                _logger.LogInformation("Begin GetAccessTokenOnExpiry");
                XpnUserName = _settings?.BureauSettings?.UserName ?? XpnUserName;
                XpnPassWord = _settings?.BureauSettings?.Password ?? XpnPassWord;
                ClientID = _settings?.BureauSettings?.ClientID ?? ClientID;
                ClientSecretCode = _settings?.BureauSettings?.ClientSecretCode ?? ClientSecretCode;
                if (string.IsNullOrEmpty(objToken.access_token) || DateTime.Now > objToken.refreshToken_Expiry)
                {
                    _logger.LogInformation("Requesting for new token using Username : " + XpnUserName + " and Password : " + XpnPassWord);
                    var request = new RestRequest("", RestSharp.Method.Post);
                    _client.Authenticator = new HttpBasicAuthenticator(ClientID, ClientSecretCode);
                    request.AddParameter("grant_type", "password");
                    request.AddParameter("username", XpnUserName);
                    request.AddParameter("password", XpnPassWord);

                    RestResponse Jsonresponse = GetRestclient().Execute(request);
                    if (Jsonresponse.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        var jsontoken = JsonConvert.DeserializeObject<JToken>(Jsonresponse.Content);
                        JObject jobj = JObject.Parse(jsontoken.ToString());
                        objToken.access_token = (string)jobj["access_token"];
                        objToken.refresh_token = (string)jobj["refresh_token"];
                        objToken.issued_at = ConvertToDate(jobj["issued_at"].ToString());
                        objToken.expires_in = (int)jobj["expires_in"];
                        objToken.accessToken_Expiry = objToken.issued_at.AddSeconds(objToken.expires_in - 10);
                        objToken.refreshToken_Expiry = objToken.issued_at.AddMinutes(1440 - 2);
                        _logger.LogInformation("AccessToken : " + objToken.access_token + " Expires at " + objToken.accessToken_Expiry);
                        _logger.LogInformation("RefreshToken : " + objToken.refresh_token + " Expires at " + objToken.refreshToken_Expiry);

                        //send email                        
                        string strEmailMsg = string.Empty;
                        strEmailMsg = Environment.NewLine + "New Access Token is generated or Refresh Token has expired.";
                        strEmailMsg += Environment.NewLine + "New Token has been generated using the Username and Password.";
                        strEmailMsg += Environment.NewLine + "Access Token : " + objToken.access_token;
                        strEmailMsg += Environment.NewLine + "Refresh Token : " + objToken.refresh_token;
                        _logger.LogError(strEmailMsg);
                        return objToken.access_token;
                    }
                    else
                    {
                        _logger.LogInformation(RemotePointInfo + " : Error on generate token Status Code: " + Jsonresponse.StatusCode + " | Error Message: " + Jsonresponse.ErrorMessage + " | Error Response: " + Jsonresponse.Content);
                    }
                }
                else
                {
                    _logger.LogInformation("Access Token Expired. Requesting for new token using Refresh Token : " + objToken.refresh_token);
                    var request = new RestRequest("", RestSharp.Method.Post);
                    _client.Authenticator = new HttpBasicAuthenticator(ClientID, ClientSecretCode);
                    request.AddParameter("grant_type", "refresh_token");
                    request.AddParameter("refresh_token", objToken.refresh_token);

                    RestResponse Jsonresponse = GetRestclient().Execute(request);
                    if (Jsonresponse.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        var jsontoken = JsonConvert.DeserializeObject<JToken>(Jsonresponse.Content);
                        JObject jobj = JObject.Parse(jsontoken.ToString());
                        objToken.access_token = (string)jobj["access_token"];
                        objToken.refresh_token = (string)jobj["refresh_token"];
                        objToken.issued_at = ConvertToDate(jobj["issued_at"].ToString());
                        objToken.expires_in = (int)jobj["expires_in"];
                        objToken.accessToken_Expiry = objToken.issued_at.AddSeconds(objToken.expires_in - 10);
                        _logger.LogInformation("AccessToken : " + objToken.access_token + " Expires at " + objToken.accessToken_Expiry);
                        return objToken.access_token;
                    }
                    else
                    {
                        _logger.LogInformation(RemotePointInfo + " : Error on generate token Status Code: " + Jsonresponse.StatusCode + " | Error Message: " + Jsonresponse.ErrorMessage + " | Error Response: " + Jsonresponse.Content);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogInformation("Exception in GetAccessToken : " + ex);
                throw ex;
            }
            return String.Empty;
        }

        #region Convert string to Date
        private DateTime ConvertToDate(string stDate)
        {
            DateTime dtResult = DateTime.Now;
            var result = Convert.ToDateTime("1900-01-01").ToString("MM/dd/yyyy");
            if (stDate != "" && stDate.Length >= 14 && Convert.ToInt16(stDate.Substring(0, 4)) != 0)
            {
                var dtConverted = new DateTime(Convert.ToInt16(stDate.Substring(0, 4)), Convert.ToInt16(stDate.Substring(4, 2)), Convert.ToInt16(stDate.Substring(6, 2)), Convert.ToInt16(stDate.Substring(8, 2)), Convert.ToInt16(stDate.Substring(10, 2)), Convert.ToInt16(stDate.Substring(12, 2)), DateTimeKind.Local);
                dtResult = dtConverted;
            }
            return dtResult;
        }

        #endregion
    }
}
