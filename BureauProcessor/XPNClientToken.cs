using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using BureauAdaptor;
using BureauAdaptor.BureauProcessor;
using BureauAdaptor.Helper;
using Newtonsoft.Json.Linq;

namespace BureauProcessor
{
    internal class XPNClientToken : Token
    {
        StringBuilder sbReceived = new StringBuilder();
        internal XPNClientToken(Socket connection, int bufferSize, ILogger<Worker> logger, Settings settings) : base(connection, bufferSize, logger, settings)
        {
            
        }
        internal override bool ProcessData(SocketAsyncEventArgs args)
        {
            // Get the message received from the client.
            String received = this.sb.ToString();
            
            _logger.LogDebug("Received data from {0} : \"{1}\". The server has read {2} bytes.", RemotePointInfo, received, received.Length);

            string BureauResponse = string.Empty;    
            try
            {
                if (!string.IsNullOrEmpty(received))
                {
                    sbReceived.AppendLine(received);
                    if (received.Substring(received.Length - SegmentInfo.SymbolLength) == SegmentInfo.AckSymbol && received.StartsWith("TCP"))
                        BureauResponse = string.Concat(SegmentInfo.AckEmptySpace, SegmentInfo.AckSymbol);
                    else if (!string.IsNullOrEmpty(_settings?.BureauSettings?.FileSystem))
                    {
                        try
                        {
                            if (!received.StartsWith("TCP"))
                            {
                                received = received.Substring(0, received.Length - SegmentInfo.SymbolLength);
                                BureauResponse = new CTBProcessor(_logger, _settings, 1, sbReceived.ToString()).PostRequest(received, RemotePointInfo);
                            }
                        }
                        catch(Exception ex)
                        {
                            _logger.LogError(ex, "Exception while processing XPN request for: " + RemotePointInfo);
                            return false;
                        }
                    }
                    else
                    {
                        received = received.Substring(0, received.Length - SegmentInfo.SymbolLength);
                        BureauResponse = new XPNProcessor(_logger, _settings, false).ExecuteRequest(received, RemotePointInfo);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while posting request to Bureau for: " + RemotePointInfo);
                return false;
            }

            _logger.LogInformation("Received response from XPN for client: {0}", RemotePointInfo);

            if (string.IsNullOrEmpty(BureauResponse))
            {
                _logger.LogInformation("Received empty response from XPN. Closing connection with CPS {0}", RemotePointInfo);
                return false;
            }
            else
                _logger.LogDebug("Received response from XPN for client {0}: {1}", RemotePointInfo, BureauResponse);

            Byte[] sendBuffer = Encoding.ASCII.GetBytes(BureauResponse);
            args.SetBuffer(sendBuffer, 0, sendBuffer.Length);
            sb.Length = 0;
            this.currentIndex = 0;
            return true;
        }

    }
}
