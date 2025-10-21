using BureauAdaptor;
using BureauAdaptor.BureauProcessor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace BureauProcessor
{
    internal class EFXClientToken : Token
    {
        internal EFXClientToken(Socket connection, int bufferSize, ILogger<Worker> logger, Settings settings) : base(connection, bufferSize, logger, settings)
        {

        }

        internal override bool ProcessData(SocketAsyncEventArgs args)
        {
            // Get the message received from the client.
            String received = this.sb.ToString();

            ////check if the adaptor is EFX, close the connection
            //if (args.BytesTransferred == 0)
            //{
            //    _logger.LogInformation("No Data received. Closing connection with CPS {0}", RemotePointInfo);
            //    return false;
            //}

            //TODO Use message received to perform a specific operation.
            _logger.LogDebug("Received data from {0} : \"{1}\". The server has read {2} bytes.", RemotePointInfo, received, received.Length);

            string BureauResponse = string.Empty;

            try
            {
                if (!string.IsNullOrEmpty(_settings?.BureauSettings?.FileSystem) && !(string.IsNullOrEmpty(received)))
                {
                    try
                    {
                        BureauResponse = new CTBProcessor(_logger, _settings, 4).PostRequest(received, RemotePointInfo);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Exception while processing EFX request for: " + RemotePointInfo);
                        return false;
                    }
                }
                else
                {
                    if (_settings?.BureauSettings?.RequestMethodType?.ToUpper() == "POST")
                        BureauResponse = new EFXProcessor(_logger, _settings).PostRequest(received, RemotePointInfo);
                    else
                        BureauResponse = new EFXProcessor(_logger, _settings).ExecuteRequest(received, RemotePointInfo);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while posting request to Bureau");
                return false;
            }

            _logger.LogInformation("Received response from EFX for client: {0}", RemotePointInfo);

            if (string.IsNullOrEmpty(BureauResponse))
            {
                _logger.LogInformation("Received empty response from EFX. Closing connection with CPS {0}", RemotePointInfo);
                return false;
            }
            //else
            //    _logger.LogDebug("Received response from EFX for client {0}: {1}", RemotePointInfo, BureauResponse);

            Byte[] sendBuffer = Encoding.ASCII.GetBytes(BureauResponse);
            args.SetBuffer(sendBuffer, 0, sendBuffer.Length);
            // Clear StringBuffer, so it can receive more data from a keep-alive connection client.
            sb.Length = 0;
            this.currentIndex = 0;
            return true;
        }
    }
}
