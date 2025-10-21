using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using BureauAdaptor;
using BureauAdaptor.BureauProcessor;

namespace BureauProcessor
{
    internal class TUClientToken : Token
    {
        internal TUClientToken(Socket connection, int bufferSize, ILogger<Worker> logger, Settings settings) : base(connection, bufferSize, logger, settings)
        {
            
        }
        internal override bool ProcessData(SocketAsyncEventArgs args)
        {
            // Get the message received from the client.
            String received = this.sb.ToString();

            if (received.Length > 12 && received.Substring(9, 2) == "27")
            {
                _logger.LogInformation("Received closed notification from  \"{0}\" : \"{1}\". The server has read {2} bytes.", RemotePointInfo, received, received.Length);
                return false;
            }

            //TODO Use message received to perform a specific operation.
            _logger.LogDebug("Received data from {0} : \"{1}\". The server has read {2} bytes.", RemotePointInfo, received, received.Length);

            string BureauResponse = string.Empty;

            try
            {
                if (!string.IsNullOrEmpty(_settings?.BureauSettings?.FileSystem) && !(string.IsNullOrEmpty(received)))
                {
                    try
                    {
                        BureauResponse = new CTBProcessor(_logger, _settings, 2).PostRequest(received, RemotePointInfo);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Exception while processing TU request for: " + RemotePointInfo);
                        return false;
                    }
                }
                else
                {
                    if (_settings?.BureauSettings?.RequestMethodType?.ToUpper() == "POST")
                        BureauResponse = new TUProcessor(_logger, _settings).PostRequest(received, RemotePointInfo);
                    else
                        BureauResponse = new TUProcessor(_logger, _settings).ExecuteRequest(received, RemotePointInfo);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while posting request to Bureau for: " + RemotePointInfo);
                return false;
            }

            _logger.LogInformation("Received response from TU for client: {0}" , RemotePointInfo);

            if (string.IsNullOrEmpty(BureauResponse))
            {
                _logger.LogInformation("Received empty response from TU. Closing connection with CPS {0}", RemotePointInfo);
                return false;
            }
            else
                _logger.LogDebug("Received response from TU for client {0}: {1}", RemotePointInfo, BureauResponse);

            Byte[] sendBuffer = Encoding.ASCII.GetBytes(BureauResponse);
            args.SetBuffer(sendBuffer, 0, sendBuffer.Length);
            // Clear StringBuffer, so it can receive more data from a keep-alive connection client.
            sb.Length = 0;
            this.currentIndex = 0;
            return true;
        }
    }
}
