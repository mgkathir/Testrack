using BureauProcessor;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Net.Sockets;

namespace BureauAdaptor
{
    internal sealed class SocketListener
    {
        private readonly ILogger<Worker> _logger;

        private readonly Settings _settings;

        private Socket listenSocket;

        private static Mutex mutex = new Mutex();

        private Int32 bufferSize;

        private Int32 numConnectedSockets;

        private Int32 numConnections;

        private string _bureau;

        private SocketAsyncEventArgsPool readWritePool;

        private Semaphore semaphoreAcceptedClients;

        internal SocketListener(Int32 numConnections, Int32 bufferSize, ILogger<Worker> logger, Settings settings, string Bureau)
        {
            this._logger = logger;
            this._settings = settings;
            this.numConnectedSockets = 0;
            this.numConnections = numConnections;
            this.bufferSize = bufferSize;
            this._bureau = Bureau;

            this.readWritePool = new SocketAsyncEventArgsPool(numConnections);
            this.semaphoreAcceptedClients = new Semaphore(numConnections, numConnections);

            // Preallocate pool of SocketAsyncEventArgs objects.
            for (Int32 i = 0; i < this.numConnections; i++)
            {
                SocketAsyncEventArgs readWriteEventArg = new SocketAsyncEventArgs();
                readWriteEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(OnIOCompleted);
                readWriteEventArg.SetBuffer(new Byte[this.bufferSize], 0, this.bufferSize);

                // Add SocketAsyncEventArg to the pool.
                this.readWritePool.Push(readWriteEventArg);
            }
        }

        private void CloseClientSocket(SocketAsyncEventArgs e)
        {
            Token token = e.UserToken as Token;
            this.CloseClientSocket(token, e);
        }

        private void CloseClientSocket(Token token, SocketAsyncEventArgs e)
        {
            string clientinfo = token.RemotePointInfo;
            token.Dispose();

            // Decrement the counter keeping track of the total number of clients connected to the server.
            this.semaphoreAcceptedClients.Release();
            Interlocked.Decrement(ref this.numConnectedSockets);
            _logger.LogInformation("A client {0} has been disconnected from the server. There are {1} clients connected to the server", clientinfo, this.numConnectedSockets);

            // Free the SocketAsyncEventArg so they can be reused by another client.
            this.readWritePool.Push(e);
        }

        /// <summary>
        /// Callback method associated with Socket.AcceptAsync
        /// operations and is invoked when an accept operation is complete.
        /// </summary>
        /// <param name="sender">Object who raised the event.</param>
        /// <param name="e">SocketAsyncEventArg associated with the completed accept operation.</param>
        private void OnAcceptCompleted(object sender, SocketAsyncEventArgs e)
        {
            this.ProcessAccept(e);
        }

        /// <summary>
        /// Callback called whenever a receive or send operation is completed on a socket.
        /// </summary>
        /// <param name="sender">Object who raised the event.</param>
        /// <param name="e">SocketAsyncEventArg associated with the completed send/receive operation.</param>
        private void OnIOCompleted(object sender, SocketAsyncEventArgs e)
        {
            // Determine which type of operation just completed and call the associated handler.
            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Receive:
                    this.ProcessReceive(e);
                    break;

                case SocketAsyncOperation.Send:
                    this.ProcessSend(e);
                    break;

                default:
                    throw new ArgumentException("The last operation completed on the socket was not a receive or send");
            }
        }

        /// <summary>
        /// Process the accept for the socket listener.
        /// </summary>
        /// <param name="e">SocketAsyncEventArg associated with the completed accept operation.</param>
        private void ProcessAccept(SocketAsyncEventArgs e)
        {
            Socket? s = e.AcceptSocket;
            if (s != null && s.Connected)
            {
                try
                {
                    SocketAsyncEventArgs readEventArgs = this.readWritePool.Pop();
                    if (readEventArgs != null)
                    {
                        // Get the socket for the accepted client connection and put it into the
                        // ReadEventArg object user token.
                        if (this._bureau.ToUpper() == "TU")
                            readEventArgs.UserToken = new TUClientToken(s, this.bufferSize, _logger, _settings);
                        else if (this._bureau.ToUpper() == "EFX")
                            readEventArgs.UserToken = new EFXClientToken(s, this.bufferSize, _logger, _settings);
                        else if (this._bureau.ToUpper() == "XPN")
                            readEventArgs.UserToken = new XPNClientToken(s, this.bufferSize, _logger, _settings);
                        else if (this._bureau.ToUpper() == "SSN")
                            readEventArgs.UserToken = new SSNClientToken(s, this.bufferSize, _logger, _settings);
                        else if (this._bureau.ToUpper() == "EFXSSN")
                            readEventArgs.UserToken = new EFXSSNClientToken(s, this.bufferSize, _logger, _settings);

                        Interlocked.Increment(ref this.numConnectedSockets);
                        _logger.LogInformation("Client connection accepted from {0}. There are {1} clients connected to the server",
                            ((Token)(readEventArgs.UserToken)).RemotePointInfo, this.numConnectedSockets);

                        if (!s.ReceiveAsync(readEventArgs))
                        {
                            this.ProcessReceive(readEventArgs);
                        }
                    }
                    else
                    {
                        _logger.LogInformation("There are no more available sockets to allocate.");
                    }
                }
                catch (SocketException ex)
                {
                    _logger.LogError("Error when processing data received from {0}:\r\n{1}", (e.UserToken as Token)?.Connection?.RemoteEndPoint, ex.ToString());
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.ToString());
                }

                // Accept the next connection request.
                this.StartAccept(e);
            }
        }

        private void ProcessError(SocketAsyncEventArgs e)
        {
            Token token = e.UserToken as Token;
            IPEndPoint localEp = token.Connection.LocalEndPoint as IPEndPoint;

            this.CloseClientSocket(token, e);

            _logger.LogError("Socket error {0} on endpoint {1} during {2}.", (Int32)e.SocketError, localEp, e.LastOperation);
        }

        private void ProcessReceive(SocketAsyncEventArgs e)
        {
            // Check if the remote host closed the connection.
            if (e.SocketError == SocketError.Success)
            {
                Token token = e.UserToken as Token;
                token.SetData(e);

                Socket s = token.Connection;
                if (s.Available == 0)
                {
                    /*Removed below logic to make parallel processing of bureau requests*/
                    // Set return buffer.
                    //if (token.ProcessData(e))
                    //{
                    //    if (!s.SendAsync(e))
                    //    {
                    //        // Set the buffer to send back to the client.
                    //        this.ProcessSend(e);
                    //    }
                    //}
                    //else
                    //    this.CloseClientSocket(e);
                    /*Removed above logic to make parallel processing of bureau requests*/
                    //IMplemenetd to handle Parallel requests to Bureau.
                    var t = new Thread(() => ProcessDataThread(token, e));
                    t.Start();
                }
                else if (!s.ReceiveAsync(e))
                {
                    // Read the next block of data sent by client.
                    this.ProcessReceive(e);
                }
            }
            else
            {
                this.ProcessError(e);
            }
        }
        private void ProcessDataThread(Token token,SocketAsyncEventArgs e)
        {
            Socket s = token.Connection;
            if (token.ProcessData(e))
            {
                if (!s.SendAsync(e))
                {
                    // Set the buffer to send back to the client.
                    this.ProcessSend(e);
                }
            }
            else
                this.CloseClientSocket(e);
        }

        private void ProcessSend(SocketAsyncEventArgs e)
        {
            if (e.SocketError == SocketError.Success)
            {
                // Done echoing data back to the client.
                Token token = e.UserToken as Token;

                if (!token.Connection.ReceiveAsync(e))
                {
                    // Read the next block of data send from the client.
                    this.ProcessReceive(e);
                }
                //else {
                //    token.Dispose();
                //}
            }
            else
            {
                this.ProcessError(e);
            }
        }

        internal void Start(Int32 port)
        {
            /*
            // Get host related information.
            IPAddress[] addressList = Dns.GetHostEntry(Environment.MachineName).AddressList;

            // Get endpoint for the listener.
            IPEndPoint localEndPoint = new IPEndPoint(addressList[addressList.Length - 1], port);

            // Create the socket which listens for incoming connections.
            this.listenSocket = new Socket(localEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            this.listenSocket.ReceiveBufferSize = this.bufferSize;
            this.listenSocket.SendBufferSize = this.bufferSize;

            if (localEndPoint.AddressFamily == AddressFamily.InterNetworkV6)
            {
                // Set dual-mode (IPv4 & IPv6) for the socket listener.
                this.listenSocket.SetSocketOption(SocketOptionLevel.IPv6, (SocketOptionName)27, false);
                this.listenSocket.Bind(new IPEndPoint(IPAddress.IPv6Any, localEndPoint.Port));
            }
            else
            {
                // Associate the socket with the local endpoint.
                this.listenSocket.Bind(localEndPoint);
            }
            */
            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, port);
            this.listenSocket = new Socket(localEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            this.listenSocket.ReceiveBufferSize = this.bufferSize;
            this.listenSocket.SendBufferSize = this.bufferSize;
            this.listenSocket.Bind(localEndPoint);

            // Start the server.
            this.listenSocket.Listen(this.numConnections);

            // Post accepts on the listening socket.
            this.StartAccept(null);

            // Blocks the current thread to receive incoming messages.
            mutex.WaitOne();
        }

        private void StartAccept(SocketAsyncEventArgs acceptEventArg)
        {
            if (acceptEventArg == null)
            {
                acceptEventArg = new SocketAsyncEventArgs();
                acceptEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(OnAcceptCompleted);
            }
            else
            {
                // Socket must be cleared since the context object is being reused.
                acceptEventArg.AcceptSocket = null;
            }

            this.semaphoreAcceptedClients.WaitOne();
            if (!this.listenSocket.AcceptAsync(acceptEventArg))
            {
                this.ProcessAccept(acceptEventArg);
            }
        }

        internal void Stop()
        {
            this.listenSocket.Close();
            mutex.ReleaseMutex();
        }
    }
}