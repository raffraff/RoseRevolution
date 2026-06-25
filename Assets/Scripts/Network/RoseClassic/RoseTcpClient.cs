using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using RoseClassic.PacketIO;

namespace RoseClassic
{
    public sealed class RoseTcpClient : IDisposable
    {
        readonly ConcurrentQueue<byte[]> receiveQueue = new ConcurrentQueue<byte[]>();
        TcpClient tcpClient;
        CancellationTokenSource cancellationTokenSource;
        Task receiveTask;
        byte[] readBuffer = new byte[PacketConstants.MaxPacketSize];
        int pendingSize = -1;
        int pendingBytes;
        byte[] pendingPacket;

        public bool IsConnected => tcpClient?.Connected ?? false;

        public event Action Connected;
        public event Action Disconnected;

        public async Task ConnectAsync(string host, int port, CancellationToken token = default)
        {
            Disconnect();

            tcpClient = new TcpClient();
            cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);

            await tcpClient.ConnectAsync(host, port);
            receiveTask = ReceiveLoopAsync(cancellationTokenSource.Token);

            EnqueueNetworkStatus(NetworkStatus.Connect);
            Connected?.Invoke();
        }

        public void Send(byte[] packet)
        {
            if (!IsConnected || packet == null || packet.Length == 0 || tcpClient?.Client == null)
                return;

            try
            {
                tcpClient.GetStream().Write(packet, 0, packet.Length);
            }
            catch (System.Exception ex)
            {
                RoseDebug.LogError($"Rose TCP send error: {ex.Message}");
            }
        }

        public bool TryDequeue(out byte[] packet) => receiveQueue.TryDequeue(out packet);

        public void Disconnect()
        {
            if (cancellationTokenSource != null)
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource.Dispose();
                cancellationTokenSource = null;
            }

            if (tcpClient != null)
            {
                try { tcpClient.Close(); } catch { }
                tcpClient = null;
            }

            pendingSize = -1;
            pendingBytes = 0;
            pendingPacket = null;
        }

        void EnqueueNetworkStatus(byte status)
        {
            var packet = new byte[35];
            BitConverter.GetBytes((short)35).CopyTo(packet, 0);
            BitConverter.GetBytes(Opcodes.SocketNetworkStatus).CopyTo(packet, 2);
            packet[6] = status;
            receiveQueue.Enqueue(packet);
        }

        async Task ReceiveLoopAsync(CancellationToken token)
        {
            var stream = tcpClient.GetStream();

            try
            {
                while (!token.IsCancellationRequested && tcpClient.Connected)
                {
                    int read = await stream.ReadAsync(readBuffer, 0, readBuffer.Length, token);
                    if (read <= 0)
                        break;

                    int offset = 0;
                    while (offset < read)
                    {
                        if (pendingSize < 0)
                        {
                            int needed = 2 - pendingBytes;
                            int copy = Math.Min(needed, read - offset);
                            if (pendingPacket == null)
                                pendingPacket = new byte[PacketConstants.MaxPacketSize];

                            Buffer.BlockCopy(readBuffer, offset, pendingPacket, pendingBytes, copy);
                            pendingBytes += copy;
                            offset += copy;

                            if (pendingBytes < 2)
                                continue;

                            pendingSize = BitConverter.ToInt16(pendingPacket, 0);
                            if (pendingSize <= 0 || pendingSize > PacketConstants.MaxPacketSize)
                            {
                                DisconnectInternal();
                                return;
                            }
                        }

                        int bodyNeeded = pendingSize - pendingBytes;
                        int bodyCopy = Math.Min(bodyNeeded, read - offset);
                        Buffer.BlockCopy(readBuffer, offset, pendingPacket, pendingBytes, bodyCopy);
                        pendingBytes += bodyCopy;
                        offset += bodyCopy;

                        if (pendingBytes >= pendingSize)
                        {
                            var completed = new byte[pendingSize];
                            Buffer.BlockCopy(pendingPacket, 0, completed, 0, pendingSize);
                            receiveQueue.Enqueue(completed);

                            pendingSize = -1;
                            pendingBytes = 0;
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                RoseDebug.LogError($"Rose TCP receive error: {ex.Message}");
            }
            finally
            {
                DisconnectInternal();
            }
        }

        void DisconnectInternal()
        {
            if (tcpClient == null)
                return;

            EnqueueNetworkStatus(NetworkStatus.Disconnect);
            Disconnect();
            Disconnected?.Invoke();
        }

        public void Dispose() => Disconnect();
    }
}
