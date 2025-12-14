using System.Net.Sockets;

namespace TcpSharp;

public class ConnectedClient
{
    /* Public Properties */
    public TcpClient Client { get; internal set; }
    public string ConnectionId { get; internal set; }
    public bool Connected { get { return this.Client != null && this.Client.Connected; } }
    public bool AcceptData { get; internal set; } = true;
    public long BytesReceived { get; private set; }
    public long BytesSent { get; private set; }

    /* Reference Fields */
    private readonly TcpSharpSocketServer _server;

    /* Private Fields */
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly CancellationToken _cancellationToken;

    internal ConnectedClient(TcpSharpSocketServer server, TcpClient client, string connectionId)
    {
        this.Client = client;
        this.ConnectionId = connectionId;

        this._server = server;
        this._cancellationTokenSource = new CancellationTokenSource();
        this._cancellationToken = this._cancellationTokenSource.Token;
    }

    internal void StartReceiving()
    {
        Task.Factory.StartNew(ReceivingTask, TaskCreationOptions.LongRunning);
    }

    internal void StopReceiving()
    {
        this._cancellationTokenSource.Cancel();
    }

    private async Task ReceivingTask()
    {
        var stream = this.Client.GetStream();
        var buffer = new byte[this.Client.ReceiveBufferSize];
        try
        {
            var bytesCount = 0;
            while (!this._cancellationToken.IsCancellationRequested && (bytesCount = await stream.ReadAsync(buffer, 0, buffer.Length, this._cancellationToken)) != 0)
            {
                // Increase BytesReceived
                BytesReceived += bytesCount;
                this._server.AddReceivedBytes(bytesCount);

                // Invoke OnDataReceived
                if (this.AcceptData)
                {
                    var bytesReceived = new byte[bytesCount];
                    Array.Copy(buffer, bytesReceived, bytesCount);
                    this._server.InvokeOnDataReceived(new OnServerDataReceivedEventArgs
                    {
                        Client = this.Client,
                        ConnectionId = this.ConnectionId,
                        Data = bytesReceived
                    });
                }
            }
            // If we exit the loop, it means the client has closed the connection
            this._server.Disconnect(this.ConnectionId, DisconnectReason.None);
        }
        catch (IOException)
        {
            // Disconnect
            this._server.Disconnect(this.ConnectionId, DisconnectReason.Exception);
        }
        catch (Exception ex)
        {
            // Invoke OnError
            this._server.InvokeOnError(new OnServerErrorEventArgs
            {
                Client = this.Client,
                ConnectionId = this.ConnectionId,
                Exception = ex
            });

            // Disconnect
            this._server.Disconnect(this.ConnectionId, DisconnectReason.Exception);
        }
    }

    public long SendBytes(byte[] bytes)
    {
        if (!this.Connected) return 0;

        this.BytesSent += bytes.Length;
        this._server.AddSentBytes(bytes.Length);

        return this.Client.Client.Send(bytes);
    }

    public long SendString(string data)
    {
        if (!this.Connected) return 0;

        var bytes = Encoding.UTF8.GetBytes(data);
        this.BytesSent += bytes.Length;
        this._server.AddSentBytes(bytes.Length);

        return this.Client.Client.Send(bytes);
    }

    public long SendString(string data, Encoding encoding)
    {
        if (!this.Connected) return 0;

        var bytes = encoding.GetBytes(data);
        this.BytesSent += bytes.Length;
        this._server.AddSentBytes(bytes.Length);

        return this.Client.Client.Send(bytes);
    }

    public long SendFile(string filePath)
    {
        // Check Point
        if (!this.Connected) return 0;
        if (!File.Exists(filePath)) return 0;

        // FileInfo
        var fileInfo = new FileInfo(filePath);
        if (fileInfo == null) return 0;

        // Action
        this.Client.Client.SendFile(filePath);
        this.BytesSent += fileInfo.Length;
        this._server.AddSentBytes(fileInfo.Length);

        // Return
        return fileInfo.Length;
    }

    public long SendFile(string filePath, byte[] preBuffer, byte[] postBuffer, TransmitFileOptions flags)
    {
        // Check Point
        if (!this.Connected) return 0;
        if (!File.Exists(filePath)) return 0;

        // FileInfo
        var fileInfo = new FileInfo(filePath);
        if (fileInfo == null) return 0;

        // Action
        this.Client.Client.SendFile(filePath, preBuffer, postBuffer, flags);
        this.BytesSent += fileInfo.Length;
        this._server.AddSentBytes(fileInfo.Length);

        // Return
        return fileInfo.Length;
    }

}

public class ConnectedClient<TPacketStruct>
{
    /* Public Properties */
    public TcpClient Client { get; internal set; }
    public string ConnectionId { get; internal set; }
    public bool Connected { get { return this.Client != null && this.Client.Connected; } }
    public bool AcceptData { get; internal set; } = true;
    public long BytesReceived { get; private set; }
    public long BytesSent { get; private set; }

    /* Reference Fields */
    private readonly TcpSharpSocketServer<TPacketStruct> _server;

    /* Private Fields */
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly CancellationToken _cancellationToken;

    internal ConnectedClient(TcpSharpSocketServer<TPacketStruct> server, TcpClient client, string connectionId)
    {
        this.Client = client;
        this.ConnectionId = connectionId;

        this._server = server;
        this._cancellationTokenSource = new CancellationTokenSource();
        this._cancellationToken = this._cancellationTokenSource.Token;
    }

    internal void StartReceiving()
    {
        Task.Factory.StartNew(ReceivingTask, TaskCreationOptions.LongRunning);
    }

    internal void StopReceiving()
    {
        this._cancellationTokenSource.Cancel();
    }

    private async Task ReceivingTask()
    {
        var stream = this.Client.GetStream();
        var buffer = new byte[this.Client.ReceiveBufferSize];
        try
        {
            var bytesCount = 0;
            List<byte> accuBuffer = [];
            while (!this._cancellationToken.IsCancellationRequested && (bytesCount = await stream.ReadAsync(buffer, 0, buffer.Length, this._cancellationToken)) != 0)
            {
                // Increase BytesReceived
                if (bytesCount <= 0) continue;
                BytesReceived += bytesCount;
                this._server.AddReceivedBytes(bytesCount);
                if (!this.AcceptData) continue;
                
                accuBuffer.AddRange(buffer.Take(bytesCount));
                while (accuBuffer.Count >= _server.PacketController.HeaderLength)
                {
                    var headerData = accuBuffer.Take(_server.PacketController.HeaderLength).ToArray();
                    var bodyLength = _server.PacketController.ReturnBodyLength(headerData);

                    if (accuBuffer.Count >= _server.PacketController.HeaderLength + bodyLength)
                    {
                        var packetData = accuBuffer.Take(_server.PacketController.HeaderLength + bodyLength).ToArray();
                        var packet = _server.PacketController.Deserialize(packetData);
                        _server.InvokeOnDataReceived(new OnServerDataReceivedEventArgs<TPacketStruct> { Packet = packet });
                        accuBuffer.RemoveRange(0, _server.PacketController.HeaderLength + bodyLength);
                    }
                    else
                    {
                        break;
                    }
                }
            }
            // If we exit the loop, it means the client has closed the connection
            this._server.Disconnect(this.ConnectionId, DisconnectReason.None);
        }
        catch (IOException)
        {
            // Disconnect
            this._server.Disconnect(this.ConnectionId, DisconnectReason.Exception);
        }
        catch (Exception ex)
        {
            // Invoke OnError
            this._server.InvokeOnError(new OnServerErrorEventArgs
            {
                Client = this.Client,
                ConnectionId = this.ConnectionId,
                Exception = ex
            });

            // Disconnect
            this._server.Disconnect(this.ConnectionId, DisconnectReason.Exception);
        }
    }

    public long SendPacket(TPacketStruct packet)
    {
        return !this.Connected ? 0 : this.Client.Client.Send(_server.PacketController.Serialize(packet));
    }

    public async Task<long> SendPacketAsync(TPacketStruct packet, CancellationToken token = default)
    {
        if (!this.Connected) return 0;
        byte[] payload = _server.PacketController.Serialize(packet);
        if (payload.Length == 0) return 0;
        await this.Client.GetStream().WriteAsync(payload, 0, payload.Length, token).ConfigureAwait(false);
        this.BytesSent += payload.Length;
        return payload.Length;
    }
}