using MMONET.Message;
using Network.Remote;
using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace MMONET.Remote
{
    /// <summary>
    /// 不支持多播地址 每包大小最好不要大于 523（548 - 框架报头8+17）
    /// </summary>
    public partial class UDPRemote : RemoteBase, IRemote, ISuperRemote
    {
        public Socket Client => udpClient?.Client;
        public UdpClient udpClient;
        public EndPoint RemappedEndPoint => udpClient?.Client.RemoteEndPoint;

        public UDPRemote(AddressFamily addressFamily = AddressFamily.InterNetworkV6) :
            this(new UdpClient(0, addressFamily))
        {

        }

        internal UDPRemote(UdpClient udp)
        {
            udpClient = udp;
            IsVaild = true;
        }

        void OnSocketException(SocketError error)
        {
            udpClient?.Close();
            OnDisConnect?.Invoke(error);
        }

        #region IDisposable Support
        private bool disposedValue = false; // 要检测冗余调用

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: 释放托管状态(托管对象)。
                    udpClient.Dispose();
                }

                // TODO: 释放未托管的资源(未托管的对象)并在以下内容中替代终结器。
                // TODO: 将大型字段设置为 null。
                IsVaild = false;
                disposedValue = true;
            }
        }

        // TODO: 仅当以上 Dispose(bool disposing) 拥有用于释放未托管资源的代码时才替代终结器。
        // ~UDPRemote2() {
        //   // 请勿更改此代码。将清理代码放入以上 Dispose(bool disposing) 中。
        //   Dispose(false);
        // }

        // 添加此代码以正确实现可处置模式。
        public void Dispose()
        {
            // 请勿更改此代码。将清理代码放入以上 Dispose(bool disposing) 中。
            Dispose(true);
            // TODO: 如果在以上内容中替代了终结器，则取消注释以下行。
            // GC.SuppressFinalize(this);
        }
        #endregion

    }

    ///连接
    partial class UDPRemote
    {
        public event Action<SocketError> OnDisConnect;

        bool isConnecting = false;

        /// <summary>
        /// IPv4 和 IPv6不能共用
        /// </summary>
        /// <param name="endPoint"></param>
        /// <param name="retryCount"></param>
        /// <returns></returns>
        public async Task<Exception> ConnectAsync(IPEndPoint endPoint, int retryCount = 0)
        {
            if (isConnecting)
            {
                return new Exception("连接正在进行中");
            }
            isConnecting = true;

            if (this.Client.AddressFamily != endPoint.AddressFamily)
            {
                ///IP版本转换
                this.ConnectIPEndPoint = new IPEndPoint(
                    this.Client.AddressFamily == AddressFamily.InterNetworkV6 ? endPoint.Address.MapToIPv6() :
                    endPoint.Address.MapToIPv4(), endPoint.Port);
            }
            else
            {
                this.ConnectIPEndPoint = endPoint;
            }

            while (retryCount >= 0)
            {
                try
                {
                    var res = await this.ConnectAsync();
                    if (res)
                    {
                        isConnecting = false;
                        ReceiveStart();
                        return null;
                    }
                }
                catch (Exception e)
                {
                    if (retryCount <= 0)
                    {
                        isConnecting = false;
                        return e;
                    }
                }
                finally
                {
                    retryCount--;
                }
            }

            isConnecting = false;
            return new SocketException((int)SocketError.TimedOut);
        }

        public void Disconnect()
        {
            IsVaild = false;
            manualDisconnecting = true;
            udpClient?.Close();
        }

        int lastseq;
        int lastack;
        async Task<bool> ConnectAsync()
        {
            lastseq = new Random().Next(0, 10000);
            var buffer = MakeUDPConnectMessage(1, 0, lastseq, lastack);
            CancellationTokenSource source = new CancellationTokenSource();
            TaskCompletionSource<bool> taskCompletion = new TaskCompletionSource<bool>();

#pragma warning disable CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法

            Task.Run(async () =>
            {
                while (true)
                {
                    var recv = await udpClient.ReceiveAsync();
                    var (Size, MessageID, RpcID) = MessagePipline.Default.ParsePacketHeader(recv.Buffer);
                    if (MessageID == MSGID.UdpConnectMessageID)
                    {
                        var (SYN, ACK, seq, ack) = ReadConnectMessage(recv.Buffer);
                        if (SYN == 1 && ACK == 1 && lastseq + 1 == ack)
                        {
                            ///ESTABLISHED

                            udpClient.Connect(recv.RemoteEndPoint);
                            break;
                        }
                    }
                }
                source.Cancel();
                taskCompletion.SetResult(true);

            }, source.Token);


            Task.Run(async () =>
            {
                while (true)
                {
                    await udpClient.SendAsync(buffer,buffer.Length, ConnectIPEndPoint);
                    await Task.Delay(1000);
                }
            }, source.Token);

            Task.Run(async () =>
            {
                await Task.Delay(5000, source.Token);
                ///一段时间没有反应，默认失败。
                source.Cancel();
                taskCompletion.TrySetException(new TimeoutException());

            }, source.Token);


#pragma warning restore CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法

            return await taskCompletion.Task;
        }

        internal async Task<bool> TryAccept(UdpReceiveResult udpReceive)
        {
            if (Client.Connected && this.Client.RemoteEndPoint.Equals(udpReceive.RemoteEndPoint))
            {
                ///已经成功连接，忽略连接请求
                return true;
            }

            ///LISTEN;
            var (SYN, ACK, seq, ack) = ReadConnectMessage(udpReceive.Buffer);

            if (SYN == 1 && ACK == 0)
            {
                ///SYN_RCVD;
                lastack = new Random().Next(0, 10000);
                lastseq = seq;

                ConnectIPEndPoint = udpReceive.RemoteEndPoint;

                ///绑定远端
                udpClient.Connect(udpReceive.RemoteEndPoint);
                var buffer = MakeUDPConnectMessage(1, 1, lastack, seq + 1);

#pragma warning disable CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法
                Task.Run(async () =>
                {
                    for (int i = 0; i < 3; i++)
                    {
                        udpClient.Send(buffer,buffer.Length);
                        await Task.Delay(800);
                    }
                });
#pragma warning restore CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法

                return true;
            }
            else
            {
                ///INVALID;
                return false;
            }
        }

        static (int SYN, int ACK, int seq, int ack) ReadConnectMessage(byte[] buffer)
        {
            ReadOnlySpan<byte> bf = buffer.AsSpan(9);

            int SYN = bf.ReadInt();
            int ACK = bf.Slice(4).ReadInt();
            int seq = bf.Slice(8).ReadInt();
            int ack = bf.Slice(12).ReadInt();
            return (SYN, ACK, seq, ack);
        }

        static byte[] MakeUDPConnectMessage(int SYN, int ACT, int seq, int ack)
        {
            var bf = new byte[25];
            ((ushort)25).WriteTo(bf);
            MSGID.UdpConnectMessageID.WriteTo(bf.AsSpan(2));
            ((short)0).WriteTo(bf.AsSpan(6));
            bf[8] = 0;
            int tempOffset = 9;
            SYN.WriteTo(bf.AsSpan(tempOffset));
            ACT.WriteTo(bf.AsSpan(tempOffset + 4));
            seq.WriteTo(bf.AsSpan(tempOffset + 8 ));
            ack.WriteTo(bf.AsSpan(tempOffset + 12));
            return bf;
        }
    }

    /// 发送
    partial class UDPRemote
    {

        /// <summary>
        /// 正常发送入口
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="rpcID"></param>
        /// <param name="message"></param>
        protected override void SendAsync<T>(short rpcID, T message)
        {
            SendByteBufferAsync(Packer.Packet(rpcID, message, this));
        }

        /// <summary>
        /// 注意，发送完成时内部回收了buffer。
        /// ((框架约定1)发送字节数组发送完成后由发送逻辑回收)
        /// </summary>
        /// <param name="bufferMsg"></param>
        protected void SendByteBufferAsync(IMemoryOwner<byte> bufferMsg)
        {
            try
            {
                Task.Run(() =>
                        {
                            try
                            {
                                if (MemoryMarshal.TryGetArray<byte>(bufferMsg.Memory, out var sbuffer))
                                {
                                    udpClient.Send(sbuffer.Array, sbuffer.Count);
                                }
                            }
                            finally
                            {
                                bufferMsg.Dispose();
                            }
                        });
            }
            catch (SocketException e)
            {
                if (!manualDisconnecting)
                {
                    OnSocketException(e.SocketErrorCode);
                }
            }
            
        }

        public Task BroadCastSendAsync(ArraySegment<byte> msgBuffer)
        {
            if (msgBuffer.Offset == 0)
            {
                return udpClient.SendAsync(msgBuffer.Array, msgBuffer.Count);
            }

            ///此处几乎用不到，省掉一个async。
            var buffer = new byte[msgBuffer.Count];
            Buffer.BlockCopy(msgBuffer.Array, msgBuffer.Offset, buffer, 0, msgBuffer.Offset);
            return udpClient.SendAsync(buffer, msgBuffer.Count);
        }
    }

    /// 接收
    partial class UDPRemote
    {
        public bool isReceiving = false;
        public override void ReceiveStart()
        {
            if (!Client.Connected || isReceiving)
            {
                return;
            }
            ReceiveAsync(BufferPool.Rent(MaxBufferLength));
        }

        async void ReceiveAsync(IMemoryOwner<byte> buffer)
        {
            if (!Client.Connected || disposedValue)
            {
                return;
            }

            try
            {
                isReceiving = true;
                if (MemoryMarshal.TryGetArray<byte>(buffer.Memory,out var receiveBuffer) )
                {
                    var res = await udpClient.ReceiveAsync(receiveBuffer);
                    LastReceiveTime = DateTime.Now;
                    if (IsVaild)
                    {
                        ///递归，继续接收
                        ReceiveAsync(BufferPool.Rent(MaxBufferLength));
                    }

                    DealMessageAsync(buffer);
                }
                else
                {
                    throw new ArgumentException();
                }
                
            }
            catch (SocketException e)
            {
                if (!manualDisconnecting)
                {
                    OnSocketException(e.SocketErrorCode);
                }
                isReceiving = false;
            }
        }

        private void DealMessageAsync(IMemoryOwner<byte> byteOwner)
        {
            Task.Run(() =>
            {
                Receiver.Receive(byteOwner, this);
            });
        }
    }
}
