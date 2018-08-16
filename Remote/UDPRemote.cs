using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MMONET.Message;
using Network.Remote;

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
                    var (Size, MessageID, RpcID) = ReadPacketHeader(recv.Buffer);
                    if (MessageID == FrameworkConst.UdpConnectMessageID)
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

            BufferPool.Push(buffer);
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

                    BufferPool.Push(buffer);
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
            var bf = BufferPool.Pop(32);
            ((ushort)25).WriteTo(bf);
            FrameworkConst.UdpConnectMessageID.WriteTo(bf.AsSpan(2));
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
        protected override void SendByteBufferAsync(IMemoryOwner<byte> bufferMsg)
        {
            try
            {
                Task.Run(() =>
                        {
                            if (MemoryMarshal.TryGetArray<byte>(bufferMsg.Memory,out var sbuffer))
                            {
                                udpClient.Send(sbuffer.Array,sbuffer.Offset);
                            }
                            else
                            {
                                throw new Exception();
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
        protected override void ReceiveStart()
        {
            if (!Client.Connected || isReceiving)
            {
                return;
            }
            ReceiveAsync(new ArraySegment<byte>(BufferPool.Pop(MaxBufferLength), 0, MaxBufferLength));
        }

        async void ReceiveAsync(ArraySegment<byte> buffer)
        {
            if (!Client.Connected || disposedValue)
            {
                return;
            }

            try
            {
                isReceiving = true;
                var res = await udpClient.ReceiveAsync(buffer);
                LastReceiveTime = DateTime.Now;
                if (IsVaild)
                {
                    ///递归，继续接收
                    ReceiveAsync(new ArraySegment<byte>(BufferPool.Pop(MaxBufferLength), 0, MaxBufferLength));
                }

                DealMessageAsync(res.Buffer);
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

        private void DealMessageAsync(ArraySegment<byte> byteMessage)
        {
            Task.Run(() =>
            {
                //解包
                var unpackedMessage = UnPacketBuffer(byteMessage);

                ///处理字节消息
                (bool IsContinue, bool SwitchThread, short rpcID, dynamic objectMessage)
                    = DealBytesMessage(unpackedMessage.messageID, unpackedMessage.rpcID,
                                        unpackedMessage.extraType, unpackedMessage.extraMessage,
                                        unpackedMessage.byteUserMessage);

                DealObjectMessage(IsContinue, SwitchThread, rpcID, objectMessage);

                ///回收池对象
                BufferPool.Push(byteMessage.Array);
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="messageID"></param>
        /// <param name="rpcID"></param>
        /// <param name="byteUserMessage"></param>
        /// <returns></returns>
        protected override (bool IsContinue, bool SwitchThread, short rpcID, dynamic objectMessage) 
            WhenNoExtra(int messageID, short rpcID, ReadOnlyMemory<byte> byteUserMessage)
        {
            if (messageID == FrameworkConst.HeartbeatsMessageID)
            {
                ///拦截框架心跳包
                
                //todo


                return (false, false, default, default);
            }
            else
            {
                return base.WhenNoExtra(messageID, rpcID, byteUserMessage);
            }
        }
    }

    public class UdpConnectMessage
    {
        static UdpConnectMessage()
        {
            MessageLUT.AddFormatter<UdpConnectMessage>(FrameworkConst.UdpConnectMessageID,
                Serialize, Deserialize);
        }

        public int SYN;
        public int ACT;
        public int seq;
        public int ack;
        static UdpConnectMessage Deserialize(ReadOnlyMemory<byte> buffer)
        {
            int SYN = buffer.Span.ReadInt();
            int ACT = buffer.Span.Slice(4).ReadInt();
            int seq = buffer.Span.Slice(8).ReadInt();
            int ack = buffer.Span.Slice(12).ReadInt();
            return new UdpConnectMessage() { SYN = SYN, ACT = ACT, seq = seq, ack = ack };
        }

        static ushort Serialize(UdpConnectMessage connectMessage, Span<byte> bf)
        {
            connectMessage.SYN.WriteTo(bf);
            connectMessage.ACT.WriteTo(bf.Slice(4));
            connectMessage.seq.WriteTo(bf.Slice(8));
            connectMessage.ack.WriteTo(bf.Slice(12));
            return 16;
        }

        public void Deconstruct(out int SYN, out int ACT, out int seq, out int ack)
        {
            SYN = this.SYN;
            ACT = this.ACT;
            seq = this.seq;
            ack = this.ack;
        }
    }
}
