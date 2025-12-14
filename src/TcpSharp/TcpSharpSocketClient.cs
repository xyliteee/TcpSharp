using System.Net.Sockets;
using TcpSharp.Interface;
using TcpSharp.Modules;

namespace TcpSharp;
public class TcpSharpSocketClient
{
    #region Public Properties
    public string Host
    {
        get { return _host; }
        set
        {
            if (Connected)
                throw (new Exception("Socket Client is already connected. You cant change this property while connected."));

            _host = value;
        }
    }
    public int Port
    {
        get { return _port; }
        set
        {
            if (Connected)
                throw (new Exception("Socket Client is already connected. You cant change this property while connected."));

            _port = value;
        }
    }
    public bool NoDelay
    {
        get { return _nodelay; }
        set
        {
            _nodelay = value;
            if (_socket != null) _socket.NoDelay = value;
        }
    }
    public bool KeepAlive
    {
        get { return _keepAlive; }
        set
        {
            if (Connected)
                throw (new Exception("Socket Client is already connected. You cant change this property while connected."));

            _keepAlive = value;
        }
    }

    public int KeepAliveTime
    {
        get { return _keepAliveTime; }
        set
        {
            if (Connected)
                throw (new Exception("Socket Client is already connected. You cant change this property while connected."));

            _keepAliveTime = value;
        }
    }

    /// <summary>
    /// Keep-alive interval in seconds
    /// </summary>
    public int KeepAliveInterval
    {
        get { return _keepAliveInterval; }
        set
        {
            if (Connected)
                throw (new Exception("Socket Client is already connected. You cant change this property while connected."));

            _keepAliveInterval = value;
        }
    }

    public int KeepAliveRetryCount
    {
        get { return _keepAliveRetryCount; }
        set
        {
            if (Connected)
                throw (new Exception("Socket Client is already connected. You cant change this property while connected."));

            _keepAliveRetryCount = value;
        }
    }
    public int ReceiveBufferSize
    {
        get { return _receiveBufferSize; }
        set
        {
            _receiveBufferSize = value;
            _recvBuffer = new byte[value];
            if (_socket != null) _socket.ReceiveBufferSize = value;
        }
    }
    public int ReceiveTimeout
    {
        get { return _receiveTimeout; }
        set
        {
            _receiveTimeout = value;
            if (_socket != null) _socket.ReceiveTimeout = value;
        }
    }
    public int SendBufferSize
    {
        get { return _sendBufferSize; }
        set
        {
            _sendBufferSize = value;
            _sendBuffer = new byte[value];
            if (_socket != null) _socket.SendBufferSize = value;
        }
    }
    public int SendTimeout
    {
        get { return _sendTimeout; }
        set
        {
            _sendTimeout = value;
            if (_socket != null) _socket.SendTimeout = value;
        }
    }
    public long BytesReceived
    {
        get { return _bytesReceived; }
        internal set { _bytesReceived = value; }
    }
    public long BytesSent
    {
        get { return _bytesSent; }
        internal set { _bytesSent = value; }
    }
    public bool Reconnect
    {
        get { return _reconnect; }
        set { _reconnect = value; }
    }
    public bool Reconnecting { get; private set; }
    public int ReconnectDelayInSeconds
    {
        get { return _reconnectDelay; }
        set { _reconnectDelay = value; }
    }
    public bool AcceptData
    {
        get { return _acceptData; }
        set { _acceptData = value; }
    }
    public bool Connected { get { return this._socket != null && this._socket.Connected; } }
    #endregion

    #region Private Properties
    private string _host;
    private int _port;
    private bool _nodelay = true;
    private bool _keepAlive = false;
    private int _keepAliveTime = 900;
    private int _keepAliveInterval = 300;
    private int _keepAliveRetryCount = 5;
    private int _receiveBufferSize = 8192;
    private int _receiveTimeout = 0;
    private int _sendBufferSize = 8192;
    private int _sendTimeout = 0;
    private long _bytesReceived;
    private long _bytesSent;
    private bool _reconnect = false;
    private int _reconnectDelay = 5;
    private bool _acceptData = true;
    #endregion

    #region Public Events
    public event EventHandler<OnClientErrorEventArgs> OnError = delegate { };
    public event EventHandler<OnClientConnectedEventArgs> OnConnected = delegate { };
    public event EventHandler<OnClientReconnectedEventArgs> OnReconnected = delegate { };
    public event EventHandler<OnClientDisconnectedEventArgs> OnDisconnected = delegate { };
    public event EventHandler<OnClientDataReceivedEventArgs> OnDataReceived = delegate { };
    #endregion

    #region Event Invokers
    internal void InvokeOnError(OnClientErrorEventArgs args) => this.OnError?.Invoke(this, args);
    internal void InvokeOnConnected(OnClientConnectedEventArgs args) => this.OnConnected?.Invoke(this, args);
    internal void InvokeOnReconnected(OnClientReconnectedEventArgs args) => this.OnReconnected?.Invoke(this, args);
    internal void InvokeOnDisconnected(OnClientDisconnectedEventArgs args) => this.OnDisconnected?.Invoke(this, args);
    internal void InvokeOnDataReceived(OnClientDataReceivedEventArgs args) => this.OnDataReceived?.Invoke(this, args);
    
    #endregion

    #region Private Fields
    private Socket _socket;
    private byte[] _recvBuffer;
    private byte[] _sendBuffer;
    #endregion

    #region Receiver Task
    private CancellationTokenSource _cancellationTokenSource;
    private CancellationToken _cancellationToken;
    #endregion

    #region Constructors
    public TcpSharpSocketClient() : this("127.0.0.1", 1024)
    {
    }

    public TcpSharpSocketClient(string host, int port)
    {
        this.Host = host;
        this.Port = port;
    }
    

    #endregion

    #region Public Methods
    public void Connect()
    {
        // Buffers
        this._recvBuffer = new byte[ReceiveBufferSize];
        this._sendBuffer = new byte[SendBufferSize];

        IPEndPoint serverIPEndPoint;
        IPAddress serverIPAddress;
        if (IPAddress.TryParse(Host, out serverIPAddress))
        {
            // Host is an IP Address, use it directly
            serverIPEndPoint = new IPEndPoint(serverIPAddress, Port);
            this._socket = new Socket(serverIPAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        }
        else
        {
            // Host is a hostname, resolve it to an IP Address
            var serverIPHost = Dns.GetHostEntry(Host);
            if (serverIPHost.AddressList.Length == 0) throw new Exception("Unable to solve host address");
            serverIPAddress = serverIPHost.AddressList[0];
            if (serverIPAddress.ToString() == "::1") serverIPAddress = new IPAddress(16777343); // 127.0.0.1
            serverIPEndPoint = new IPEndPoint(serverIPAddress, Port);

            // Create a TCP/IP socket
            this._socket = new Socket(serverIPAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        }

        // Create a TCP/IP  socket.    
        this._socket = new Socket(serverIPAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        // Set Properties
        this._socket.NoDelay = this.NoDelay;
        this._socket.ReceiveBufferSize = this.ReceiveBufferSize;
        this._socket.ReceiveTimeout = this.ReceiveTimeout;
        this._socket.SendBufferSize = this.SendBufferSize;
        this._socket.SendTimeout = this.SendTimeout;

        /* Keep Alive */
        if (this.KeepAlive && this.KeepAliveInterval > 0)
        {
#if NETCOREAPP3_0_OR_GREATER || NET5_0_OR_GREATER
            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            _socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, this.KeepAliveTime);
            _socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, this.KeepAliveInterval);
            _socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, this.KeepAliveRetryCount);
#elif NETFRAMEWORK
            // Get the size of the uint to use to back the byte array
            // int size = Marshal.SizeOf((uint)0);
            int size = sizeof(uint);

            // Create the byte array
            byte[] keepAlive = new byte[size * 3];

            // Pack the byte array:
            // Turn keepalive on
            Buffer.BlockCopy(BitConverter.GetBytes((uint)1), 0, keepAlive, 0, size);

            // How long does it take to start the first probe (in milliseconds)
            Buffer.BlockCopy(BitConverter.GetBytes((uint)(KeepAliveTime * 1000)), 0, keepAlive, size, size);

            // Detection time interval (in milliseconds)
            Buffer.BlockCopy(BitConverter.GetBytes((uint)(KeepAliveInterval * 1000)), 0, keepAlive, size * 2, size);

            // Set the keep-alive settings on the underlying Socket
            _socket.IOControl(IOControlCode.KeepAliveValues, keepAlive, null);
#elif NETSTANDARD
            // Set the keep-alive settings on the underlying Socket
            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
#endif
        }

        // Connect to Remote EndPoint
        _socket.Connect(serverIPEndPoint);

        // Start Receiver Thread
        if (this._cancellationTokenSource != null) this._cancellationTokenSource.Cancel();
        this._cancellationTokenSource = new CancellationTokenSource();
        this._cancellationToken = this._cancellationTokenSource.Token;
        Task.Factory.StartNew(ReceiverTask, TaskCreationOptions.LongRunning);

        // Invoke OnConnected
        this.InvokeOnConnected(new OnClientConnectedEventArgs
        {
            ServerHost = this.Host,
            ServerPort = this.Port,
        });
    }

    public async Task ConnectAsync()
    {
        _recvBuffer = new byte[ReceiveBufferSize];
        _sendBuffer = new byte[SendBufferSize];

        IPAddress[] addresses;
        if (IPAddress.TryParse(Host, out IPAddress ip))
        {
            addresses = new[] { ip };
        }
        else
        {
            addresses = await Dns.GetHostAddressesAsync(Host).ConfigureAwait(false);
            if (addresses.Length == 0)
                throw new Exception("Unable to resolve host address");
            if (addresses[0].ToString() == "::1")
                addresses[0] = new IPAddress(16777343L);
        }

        _socket = new Socket(addresses[0].AddressFamily, SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = NoDelay,
            ReceiveBufferSize = ReceiveBufferSize,
            ReceiveTimeout = ReceiveTimeout,
            SendBufferSize = SendBufferSize,
            SendTimeout = SendTimeout
        };

        if (KeepAlive && KeepAliveInterval > 0)
            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

        await _socket.ConnectAsync(addresses, Port).ConfigureAwait(false);

        if (_cancellationTokenSource != null)
            _cancellationTokenSource.Cancel();

        _cancellationTokenSource = new CancellationTokenSource();
        _cancellationToken = _cancellationTokenSource.Token;

        _ = Task.Factory.StartNew(ReceiverTask, TaskCreationOptions.LongRunning);

        InvokeOnConnected(new OnClientConnectedEventArgs
        {
            ServerHost = Host,
            ServerPort = Port
        });
    }

    public void Disconnect()
    {
        this.Disconnect(DisconnectReason.None);
    }

    public long SendBytes(byte[] bytes)
    {
        // Check Point
        if (!this.Connected) return 0;

        // Action
        var sent = this._socket.Send(bytes);
        this.BytesSent += sent;

        // Return
        return sent;
    }

    public async Task<long> SendBytesAsync(byte[] bytes, CancellationToken token)
    {
        if (!this.Connected) return 0;
        if (bytes.Length == 0) return 0;

        var args = new SocketAsyncEventArgs
        {
            BufferList = [new ArraySegment<byte>(bytes)]
        };

        if (this._socket.SendAsync(args))
        {
            using (token.Register(() => args.Dispose(), useSynchronizationContext: false))
            {
                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                void Handler(object s, SocketAsyncEventArgs e) => tcs.TrySetResult(e.SocketError == SocketError.Success);
                args.Completed += Handler;
                try
                {
                    await tcs.Task.ConfigureAwait(false);
                }
                finally
                {
                    args.Completed -= Handler;
                }
            }
        }

        if (args.SocketError != SocketError.Success)
        {
            throw new SocketException((int)args.SocketError);
        }

        long sent = args.BytesTransferred;
        this.BytesSent += sent;
        return sent;
    }

    public long SendString(string data)
    {
        // Check Point
        if (!this.Connected) return 0;

        // Action
        var bytes = Encoding.UTF8.GetBytes(data);
        var sent = this._socket.Send(bytes);
        this.BytesSent += sent;

        // Return
        return sent;
    }

    public async Task<long> SendStringAsync(string data, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return await SendBytesAsync(Encoding.UTF8.GetBytes(data), token);
    }

    public long SendString(string data, Encoding encoding)
    {
        // Check Point
        if (!this.Connected) return 0;

        // Action
        var bytes = encoding.GetBytes(data);
        var sent = this._socket.Send(bytes);
        this.BytesSent += sent;

        // Return
        return sent;
    }

    public async Task<long> SendStringAsync(string data, Encoding encoding, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return await SendBytesAsync(encoding.GetBytes(data), token);
    }

    public long SendFile(string fileName)
    {
        // Check Point
        if (!this.Connected) return 0;
        if (!File.Exists(fileName)) return 0;

        // FileInfo
        var fileInfo = new FileInfo(fileName);
        if (fileInfo == null) return 0;

        // Action
        this._socket.SendFile(fileName);
        this.BytesSent += fileInfo.Length;

        // Return
        return fileInfo.Length;
    }

    public async Task<long> SendFileAsync(string fileName, CancellationToken token)
    {
        if (!Connected) return 0;
        if (!File.Exists(fileName)) return 0;

        long fileLen;
        byte[] payload;

        using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read,
                                       bufferSize: 4096, FileOptions.Asynchronous))
        {
            fileLen = fs.Length;
            payload = new byte[fileLen];
            int offset = 0;
            int read;
            while (offset < payload.Length &&
                   (read = await fs.ReadAsync(payload, offset, payload.Length - offset, token)
                                 .ConfigureAwait(false)) > 0)
            {
                offset += read;
            }
        }

        await SendBytesAsync(payload, token).ConfigureAwait(false);
        BytesSent += fileLen;
        return fileLen;
    }

    public long SendFile(string fileName, byte[] preBuffer, byte[] postBuffer, TransmitFileOptions flags)
    {
        // Check Point
        if (!this.Connected) return 0;
        if (!File.Exists(fileName)) return 0;

        // FileInfo
        var fileInfo = new FileInfo(fileName);
        if (fileInfo == null) return 0;

        // Action
        this._socket.SendFile(fileName, preBuffer, postBuffer, flags);
        this.BytesSent += fileInfo.Length;

        // Return
        return fileInfo.Length;
    }

    public async Task<long> SendFileAsync(string fileName, byte[] preBuffer, byte[] postBuffer, TransmitFileOptions flags, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        await Task.CompletedTask;

        return SendFile(fileName, preBuffer, postBuffer, flags);
    }
    #endregion

    #region Private Methods
    private void ReceiverTask()
    {
        try
        {
            while (!_cancellationToken.IsCancellationRequested)
            {
                if (_socket.Poll(1000, SelectMode.SelectRead) && _socket.Available == 0)
                {
                    // Connection has been closed, this likely means the server closed the connection. We call Disconnect to clean up and notify the client.
                    Disconnect(DisconnectReason.None);
                    return;
                }
                // Receive the response from the remote device.    
                var bytesCount = _socket.Receive(_recvBuffer);
                
                if (bytesCount <= 0) continue;
                if (!this.AcceptData) continue;
                var bytes = new byte[bytesCount];
                Array.Copy(_recvBuffer, bytes, bytesCount);

                // Invoke OnDataReceived
                this.InvokeOnDataReceived(new OnClientDataReceivedEventArgs
                {
                    Data = bytes
                });
            }
        }
        catch (SocketException ex)
        {
            Disconnect(ex.SocketErrorCode == SocketError.ConnectionAborted
                ? DisconnectReason.ServerAborted
                : DisconnectReason.None);
        }
        catch (Exception ex)
        {
            // Invoke OnError
            this.InvokeOnError(new OnClientErrorEventArgs
            {
                Exception = ex
            });

            // Disconnect
            Disconnect(DisconnectReason.Exception);
        }
    }

    private void Disconnect(DisconnectReason reason)
    {
        // If already disconnected, no need to do this again.
        if (!Connected) 
        {
            return;
        }
        try
        {
            // Stop Receiver Task
            this._cancellationTokenSource.Cancel();
        }
        catch { }

        try
        {
            // Release the socket.
            _socket.Shutdown(SocketShutdown.Both);
            _socket.Close();
        }
        catch { }

        try
        {
            // Dispose
            _socket.Dispose();
        }
        catch { }
        // Invoke OnDisconnected
        this.InvokeOnDisconnected(new OnClientDisconnectedEventArgs());

        // Reconnect
        if (this.Reconnect && !Reconnecting)
        {
            Reconnecting = true;
            while (!this.Connected)
            {
                try
                {
                    Task.Delay(this.ReconnectDelayInSeconds * 1000);
                    this.Connect();
                    break;
                }
                catch { }
            }
            Reconnecting = false;

            // Invoke OnReconnected
            this.InvokeOnReconnected(new OnClientReconnectedEventArgs
            {
                ServerHost = this.Host,
                ServerPort = this.Port,
            });
        }
    }
    #endregion

}

public class TcpSharpSocketClient<TPacketStruct>
{
    #region Public Properties
    public string Host
    {
        get { return _host; }
        set
        {
            if (Connected)
                throw (new Exception("Socket Client is already connected. You cant change this property while connected."));

            _host = value;
        }
    }
    public int Port
    {
        get { return _port; }
        set
        {
            if (Connected)
                throw (new Exception("Socket Client is already connected. You cant change this property while connected."));

            _port = value;
        }
    }
    public bool NoDelay
    {
        get { return _nodelay; }
        set
        {
            _nodelay = value;
            if (_socket != null) _socket.NoDelay = value;
        }
    }
    public bool KeepAlive
    {
        get { return _keepAlive; }
        set
        {
            if (Connected)
                throw (new Exception("Socket Client is already connected. You cant change this property while connected."));

            _keepAlive = value;
        }
    }

    public int KeepAliveTime
    {
        get { return _keepAliveTime; }
        set
        {
            if (Connected)
                throw (new Exception("Socket Client is already connected. You cant change this property while connected."));

            _keepAliveTime = value;
        }
    }

    /// <summary>
    /// Keep-alive interval in seconds
    /// </summary>
    public int KeepAliveInterval
    {
        get { return _keepAliveInterval; }
        set
        {
            if (Connected)
                throw (new Exception("Socket Client is already connected. You cant change this property while connected."));

            _keepAliveInterval = value;
        }
    }

    public int KeepAliveRetryCount
    {
        get { return _keepAliveRetryCount; }
        set
        {
            if (Connected)
                throw (new Exception("Socket Client is already connected. You cant change this property while connected."));

            _keepAliveRetryCount = value;
        }
    }
    public int ReceiveBufferSize
    {
        get { return _receiveBufferSize; }
        set
        {
            _receiveBufferSize = value;
            _recvBuffer = new byte[value];
            if (_socket != null) _socket.ReceiveBufferSize = value;
        }
    }
    public int ReceiveTimeout
    {
        get { return _receiveTimeout; }
        set
        {
            _receiveTimeout = value;
            if (_socket != null) _socket.ReceiveTimeout = value;
        }
    }
    public int SendBufferSize
    {
        get { return _sendBufferSize; }
        set
        {
            _sendBufferSize = value;
            _sendBuffer = new byte[value];
            if (_socket != null) _socket.SendBufferSize = value;
        }
    }
    public int SendTimeout
    {
        get { return _sendTimeout; }
        set
        {
            _sendTimeout = value;
            if (_socket != null) _socket.SendTimeout = value;
        }
    }
    public long BytesReceived
    {
        get { return _bytesReceived; }
        internal set { _bytesReceived = value; }
    }
    public long BytesSent
    {
        get { return _bytesSent; }
        internal set { _bytesSent = value; }
    }
    public bool Reconnect
    {
        get { return _reconnect; }
        set { _reconnect = value; }
    }
    public bool Reconnecting { get; private set; }
    public int ReconnectDelayInSeconds
    {
        get { return _reconnectDelay; }
        set { _reconnectDelay = value; }
    }
    public bool AcceptData
    {
        get { return _acceptData; }
        set { _acceptData = value; }
    }
    public bool Connected { get { return this._socket != null && this._socket.Connected; } }
    #endregion

    #region Private Properties
    private string _host;
    private int _port;
    private bool _nodelay = true;
    private bool _keepAlive = false;
    private int _keepAliveTime = 900;
    private int _keepAliveInterval = 300;
    private int _keepAliveRetryCount = 5;
    private int _receiveBufferSize = 8192;
    private int _receiveTimeout = 0;
    private int _sendBufferSize = 8192;
    private int _sendTimeout = 0;
    private long _bytesReceived;
    private long _bytesSent;
    private bool _reconnect = false;
    private int _reconnectDelay = 5;
    private bool _acceptData = true;
    private readonly IPacketController<TPacketStruct> _packetController;
    #endregion

    #region Public Events
    public event EventHandler<OnClientErrorEventArgs> OnError = delegate { };
    public event EventHandler<OnClientConnectedEventArgs> OnConnected = delegate { };
    public event EventHandler<OnClientReconnectedEventArgs> OnReconnected = delegate { };
    public event EventHandler<OnClientDisconnectedEventArgs> OnDisconnected = delegate { };
    public event EventHandler<OnClientDataReceivedEventArgs<TPacketStruct>> OnDataReceived = delegate { };
    #endregion

    #region Event Invokers
    internal void InvokeOnError(OnClientErrorEventArgs args) => this.OnError?.Invoke(this, args);
    internal void InvokeOnConnected(OnClientConnectedEventArgs args) => this.OnConnected?.Invoke(this, args);
    internal void InvokeOnReconnected(OnClientReconnectedEventArgs args) => this.OnReconnected?.Invoke(this, args);
    internal void InvokeOnDisconnected(OnClientDisconnectedEventArgs args) => this.OnDisconnected?.Invoke(this, args);
    internal void InvokeOnDataReceived(OnClientDataReceivedEventArgs<TPacketStruct> args) => this.OnDataReceived?.Invoke(this, args);
    
    #endregion

    #region Private Fields
    private Socket _socket;
    private byte[] _recvBuffer;
    private byte[] _sendBuffer;
    #endregion

    #region Receiver Task
    private CancellationTokenSource _cancellationTokenSource;
    private CancellationToken _cancellationToken;
    #endregion

    #region Constructors
    public TcpSharpSocketClient(string host, int port, IPacketController<TPacketStruct> packetController)
    {
        this.Host = host;
        this.Port = port;
        _packetController = packetController;
    }

    #endregion

    #region Public Methods
    public void Connect()
    {
        // Buffers
        this._recvBuffer = new byte[ReceiveBufferSize];
        this._sendBuffer = new byte[SendBufferSize];

        IPEndPoint serverIPEndPoint;
        IPAddress serverIPAddress;
        if (IPAddress.TryParse(Host, out serverIPAddress))
        {
            // Host is an IP Address, use it directly
            serverIPEndPoint = new IPEndPoint(serverIPAddress, Port);
            this._socket = new Socket(serverIPAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        }
        else
        {
            // Host is a hostname, resolve it to an IP Address
            var serverIPHost = Dns.GetHostEntry(Host);
            if (serverIPHost.AddressList.Length == 0) throw new Exception("Unable to solve host address");
            serverIPAddress = serverIPHost.AddressList[0];
            if (serverIPAddress.ToString() == "::1") serverIPAddress = new IPAddress(16777343); // 127.0.0.1
            serverIPEndPoint = new IPEndPoint(serverIPAddress, Port);

            // Create a TCP/IP socket
            this._socket = new Socket(serverIPAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        }

        // Create a TCP/IP  socket.    
        this._socket = new Socket(serverIPAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        // Set Properties
        this._socket.NoDelay = this.NoDelay;
        this._socket.ReceiveBufferSize = this.ReceiveBufferSize;
        this._socket.ReceiveTimeout = this.ReceiveTimeout;
        this._socket.SendBufferSize = this.SendBufferSize;
        this._socket.SendTimeout = this.SendTimeout;

        /* Keep Alive */
        if (this.KeepAlive && this.KeepAliveInterval > 0)
        {
#if NETCOREAPP3_0_OR_GREATER || NET5_0_OR_GREATER
            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            _socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, this.KeepAliveTime);
            _socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, this.KeepAliveInterval);
            _socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, this.KeepAliveRetryCount);
#elif NETFRAMEWORK
            // Get the size of the uint to use to back the byte array
            // int size = Marshal.SizeOf((uint)0);
            int size = sizeof(uint);

            // Create the byte array
            byte[] keepAlive = new byte[size * 3];

            // Pack the byte array:
            // Turn keepalive on
            Buffer.BlockCopy(BitConverter.GetBytes((uint)1), 0, keepAlive, 0, size);

            // How long does it take to start the first probe (in milliseconds)
            Buffer.BlockCopy(BitConverter.GetBytes((uint)(KeepAliveTime * 1000)), 0, keepAlive, size, size);

            // Detection time interval (in milliseconds)
            Buffer.BlockCopy(BitConverter.GetBytes((uint)(KeepAliveInterval * 1000)), 0, keepAlive, size * 2, size);

            // Set the keep-alive settings on the underlying Socket
            _socket.IOControl(IOControlCode.KeepAliveValues, keepAlive, null);
#elif NETSTANDARD
            // Set the keep-alive settings on the underlying Socket
            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
#endif
        }

        // Connect to Remote EndPoint
        _socket.Connect(serverIPEndPoint);

        // Start Receiver Thread
        if (this._cancellationTokenSource != null) this._cancellationTokenSource.Cancel();
        this._cancellationTokenSource = new CancellationTokenSource();
        this._cancellationToken = this._cancellationTokenSource.Token;
        Task.Factory.StartNew(ReceiverTask, TaskCreationOptions.LongRunning);

        // Invoke OnConnected
        this.InvokeOnConnected(new OnClientConnectedEventArgs
        {
            ServerHost = this.Host,
            ServerPort = this.Port,
        });
    }

    public async Task ConnectAsync()
    {
        _recvBuffer = new byte[ReceiveBufferSize];
        _sendBuffer = new byte[SendBufferSize];

        IPAddress[] addresses;
        if (IPAddress.TryParse(Host, out IPAddress ip))
        {
            addresses = new[] { ip };
        }
        else
        {
            addresses = await Dns.GetHostAddressesAsync(Host).ConfigureAwait(false);
            if (addresses.Length == 0)
                throw new Exception("Unable to resolve host address");
            if (addresses[0].ToString() == "::1")
                addresses[0] = new IPAddress(16777343L);
        }

        _socket = new Socket(addresses[0].AddressFamily, SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = NoDelay,
            ReceiveBufferSize = ReceiveBufferSize,
            ReceiveTimeout = ReceiveTimeout,
            SendBufferSize = SendBufferSize,
            SendTimeout = SendTimeout
        };

        if (KeepAlive && KeepAliveInterval > 0)
            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

        await _socket.ConnectAsync(addresses, Port).ConfigureAwait(false);

        if (_cancellationTokenSource != null)
            _cancellationTokenSource.Cancel();

        _cancellationTokenSource = new CancellationTokenSource();
        _cancellationToken = _cancellationTokenSource.Token;

        _ = Task.Factory.StartNew(ReceiverTask, TaskCreationOptions.LongRunning);

        InvokeOnConnected(new OnClientConnectedEventArgs
        {
            ServerHost = Host,
            ServerPort = Port
        });
    }

    public void Disconnect()
    {
        this.Disconnect(DisconnectReason.None);
    }

    public long SendPacket(TPacketStruct packet)
    {
        if (!this.Connected) return 0;
        var bytes = _packetController.Serialize(packet);
        var sent = this._socket.Send(bytes);
        this.BytesSent += sent;
        return sent;
    }
    public async Task<long> SendPacketAsync(TPacketStruct packet, CancellationToken token)
    {
        if (!Connected) return 0;

        byte[] raw = _packetController.Serialize(packet);
        if (raw.Length == 0) return 0;

        byte[] buffer = raw;
        bool rented = false;
        if (raw.Length <= BufferPool.POOL_SIZE)
        {
            buffer = BufferPool.Rent();
            rented = true;
            Buffer.BlockCopy(raw, 0, buffer, 0, raw.Length);
        }

        long sent;
        try
        {
            var seg = new ArraySegment<byte>(buffer, 0, raw.Length);
            var args = new SocketAsyncEventArgs();
            args.SetBuffer(seg.Array, seg.Offset, seg.Count);

            if (_socket.SendAsync(args))
            {
                using (token.Register(args.Dispose, useSynchronizationContext: false))
                {
                    var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    void Handler(object s, SocketAsyncEventArgs e) => tcs.TrySetResult(e.SocketError == SocketError.Success);
                    args.Completed += Handler;
                    try { await tcs.Task.ConfigureAwait(false); }
                    finally { args.Completed -= Handler; }
                }
            }

            if (args.SocketError != SocketError.Success)
                throw new SocketException((int)args.SocketError);

            sent = args.BytesTransferred;
            BytesSent += sent;
        }
        finally
        {
            if (rented) BufferPool.Return(buffer);
        }
        return sent;
    }


    #endregion

    #region Private Methods
    private void ReceiverTask()
    {
        try
        {
            List<byte> accuBuffer = [];
            while (!_cancellationToken.IsCancellationRequested)
            {
                if (_socket.Poll(1000, SelectMode.SelectRead) && _socket.Available == 0)
                {
                    // Connection has been closed, this likely means the server closed the connection. We call Disconnect to clean up and notify the client.
                    Disconnect(DisconnectReason.None);
                    return;
                }
                // Receive the response from the remote device.    
                var bytesCount = _socket.Receive(_recvBuffer);
                
                if (bytesCount <= 0) continue;
                BytesReceived += bytesCount;
                if (!this.AcceptData) continue;
                
                accuBuffer.AddRange(_recvBuffer.Take(bytesCount));
                while (accuBuffer.Count >= _packetController.HeaderLength)
                {
                    var headerData = accuBuffer.Take(_packetController.HeaderLength).ToArray();
                    var bodyLength = _packetController.ReturnBodyLength(headerData);

                    if (accuBuffer.Count >= _packetController.HeaderLength + bodyLength)
                    {
                        var packetData = accuBuffer.Take(_packetController.HeaderLength + bodyLength).ToArray();
                        var packet = _packetController.Deserialize(packetData);
                        InvokeOnDataReceived(new OnClientDataReceivedEventArgs<TPacketStruct> { Packet = packet });
                        accuBuffer.RemoveRange(0, _packetController.HeaderLength + bodyLength);
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }
        catch (SocketException ex)
        {
            if (ex.SocketErrorCode == SocketError.ConnectionAborted)
            {
                Disconnect(DisconnectReason.ServerAborted);
            }
            else
            {
                Disconnect(DisconnectReason.None);
            }
        }
        catch (Exception ex)
        {
            // Invoke OnError
            this.InvokeOnError(new OnClientErrorEventArgs
            {
                Exception = ex
            });

            // Disconnect
            Disconnect(DisconnectReason.Exception);
        }
    }

    private void Disconnect(DisconnectReason reason)
    {
        // If already disconnected, no need to do this again.
        if (!Connected) 
        {
            return;
        }
        try
        {
            // Stop Receiver Task
            this._cancellationTokenSource.Cancel();
        }
        catch { }

        try
        {
            // Release the socket.
            _socket.Shutdown(SocketShutdown.Both);
            _socket.Close();
        }
        catch { }

        try
        {
            // Dispose
            _socket.Dispose();
        }
        catch { }
        // Invoke OnDisconnected
        this.InvokeOnDisconnected(new OnClientDisconnectedEventArgs());

        // Reconnect
        if (this.Reconnect && !Reconnecting)
        {
            Reconnecting = true;
            while (!this.Connected)
            {
                try
                {
                    Task.Delay(this.ReconnectDelayInSeconds * 1000);
                    this.Connect();
                    break;
                }
                catch { }
            }
            Reconnecting = false;

            // Invoke OnReconnected
            this.InvokeOnReconnected(new OnClientReconnectedEventArgs
            {
                ServerHost = this.Host,
                ServerPort = this.Port,
            });
        }
    }
    #endregion

}
