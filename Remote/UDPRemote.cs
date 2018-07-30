using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MMONET.Message;
using Network.Remote;
using static MMONET.Message.MessageLUT;

namespace MMONET.Remote
{
    /// <summary>
    /// 不支持多播地址
    /// </summary>
    public partial class UDPRemote : UdpClient, IRemote, IDealMessage
    {
        public int InstanceID { get; set; }
        public bool Connected => this.Client.Connected;
        public Socket Socket => this.Client;
        public bool IsVaild { get; }

        public UDPRemote():base(0, AddressFamily.InterNetworkV6)
        {
        }

        #region RPC

        public IRpcCallbackPool RpcCallbackPool { get; } = new RpcCallbackPool(31);

        #endregion

        #region Send

        public bool IsSending { get; protected set; }

        public virtual Task<(RpcResult result, Exception exception)> RpcSendAsync<RpcResult>(dynamic message)
        {
            if (!IsReceiving)
            {
                Receive(null);
            }

            var (rpcID, source) = RpcCallbackPool.Regist<RpcResult>();

            SendAsync(rpcID, message);

            return source;
        }


        /// <summary>
        /// <see cref="IRpcSendMessage.SafeRpcSendAsync{RpcResult}(dynamic, Action{Exception})"/>
        /// </summary>
        /// <typeparam name="RpcResult"></typeparam>
        /// <param name="message"></param>
        /// <param name="OnException"></param>
        /// <returns></returns>
        public virtual ICanAwaitable<RpcResult> SafeRpcSendAsync<RpcResult>(dynamic message, Action<Exception> OnException = null)
        {
            if (!IsReceiving)
            {
                Receive(null);
            }

            var (rpcID, source) = RpcCallbackPool.Regist<RpcResult>(OnException);

            SendAsync(rpcID, message);

            return source;
        }

        public virtual void SendAsync<T>(T message)
        {
            SendAsync(0, message);
        }

        /// <summary>
        /// 异步发送
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="rpcID"></param>
        /// <param name="message"></param>
        Exception SendAsync<T>(short rpcID, T message)
        {
            if (!Connected)
            {
                return null;
            }

            var bufferMsg = MessageLUT.Serialize(rpcID, message);

            Task.Run(async () =>
            {
                var s = await SendAsync(bufferMsg.Array, bufferMsg.Count);
                BufferPool.Push(bufferMsg.Array);
            });

            return null;
        }

        Exception IDealMessage.SendAsync<T>(short rpcID, T message) => SendAsync(rpcID, message);
        #endregion

        #region Receive

        public int ReceiveBufferSize { get; }
        public bool IsReceiving { get; private set; }
        /// <summary>
        /// 是否将接收消息回调切换到指定线程<seealso cref="MainThreadScheduler"/>
        /// </summary>
        public bool SwitchThread { get; set; } = true;
        /// <summary>
        /// 接受消息的回调函数
        /// </summary>
        protected OnReceiveMessage onReceive;
        OnReceiveMessage IDealMessage.OnReceive => onReceive;

        public void Receive(OnReceiveMessage onReceive)
        {
            this.onReceive = onReceive;

            if (!IsReceiving)
            {
                MyReceiveAsync();
            }
        }

        async void MyReceiveAsync()
        {
            if (!IsReceiving)
            {
                ///绑定远端，防止UDP流量攻击
                //if (!Connected)
                //{
                //    Connect(ConnectIPEndPoint);
                //}
            }

            IsReceiving = true;

            var res = await ReceiveAsync();
            LastReceiveTime = DateTime.Now;
            if (IsVaild)
            {
                MyReceiveAsync();
            }

            ParseBuffer(res);
        }

        private void ParseBuffer(UdpReceiveResult result)
        {
            var (Size, MessageID, RpcID) = ParsePacketHeader(result.Buffer, 0);
            if (MessageID == FrameworkConst.HeartbeatsMessageID)
            {
                ///拦截框架心跳包
                //todo
            }
            else
            {
                ///推入消息池
                MessagePool.PushReceivePacket(MessageID, RpcID,
                    new ArraySegment<byte>(result.Buffer, TotalHeaderByteCount, Size), this, SwitchThread);
            }
        }

        #endregion

        public event Action<SocketError> OnDisConnect;

        public void Disconnect(bool triggerOnDisConnectEvent = true)
        {
            throw new NotImplementedException();
        }

        public void ReceiveCallback(int messageID, short rpcID, dynamic msg)
        {
            throw new NotImplementedException();
        }

        public int Guid { get; } = InterlockedID<IRemote>.NewID();

        bool isConnecting = false;
        
        public async Task<Exception> ConnectAsync(IPEndPoint endPoint, int retryCount = 0)
        {
            if (isConnecting)
            {
                return new Exception("连接正在进行中");
            }
            isConnecting = true;
            this.ConnectIPEndPoint = endPoint;
            while (retryCount >= 0)
            {
                try
                {
                    var res = await this.Connect();
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

        public IPEndPoint ConnectIPEndPoint { get; set; }
        public EndPoint RemappedEndPoint => Socket.RemoteEndPoint;
        public DateTime LastReceiveTime { get; private set; }

        public Task BroadCastSendAsync(ArraySegment<byte> msgBuffer)
        {
            return this.SendAsync(msgBuffer.Array, msgBuffer.Count);
        }
    }

    partial class UDPRemote
    {
        int lastseq;
        int lastack;
        async Task<bool> Connect()
        {
            lastseq = new Random().Next(0, 10000);
            var buffer =  MakeUDPConnectMessage(1, 0, lastseq, lastack);
            CancellationTokenSource source = new CancellationTokenSource();
            TaskCompletionSource<bool> taskCompletion = new TaskCompletionSource<bool>();

#pragma warning disable CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法
            
            Task.Run(async () =>
            {
                while (true)
                {
                    //var rf = new IPEndPoint(IPAddress.IPv6Any, 0);
                    //var res = base.Receive(ref rf);
                    //res.ToString();
                    var recv = await ReceiveAsync();
                    var (Size, MessageID, RpcID) = ParsePacketHeader(recv.Buffer, 0);
                    if (MessageID == FrameworkConst.UdpConnectMessageID)
                    {
                        var (SYN, ACK, seq, ack) = ReadConnectMessage(recv.Buffer);
                        if (SYN == 1 && ACK == 1 && lastseq + 1 == ack)
                        {
                            ///ESTABLISHED

                            Connect(recv.RemoteEndPoint);
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
                    await SendAsync(buffer, buffer.Length,ConnectIPEndPoint);
                    await Task.Delay(1000);
                }
            },source.Token);

            Task.Run(async () => 
            {
                await Task.Delay(5000, source.Token);
                ///一段时间没有反应，默认失败。
                source.Cancel();
                taskCompletion.TrySetException(new TimeoutException());

            },source.Token);


#pragma warning restore CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法

            BufferPool.Push(buffer);
            return await taskCompletion.Task;
        }

        internal async Task<bool> TryAccept(UdpReceiveResult udpReceive)
        {
            if (Connected && this.Client.RemoteEndPoint.Equals(udpReceive.RemoteEndPoint))
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
                base.Connect(udpReceive.RemoteEndPoint);
                var buffer =  MakeUDPConnectMessage(1, 1, lastack, seq + 1);

#pragma warning disable CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法
                Task.Run(async ()=>
                {
                    for (int i = 0; i < 3; i++)
                    {
                        await SendAsync(buffer, buffer.Length);
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

        static (int SYN,int ACK,int seq,int ack) ReadConnectMessage(byte[] buffer)
        {
            int SYN = BitConverter.ToInt32(buffer, TotalHeaderByteCount);
            int ACK = BitConverter.ToInt32(buffer, TotalHeaderByteCount + 4);
            int seq = BitConverter.ToInt32(buffer, TotalHeaderByteCount + 8);
            int ack = BitConverter.ToInt32(buffer, TotalHeaderByteCount + 12);
            return (SYN, ACK, seq, ack);
        }

        static byte[] MakeUDPConnectMessage(int SYN, int ACT, int seq, int ack)
        {
            var bf = BufferPool.Pop(32);
            MakePacket(16, FrameworkConst.UdpConnectMessageID, 0, bf);
            BitConverter.GetBytes(SYN).CopyTo(bf, TotalHeaderByteCount);
            BitConverter.GetBytes(ACT).CopyTo(bf, TotalHeaderByteCount + 4);
            BitConverter.GetBytes(seq).CopyTo(bf, TotalHeaderByteCount + 8);
            BitConverter.GetBytes(ack).CopyTo(bf, TotalHeaderByteCount + 12);
            return bf;
        }
    }

    enum UDPConnectState
    {
        CLOSED,
        LISTEN,
        SYN_SENT,
        SYN_RCVD,
        ESTABLISHED,
        INVALID,
    }
}
