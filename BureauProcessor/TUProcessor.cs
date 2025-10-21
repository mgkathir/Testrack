using BureauAdaptor;
using BureauAdaptor.Helper;
using RestSharp;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace BureauProcessor
{
    internal class TUProcessor
    {
        private readonly ILogger<Worker> _logger;
        private readonly Settings _settings;
        private readonly RestClient _client;
        private static readonly string TuUrl = "https://netaccess-test.transunion.com/";
        private static readonly string TuSystemId = "INFORMA7";
        private static readonly string TuPassword = "K0ermvid9igVOLqjBk4Z";
        private static readonly string TuProcessingEnvironment = "standardTest";
        private static readonly int TuTimeout = 10000;
        private static string CertificatePath = "C:\\ClientCertificates\\INFORMA7_SHA2.p12";

        //constants
        private const int RequestLength = 5;

        private const int HeaderStartLength = 5;

        private const int HeaderEndLength = 12;

        private const int HeaderTotalLegth = 17;

        #region "TU Error Code 602 variables"

        private const int HeaderSegmentLengthStart = 25;

        private const int HeaderSegmentLength = 38;

        private const int SegmentServiceCodeStart = 4;

        private const int SegmentServiceCodeLength = 5;

        private const string HeaderSegment = "TU4E062";

        private const string FfrRecordVersion = "0";

        private const string CountryCod = "1";

        private const string LanguageIndicator = "1";

        private const string ServiceHeadersegment = "PH01";

        private const string ServiceSegmentLength = "012";

        private const string ErrcServiceHeadersegment = "ERRC";

        private const string ErrcSegmentLength = "011";

        private const string ErrcSegmentSubjectIdentifier = "1";

        private const string EndsServiceHeaderSegment = "ENDS";

        private const string EndsSegmentLength = "010";

        private const string TotalNumberofSegments = "004";

        private readonly Dictionary<string, string> errorMap = new Dictionary<string, string>() {
            {"40","The digital certificate you are using is invalid or missing when accessing TUNA service"},
            {"50","The system ID that you are using is invalid/disabled."},
            {"852","The system ID that you are using does not exist in our system."},
            {"952","The system ID that you are using does not exist in our system."},
            {"851","The system ID password that you are using isn’t correct."},
            {"951","The system ID password that you are using isn’t correct."},
            {"853","The system ID and digital certificate do not match."},
            {"953","The system ID and digital certificate do not match."},
            {"54","You have typed in the wrong system ID password more than three times."},
            {"61","The application was not able to process the transactions at this moment due to an error. Routing error to reach application endpoint due to network, application or any other issues."},
            {"70","The format of the transaction is incorrect.This message is returned from TransUnion when a connection is made but no data is received from your application."},
            {"201","The IP address through which you are sending your transaction is invalid for your TransUnion account."},
            {"601","The system ID and subscriber code linking is not enabled in Transunion system. The threshold is breached.There is an issue with the subscriber code and system ID linking."},
            {"605","The system ID and subscriber code linking is not enabled in Transunion system. The threshold is breached.There is an issue with the subscriber code and system ID linking."},
            {"602","There is an issue with the subscriber code and system ID linking. The threshold is breached."},
            {"606","There is an issue with the subscriber code and system ID linking. The threshold is breached."},
            {"607","There is an issue with XML wrapper not being sent or incorrect. XML wrapper is enabled but wrapper is not being sent. The threshold is breached."},
            {"608","There is an issue with XML wrapper not being sent or incorrect. XML wrapper is enabled and sent but content of XML is incorrect. The threshold is breached."},
            {"501","Subscriber code blacklisted."},
            {"502","Subscriber code blacklisted."},
            {"503","Subscriber code blacklisted."},
            {"504","Subscriber code blacklisted."},
            {"505","Subscriber code blacklisted."},
            {"500","Internal Server Error."}
        };

        #endregion "TU Error Code 602 variables"

        public TUProcessor(ILogger<Worker> logger, Settings settings)
        {
            _logger = logger;
            _settings = settings;
            if (_client == null)
            {
                X509Certificate2 Certificate = new X509Certificate2(settings?.BureauSettings?.CertificatePath ?? CertificatePath, settings?.BureauSettings?.Password ?? TuPassword);
                var options = new RestClientOptions()
                {
                    BaseUrl = new Uri(settings?.BureauSettings?.URL ?? TuUrl),
                    MaxTimeout = settings?.BureauSettings?.Timeout ?? TuTimeout,
                    RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true,
                    ClientCertificates = new X509CertificateCollection() { Certificate },
                    ThrowOnAnyError = true

                };
                _client = new RestClient(options);
            }
        }

        private RestClient GetRestclient()
        {
            return _client;
        }

        public string PostRequest(string Content, string RemotePointInfo)
        {
            string TuResponse = string.Empty;

            if (Content.Length < (HeaderStartLength + HeaderEndLength))
                return TuResponse;

            string RequestHeader = Content.Substring(HeaderStartLength, HeaderEndLength);
            xmlrequest ObjXmlCreate = new xmlrequest()
            {
                systemId = _settings?.BureauSettings?.SystemId ?? TuSystemId,
                systemPassword = _settings?.BureauSettings?.Password ?? TuPassword,
                processingEnvironment = _settings?.BureauSettings?.ProcessingEnvironment ?? TuProcessingEnvironment,
                productrequest = Content.Substring(HeaderTotalLegth)
            };
            string XmlRequestData = CreateXMLFromObject(ObjXmlCreate);

            var XmlRequest = new RestRequest("", RestSharp.Method.Post);
            XmlRequest.AddHeader("Content-Type", "text/xml");
            XmlRequest.AddParameter("text/xml", XmlRequestData, ParameterType.RequestBody);

            //RestResponse Response = GetTUAclient().Execute(XmlRequest);
            RestResponse Response = GetRestclient().PostAsync(XmlRequest).GetAwaiter().GetResult();
            
            if (Response != null && Response.StatusCode == System.Net.HttpStatusCode.OK && Response.Content != null)
            {
                _logger.LogInformation("TU response received.....for : "+ RemotePointInfo);
                int ResLength = Convert.ToInt32(Response.Content.Length) + HeaderTotalLegth;
                string ResponseLength = LengthConvertToHexadecimal(ResLength);
                TuResponse = ResponseLength.PadLeft(RequestLength, '0') + RequestHeader + Response.Content;
            }
            else if (Response?.Content != null)
            {
                _logger.LogInformation("TU Error Response for client: "+ RemotePointInfo +" : " + Response?.Content);
                string TUErrorResponse = Response.Content;
                if (Response.Content.Length > 0 && IsValidXML(TUErrorResponse))
                {
                    XmlDocument xmltest = new XmlDocument();
                    xmltest.LoadXml(TUErrorResponse);
                    XmlNodeList elemlist = xmltest.GetElementsByTagName("errorcode");

                    if (elemlist.Count > 0 && elemlist[0].InnerXml.Replace("\n", "") == "602")
                    {
                        string ErrorCode = elemlist[0].InnerXml.Replace("\n", "");
                        string UserReferenceNumber = Content.Substring(HeaderSegmentLengthStart, HeaderSegmentLength);
                        string TransactionDate = DateTime.Now.ToString("yyyyMMdd");
                        string TransactionTime = DateTime.Now.ToString("HHmmss");
                        string SegmentServiceCode = Content.Substring(Content.IndexOf("RP01") + SegmentServiceCodeStart, SegmentServiceCodeLength);

                        string TuErrorResponse = HeaderSegment + FfrRecordVersion + CountryCod +
                        LanguageIndicator + UserReferenceNumber +
                        TransactionDate + TransactionTime + ServiceHeadersegment + ServiceSegmentLength +
                        SegmentServiceCode + ErrcServiceHeadersegment + ErrcSegmentLength + ErrcSegmentSubjectIdentifier +
                        ErrorCode + EndsServiceHeaderSegment + EndsSegmentLength + TotalNumberofSegments;

                        int ErrorResLength = Convert.ToInt32(TuErrorResponse.Length) + HeaderTotalLegth;
                        string ErrorResponseLength = LengthConvertToHexadecimal(ErrorResLength);
                        TuResponse = ErrorResponseLength.PadLeft(RequestLength, '0') + RequestHeader + TuErrorResponse;
                    }
                    if (elemlist.Count > 0)
                    {
                        string ErrorCode = elemlist[0].InnerXml.Replace("\n", "");
                        LogTUNAServiceError(ErrorCode);
                    }
                }
                else
                    _logger.LogDebug("TU Error response: " + TUErrorResponse);
            }
            return TuResponse;
        }

        public string ExecuteRequest(string Content, string RemotePointInfo)
        {
            string TuResponse = string.Empty;

            if (Content.Length < (HeaderStartLength + HeaderEndLength))
                return TuResponse;

            string RequestHeader = Content.Substring(HeaderStartLength, HeaderEndLength);
            xmlrequest ObjXmlCreate = new xmlrequest()
            {
                systemId = _settings?.BureauSettings?.SystemId ?? TuSystemId,
                systemPassword = _settings?.BureauSettings?.Password ?? TuPassword,
                processingEnvironment = _settings?.BureauSettings?.ProcessingEnvironment ?? TuProcessingEnvironment,
                productrequest = Content.Substring(HeaderTotalLegth)
            };
            string XmlRequestData = CreateXMLFromObject(ObjXmlCreate);

            var XmlRequest = new RestRequest("", RestSharp.Method.Post);
            XmlRequest.AddHeader("Content-Type", "text/xml");
            XmlRequest.AddParameter("text/xml", XmlRequestData, ParameterType.RequestBody);
            XmlRequest.Timeout = _settings?.BureauSettings?.Timeout ?? TuTimeout;
            
            RestResponse Response = GetRestclient().ExecuteAsync(XmlRequest).GetAwaiter().GetResult();

            if (Response != null && Response.StatusCode == System.Net.HttpStatusCode.OK && Response.Content != null)
            {
                _logger.LogInformation("TU response received.....for : "+ RemotePointInfo);
                int ResLength = Convert.ToInt32(Response.Content.Length) + HeaderTotalLegth;
                string ResponseLength = LengthConvertToHexadecimal(ResLength);
                TuResponse = ResponseLength.PadLeft(RequestLength, '0') + RequestHeader + Response.Content;
            }
            else if (Response?.Content != null)
            {
                _logger.LogInformation("TU Error Response for client: "+ RemotePointInfo +" : " + Response?.Content);
                string TUErrorResponse = Response.Content;
                if (Response.Content.Length > 0 && IsValidXML(TUErrorResponse))
                {
                    XmlDocument xmltest = new XmlDocument();
                    xmltest.LoadXml(TUErrorResponse);
                    XmlNodeList elemlist = xmltest.GetElementsByTagName("errorcode");

                    if (elemlist.Count > 0 && elemlist[0].InnerXml.Replace("\n", "") == "602")
                    {
                        string ErrorCode = elemlist[0].InnerXml.Replace("\n", "");
                        string UserReferenceNumber = Content.Substring(HeaderSegmentLengthStart, HeaderSegmentLength);
                        string TransactionDate = DateTime.Now.ToString("yyyyMMdd");
                        string TransactionTime = DateTime.Now.ToString("HHmmss");
                        string SegmentServiceCode = Content.Substring(Content.IndexOf("RP01") + SegmentServiceCodeStart, SegmentServiceCodeLength);

                        string TuErrorResponse = HeaderSegment + FfrRecordVersion + CountryCod +
                        LanguageIndicator + UserReferenceNumber +
                        TransactionDate + TransactionTime + ServiceHeadersegment + ServiceSegmentLength +
                        SegmentServiceCode + ErrcServiceHeadersegment + ErrcSegmentLength + ErrcSegmentSubjectIdentifier +
                        ErrorCode + EndsServiceHeaderSegment + EndsSegmentLength + TotalNumberofSegments;

                        int ErrorResLength = Convert.ToInt32(TuErrorResponse.Length) + HeaderTotalLegth;
                        string ErrorResponseLength = LengthConvertToHexadecimal(ErrorResLength);
                        TuResponse = ErrorResponseLength.PadLeft(RequestLength, '0') + RequestHeader + TuErrorResponse;
                    }
                    if (elemlist.Count > 0)
                    {
                        string ErrorCode = elemlist[0].InnerXml.Replace("\n", "");
                        LogTUNAServiceError(ErrorCode);
                    }
                }
                else
                    _logger.LogDebug("TU Error response: " + TUErrorResponse);
            }
            return TuResponse;
        }

        public void LogTUNAServiceError(string ErrorCode)
        {
            _logger.LogError("TU Service Error: {0} - {1}", ErrorCode, errorMap[ErrorCode]);
        }

        public static string LengthConvertToHexadecimal(int ResponseLenght)
        {
            if (ResponseLenght < 1) return "0";

            int HexaDecimal = ResponseLenght;
            string HexaString = string.Empty;

            while (ResponseLenght > 0)
            {
                HexaDecimal = ResponseLenght % 16;

                if (HexaDecimal < 10)
                    HexaString = HexaString.Insert(0, Convert.ToChar(HexaDecimal + 48).ToString());
                else
                    HexaString = HexaString.Insert(0, Convert.ToChar(HexaDecimal + 55).ToString());

                ResponseLenght /= 16;
            }

            return HexaString;
        }

        public static bool IsValidXML(string XmlStr)
        {
            try
            {
                if (!string.IsNullOrEmpty(XmlStr))
                {
                    System.Xml.XmlDocument xmlDoc = new System.Xml.XmlDocument();
                    xmlDoc.LoadXml(XmlStr);
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (System.Xml.XmlException)
            {
                return false;
            }
        }

        public string CreateXMLFromObject(object ObjRequest)
        {
            StringWriter ObjStringWriter = new Utf8StringWriter();
            XmlTextWriter ObjTextWriter = null;
            try
            {
                XmlSerializerNamespaces XmlNameSpace = new XmlSerializerNamespaces();
                XmlSerializer Serializer = new XmlSerializer(ObjRequest.GetType());
                XmlTypeMapping XmlTypeMapping = new SoapReflectionImporter().ImportTypeMapping(ObjRequest.GetType());
                ObjTextWriter = new XmlTextWriter(ObjStringWriter);
                Serializer.Serialize(ObjTextWriter, ObjRequest, XmlNameSpace);
            }
            catch (Exception ex)
            {
                throw (ex);
            }
            finally
            {
                ObjStringWriter.Close();
                if (ObjTextWriter != null)
                {
                    ObjTextWriter.Close();
                }
            }
            return ObjStringWriter.ToString();
        }
    }

    public class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding
        { get { return Encoding.UTF8; } }
    }
}