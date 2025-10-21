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
    internal class EFXSSNClientToken : Token
    {
        internal EFXSSNClientToken(Socket connection, int bufferSize, ILogger<Worker> logger, Settings settings) : base(connection, bufferSize, logger, settings)
        {

        }

        internal override bool ProcessData(SocketAsyncEventArgs args)
        {
            String received = this.sb.ToString();
            _logger.LogDebug("Received data from {0} : \"{1}\". The server has read {2} bytes.", RemotePointInfo, received, received.Length);

            string BureauResponse = string.Empty;
            try
            {
                if ((received.Trim().StartsWith("{") && received.Trim().EndsWith("}")))
                    BureauResponse = new EFXSSNProcessor(_logger, _settings).ExecuteRequest(received, RemotePointInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while posting request to Bureau for: " + RemotePointInfo);
                return false;
            }

            _logger.LogInformation("Received response from EFXSSN for client: {0}", RemotePointInfo);
            if (string.IsNullOrEmpty(BureauResponse))
            {
                _logger.LogInformation("Received empty response from EFXSSN. Closing connection {0}", RemotePointInfo);
                return false;
            }
            else
                _logger.LogDebug("Received response from EFXSSN for client {0}: {1}", RemotePointInfo, BureauResponse);

            Byte[] sendBuffer = Encoding.ASCII.GetBytes(BureauResponse);
            args.SetBuffer(sendBuffer, 0, sendBuffer.Length);
            sb.Length = 0;
            this.currentIndex = 0;
            return true;
        }
    }
}
