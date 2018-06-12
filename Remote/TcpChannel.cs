using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using static MMONET.Sockets.Remote;

namespace MMONET.Sockets
{
    internal interface ITcpNeed : IOnReceivePacket
    {
        int RemotePort { get; }
        IPAddress RemoteAddress { get; }

        void OnDisconnect(RemoteChannal channal, SocketError socketError, bool triggerOnDisConnectEvent = true);
    }

    internal partial class TcpChannel : IChannal
    {
        private TcpListener tcpListener;

        public TcpChannel(ITcpNeed host)
        {
            this.Remote = host;
        }

        private TcpChannel(Socket remoteSocket)
        {
            this.Socket = remoteSocket;
        }

        void IChannal.SetRemote(Remote remote)
        {
            this.Remote = remote;
        }

        public Socket Socket { get; private set; }

        public bool Connected
        {
            get
            {
                return Socket?.Connected ?? false;
            }
        }

        public bool IsReceive { get; private set; }

        /// <summary>
        /// 外层Remote
        /// </summary>
        public ITcpNeed Remote { get; private set; }

        public bool IsSend { get; private set; }

        bool isConnecting = false;
        public async Task<Exception> ConnectAsync()
        {
            if (isConnecting)
            {
                return new Exception("连接正在进行中");
            }

            isConnecting = true;
            Exception exception = null;
            await Task.Run(() =>
            {
                try
                {
                    Socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                    Socket.Connect(Remote.RemoteAddress, Remote.RemotePort);
                }
                catch (Exception e)
                {
                    exception = e;
                }
            });

            isConnecting = false;
            return exception;
        }

        public void Disconnect(bool triggerOnDisConnectEvent)
        {
            if (Connected)
            {
                Socket.Shutdown(SocketShutdown.Both);
                Socket.Disconnect(false);
                Remote.OnDisconnect(RemoteChannal.TCP,SocketError.Disconnecting,triggerOnDisConnectEvent);
            }
        }



        public void Dispose()
        {
            throw new System.NotImplementedException();
        }

        public async Task<IChannal> ListenAsync(int mainPort)
        {
            if (tcpListener == null)
            {
                ///同时支持IPv4和IPv6
                tcpListener = TcpListener.Create(mainPort);

                tcpListener.AllowNatTraversal(true);
                tcpListener.Start();
            }

            var remoteSocket = await tcpListener.AcceptSocketAsync();
            TcpChannel channel = new TcpChannel(remoteSocket);
            return channel;
        }



        #region Send

        List<ArraySegment<byte>> sendWaitList = new List<ArraySegment<byte>>(13);
        List<ArraySegment<byte>> dealList = new List<ArraySegment<byte>>(13);

        public void SendAsync<T>(short rpcID, T message)
        {
            if (!Connected)
            {
                return;
            }

            var bufferMsg = MMONET.Sockets.Remote.Serialize(rpcID, message);

            bool sendNow = false;

            ///如果现在没有发送消息，手动调用发送。
            lock (this)
            {
                sendWaitList.Add(bufferMsg);
                sendNow = IsSend == false;
                if (sendNow)
                {
                    ///交换队列放在这里而不是Send函数是为了加锁。
                    ///交换等待发送队列
                    var temp = dealList;
                    dealList = sendWaitList;
                    sendWaitList = temp;
                }
            }

            if (sendNow)
            {
                Send();
            }
        }

        protected SocketAsyncEventArgs sendArgs;
        void Send()
        {
            IsSend = true;
            if (sendArgs == null)
            {
                sendArgs = new SocketAsyncEventArgs();
                sendArgs.Completed += SendComplete;
            }

            sendArgs.BufferList = dealList;

            if (!Socket.SendAsync(sendArgs))
            {
                SendComplete(Socket, sendArgs);
            }
        }

        void SendComplete(object sender, SocketAsyncEventArgs args)
        {
            ///这个方法由其他线程调用
            
            if (args.SocketError == SocketError.Success)
            {
                //Console.WriteLine("发送完成");

                ///如果发送完成时，待发送队列还有消息，继续发送
                ///否则，标记发送停止
                bool continueSent = false;
                lock (this)
                {
                    ///归还buffer,重置BufferList
                    foreach (var item in args.BufferList)
                    {
                        BufferPool.Push(item.Array);
                    }
                    args.BufferList.Clear();
                    args.BufferList = null;

                    if (sendWaitList.Count > 0)
                    {
                        ///交换等待发送队列
                        var temp = dealList;
                        dealList = sendWaitList;
                        sendWaitList = temp;

                        continueSent = true;
                    }
                    else
                    {
                        IsSend = false;
                    }
                }
                if (continueSent)
                {
                    Send();
                }
            }
            else
            {
                ///遇到错误关闭连接
                Socket.Shutdown(SocketShutdown.Both);
                Socket.Close();
                Remote.OnDisconnect(RemoteChannal.TCP, args.SocketError);
                foreach (var item in sendWaitList)
                {
                    BufferPool.Push(item.Array);
                }
                sendWaitList.Clear();
                foreach (var item in dealList)
                {
                    BufferPool.Push(item.Array);
                }
                dealList.Clear();
                IsSend = false;
            }
        }

        public Task BroadCastSendAsync(ArraySegment<byte> msgBuffer)
        {
            return Task.Run(() =>
            {
                Socket.Send(msgBuffer.Array, msgBuffer.Offset, msgBuffer.Count, SocketFlags.None);
            });
        }



        #endregion
    }

    #region Receive

    partial class TcpChannel
    {
        public void Receive()
        {
            if (Connected)
            {
                if (!IsReceive)
                {
                    IsReceive = true;
                    RemoteArgsNew remoteArgs = RemoteArgsNew.Pop();
                    remoteArgs.Channal = RemoteChannal.TCP;
                    Receive(remoteArgs);
                }
            }
        }

        void Receive(RemoteArgsNew receiveArgs)
        {
            receiveArgs.Completed += TcpReceiveComplete;

            if (!Socket.ReceiveAsync(receiveArgs))
            {
                TcpReceiveComplete(Socket, receiveArgs);
            }
        }

        void TcpReceiveComplete(object sender, SocketAsyncEventArgs e)
        {
            RemoteArgsNew args = e as RemoteArgsNew;
            args.Completed -= TcpReceiveComplete;

            if (args.SocketError == SocketError.Success)
            {
                ///本次接收的长度
                int length = args.BytesTransferred;

                if (length <= 0)
                {
                    OnReceiveError(args,SocketError.NoData);
                    return;
                }

                ///上次接收的半包长度
                int alreadyHave = args.Offset;

                //////有效消息长度
                length += alreadyHave;

                ////调试
                //Thread.Sleep(20);

                var (IsHaveMesssag, Residual) = args.DealTcpPacket(length);

                if (IsHaveMesssag)
                {
                    var newArgs = RemoteArgsNew.Pop();
                    newArgs.Channal = RemoteChannal.TCP;
                    if (Residual.Count > 0)
                    {
                        ///半包复制
                        Buffer.BlockCopy(Residual.Array, Residual.Offset, newArgs.Buffer, 0, Residual.Count);
                    }

                    newArgs.SetBuffer(Residual.Count, RemoteArgsNew.ReceiveBufferSize - Residual.Count);
                    Receive(newArgs);

                    Remote.OnReceive(args);
                }
                else
                {
                    ///没有完整包，继续接收
                    ///重设起始位置
                    args.SetBuffer(length, RemoteArgsNew.ReceiveBufferSize - length);///这一行代码耗费了我8个小时
                    Receive(args);
                    return;
                }
            }
            else
            {
                OnReceiveError(args,args.SocketError);
            }
        }

        void OnReceiveError(RemoteArgsNew args,SocketError socketError)
        {
            ///遇到错误关闭连接
            Socket.Shutdown(SocketShutdown.Both);
            Socket.Close();
            Remote.OnDisconnect(RemoteChannal.TCP, socketError);

            IsReceive = false;
            args.Push2Pool();
        }
    }

    #endregion
}