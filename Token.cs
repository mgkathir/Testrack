using BureauProcessor;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace BureauAdaptor
{
    internal delegate void ProcessData(SocketAsyncEventArgs args);

    /// <summary>
    /// Token for use with SocketAsyncEventArgs.
    /// </summary>
    internal class Token : IDisposable
    {
        private Socket connection;

        protected StringBuilder sb;

        protected string remotePointInfo;

        protected Int32 currentIndex;

        protected readonly ILogger<Worker> _logger;

        protected readonly Settings _settings;

        /// <summary>
        /// Class constructor.
        /// </summary>
        /// <param name="connection">Socket to accept incoming data.</param>
        /// <param name="bufferSize">Buffer size for accepted data.</param>
        internal Token(Socket connection, Int32 bufferSize, ILogger<Worker> logger, Settings settings)
        {
            this._logger = logger;
            this._settings = settings;
            this.connection = connection;
            this.sb = new StringBuilder(bufferSize);
            this.remotePointInfo = connection.RemoteEndPoint != null ? "[" + ((IPEndPoint)connection.RemoteEndPoint).Address + "][" + ((IPEndPoint)connection.RemoteEndPoint).Port + "]"
                                                             : string.Empty;
        }

        /// <summary>
        /// Accept socket.
        /// </summary>
        internal Socket Connection
        {
            get { return this.connection; }
        }

        internal string RemotePointInfo
        {
            get { return this.remotePointInfo; }
        }

        /// <summary>
        /// Process data received from the client.
        /// </summary>
        /// <param name="args">SocketAsyncEventArgs used in the operation.</param>
        internal virtual bool ProcessData(SocketAsyncEventArgs args)
        {
            return false;
        }

        /// <summary>
        /// Set data received from the client.
        /// </summary>
        /// <param name="args">SocketAsyncEventArgs used in the operation.</param>
        internal void SetData(SocketAsyncEventArgs args)
        {
            Int32 count = args.BytesTransferred;

            if ((this.currentIndex + count) > this.sb.Capacity)
            {
                throw new ArgumentOutOfRangeException("count",
                    String.Format(CultureInfo.CurrentCulture, "Adding {0} bytes on buffer which has {1} bytes, the listener buffer will overflow.", count, this.currentIndex));
            }

            sb.Append(Encoding.ASCII.GetString(args.Buffer, args.Offset, count));
            this.currentIndex += count;
        }

        #region IDisposable Members

        /// <summary>
        /// Release instance.
        /// </summary>
        public void Dispose()
        {
            try
            {
                this.connection.Shutdown(SocketShutdown.Send);
            }
            catch (Exception)
            {
                // Throw if client has closed, so it is not necessary to catch.
            }
            finally
            {
                this.connection.Close();
            }
        }

        #endregion IDisposable Members
    }
}