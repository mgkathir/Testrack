using BureauAdaptor.Helper;
using Newtonsoft.Json.Linq;
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
    public class EFXSSNAuthenticator: AuthenticatorBase
    {
        private readonly ILogger<Worker> _logger;
        private readonly Settings _settings;
        private readonly RestClient _client;
        private static string _ClientID = "SG1u6I9kqynP7sk4rCt5gUsH0TqJDleh";
        private static string _ClientSecretCode = "V5fN47cjXDaLxggr";
        private static string _scope = "https://api.equifax.com/business/dtec/v1";
        private static string _EFXSSNTokenUrl = "https://api.uat.equifax.com/v1/oauth/token";
        public static XPNToken objToken = new XPNToken();
        public EFXSSNAuthenticator(ILogger<Worker> logger, Settings settings) : base(objToken.access_token)
        {
            this._logger = logger;
            this._settings = settings;
            if (_client == null)
            {
                var options = new RestClientOptions()
                {
                    BaseUrl = new Uri(settings?.BureauSettings?.TokenURL ?? _EFXSSNTokenUrl),
                };
                _client = new RestClient(options)
                {
                    Authenticator = new HttpBasicAuthenticator(_ClientID, _ClientSecretCode)
                };
            }
        }

        protected override async ValueTask<Parameter> GetAuthenticationParameter(string accessToken)
        {
            Token = string.IsNullOrEmpty(Token) || DateTime.Now > objToken.accessToken_Expiry ? await GenerateAccessToken() : objToken.access_token;
            return new HeaderParameter(KnownHeaders.Authorization, "Bearer " + Token);
        }

        private RestClient GetRestclient()
        {
            return _client;
        }

        async Task<string> GenerateAccessToken(string RemotePointInfo = null)
        {
            try
            {
                _logger.LogInformation("Begin GetAccessTokenOnExpiry");
                _ClientID = _settings?.BureauSettings?.ClientID ?? _ClientID;
                _ClientSecretCode = _settings?.BureauSettings?.ClientSecretCode ?? _ClientSecretCode;
                _scope = _settings?.BureauSettings?.Scope ?? _scope;
                if (string.IsNullOrEmpty(objToken.access_token) || DateTime.Now > objToken.accessToken_Expiry)
                {
                    _logger.LogInformation("Requesting for new token");
                    var request = new RestRequest("", RestSharp.Method.Post);
                    _client.Authenticator = new HttpBasicAuthenticator(_ClientID, _ClientSecretCode);

                    request.AddParameter("client_id", _ClientID);
                    request.AddParameter("client_secret", _ClientSecretCode);
                    request.AddParameter("scope", _scope);
                    request.AddParameter("grant_type", "client_credentials");

                    RestResponse Jsonresponse = GetRestclient().ExecuteAsync<XPNToken>(request).GetAwaiter().GetResult();
                    if (Jsonresponse.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        var jsontoken = JsonConvert.DeserializeObject<JToken>(Jsonresponse.Content);
                        JObject jobj = JObject.Parse(jsontoken.ToString());
                        objToken.access_token = (string)jobj["access_token"];
                        objToken.expires_in = (int)jobj["expires_in_secs"];
                        objToken.accessToken_Expiry = DateTime.Now.AddSeconds(objToken.expires_in - 10);
                        _logger.LogInformation("AccessToken : " + objToken.access_token + " Expires at " + objToken.accessToken_Expiry);

                        //send email                        
                        string strEmailMsg = string.Empty;
                        strEmailMsg = Environment.NewLine + "New Access Token is generated.";
                        strEmailMsg += Environment.NewLine + "Access Token : " + objToken.access_token;
                        _logger.LogError(strEmailMsg);
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
