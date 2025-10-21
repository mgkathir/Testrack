using BureauAdaptor.Helper;
using BureauProcessor;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BureauAdaptor.BureauProcessor
{
    internal class CTBProcessor
    {
        private readonly ILogger<Worker> _logger;
        private readonly Settings _settings;
        public int _Bureau = 0;
        public string _Received = string.Empty;
        StringBuilder sbReceived = new StringBuilder();
        StringBuilder sbLogMsg = new StringBuilder();
        public CTBProcessor(ILogger<Worker> logger, Settings settings, int Bureau, string received = "")
        {
            _logger = logger;
            _settings = settings;
            _Bureau = Bureau;
            _Received = received;
        }

        public string PostRequest(string received, string remotePointInfo)
        {
            string BureauResponse = string.Empty;
            sbReceived = new StringBuilder();
            sbLogMsg = new StringBuilder();
            _logger.LogInformation("CTB request received for Bureau :" + _Bureau + " from " + remotePointInfo);
            if (_Bureau == 1)   // XPN
            {
                sbReceived.AppendLine(_Received);
                var borrowerSSN = "";
                var coBorrowerSSN = "";

                JObject objjson = JObject.Parse(received);
                borrowerSSN = objjson.SelectToken("consumerPii.primaryApplicant.ssn.ssn")?.Value<string>();
                coBorrowerSSN = objjson.SelectToken("consumerPii.secondaryApplicant.ssn.ssn")?.Value<string>();

                borrowerSSN = !string.IsNullOrEmpty(borrowerSSN) ? borrowerSSN : "000000000";
                coBorrowerSSN = !string.IsNullOrEmpty(coBorrowerSSN) ? coBorrowerSSN : "000000000";

                string CTBPath = _settings?.BureauSettings?.FileSystem + "RSP\\" + borrowerSSN + coBorrowerSSN + ".rsp";
                if (File.Exists(CTBPath))
                {
                    _logger.LogInformation("File Exists for the Path :" + CTBPath);
                    byte[] data = File.ReadAllBytes(CTBPath);
                    BureauResponse = System.Text.Encoding.UTF8.GetString(data);
                    BureauResponse = string.Concat(BureauResponse, SegmentInfo.AckSymbol);
                }
                LogMessage(remotePointInfo, BureauResponse.Length, borrowerSSN, coBorrowerSSN);
            }
            else if (_Bureau == 2)  // TU
            {
                sbReceived.AppendLine(received);
                var borrowerSSN = "";
                var coBorrowerSSN = "";
                /*
                borrowerSSN = received.ToString().Substring(211, 9);
                coBorrowerSSN = received.Length > 375 ? received.ToString().Substring(404, 9) : "000000000";
                */
                var segments = Regex.Split(received, @"(PI01[\d\s]{9}.{12})").Where(s => s != String.Empty && s.StartsWith("PI01"));

                if (segments.Any())
                    borrowerSSN = segments.ElementAt(0).Substring(4, 9);
                coBorrowerSSN = segments.Count() > 1 ? segments.ElementAt(1).Substring(4, 9) : "000000000";


                string CTBPath = _settings?.BureauSettings?.FileSystem + "RSP\\" + borrowerSSN + coBorrowerSSN + ".rsp";

                string RequestHeader = received.Substring(5, 12);
                if (File.Exists(CTBPath))
                {
                    _logger.LogInformation("File Exists for the Path :" + CTBPath);
                    byte[] data = File.ReadAllBytes(CTBPath);
                    string Response = System.Text.Encoding.UTF8.GetString(data);
                    int ResLength = Convert.ToInt32(Response.Length) + 17;
                    string ResponseLength = TUProcessor.LengthConvertToHexadecimal(ResLength);
                    BureauResponse = ResponseLength.PadLeft(5, '0') + RequestHeader + Response;
                }
                LogMessage(remotePointInfo, BureauResponse.Length, borrowerSSN, coBorrowerSSN);
            }
            else if (_Bureau == 4)  // EFX
            {
                sbReceived.AppendLine(received);
                var borrowerSSN = "";
                var coBorrowerSSN = "";
                borrowerSSN = received.ToString().Substring(89, 9);
                coBorrowerSSN = received.ToString().Substring(163, 1).Trim() != String.Empty ? received.ToString().Substring(163, 9) : "000000000";
                string CTBPath = _settings?.BureauSettings?.FileSystem + "RSP\\" + borrowerSSN + coBorrowerSSN + ".rsp";
                if (File.Exists(CTBPath))
                {
                    _logger.LogInformation("File Exists for the Path :" + CTBPath);
                    byte[] data = File.ReadAllBytes(CTBPath);
                    BureauResponse = System.Text.Encoding.UTF8.GetString(data);
                    BureauResponse = BureauResponse.Length.ToString("000000") + BureauResponse;
                }
                LogMessage(remotePointInfo, BureauResponse.Length, borrowerSSN, coBorrowerSSN);
            }
            return BureauResponse;
        }

        public void LogMessage(string remotePointInfo, int resLength, string borrowerSSN, string coBorrowerSSN)
        {
            ulong filename = (ulong)DateTime.Now.Ticks;
            string REQPath = _settings?.BureauSettings?.FileSystem + "REQ\\" + filename + ".req";

            File.AppendAllText(REQPath, sbReceived.ToString());

            sbLogMsg.AppendLine("TCP Connection opened");
            sbLogMsg.AppendLine("Request File Name: " + _settings?.BureauSettings?.FileSystem + "\\REQ\\" + filename + ".req");
            sbLogMsg.AppendLine("IR Client [IP Address][Port No]: " + remotePointInfo);
            sbLogMsg.AppendLine("REQ - Length: " + sbReceived.Length);

            if (resLength > 0)
            {
                sbLogMsg.AppendLine("Response File Name: " + _settings?.BureauSettings?.FileSystem + "\\RSP\\" + borrowerSSN + coBorrowerSSN + ".rsp");
                sbLogMsg.AppendLine("RSP - Length: " + resLength);
            }
            sbLogMsg.AppendLine("TCP Connection closed");

            string LOGPath = _settings?.BureauSettings?.FileSystem + "LOG\\" + filename + ".txt";
            File.AppendAllText(LOGPath, sbLogMsg.ToString());
            sbLogMsg.Clear();
            sbReceived.Clear();
        }
    }
}
