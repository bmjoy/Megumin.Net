using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using MMONET.Message;
using static MMONET.Message.MessageLUT;

namespace MMONET.Remote
{
    /// <summary>
    /// <para>TcpChannel内存开销 整体采用内存池优化</para>
    /// <para>发送内存开销 对于TcpChannel实例 动态内存开销，取决于发送速度，内存实时占用为发送数据的1~2倍</para>
    /// <para>                  接收的常驻开销8kb*2,随着接收压力动态调整</para>
    /// </summary>
    public class TCPRemote : RemoteBase, IRemote
    {
        public TCPRemote(Socket socket):base(socket)
        {

        }

        public TCPRemote() : this(new Socket(SocketType.Stream, ProtocolType.Tcp))
        {
            
        }

        #region Receive

        public override int ReceiveBufferSize => RemoteArgs.ReceiveBufferSize;

        protected override void Receive()
        {
            if (Connected)
            {
                if (!IsReceiving)
                {
                    IsReceiving = true;
                    RemoteArgs remoteArgs = RemoteArgs.Pop();
                    Receive(remoteArgs);
                }
            }
        }

        void Receive(RemoteArgs receiveArgs)
        {
            receiveArgs.Completed += TcpReceiveComplete;

            if (!Socket.ReceiveAsync(receiveArgs))
            {
                TcpReceiveComplete(Socket, receiveArgs);
            }
        }

        void TcpReceiveComplete(object sender, SocketAsyncEventArgs e)
        {
            RemoteArgs args = e as RemoteArgs;
            args.Completed -= TcpReceiveComplete;

            if (args.SocketError == SocketError.Success)
            {
                ///本次接收的长度
                int length = args.BytesTransferred;

                if (length <= 0)
                {
                    OnReceiveError(args, SocketError.NoData);
                    return;
                }

                ///上次接收的半包长度
                int alreadyHave = args.Offset;

                //////有效消息长度
                length += alreadyHave;

                ////调试
                //Thread.Sleep(20);

                var (IsHaveMesssag, Residual) = DealTcpPacket(length,args);

                if (IsHaveMesssag)
                {
                    var newArgs = RemoteArgs.Pop();
                    if (Residual.Count > 0)
                    {
                        ///半包复制
                        Buffer.BlockCopy(Residual.Array, Residual.Offset, newArgs.Buffer, 0, Residual.Count);
                    }

                    newArgs.SetBuffer(Residual.Count, RemoteArgs.ReceiveBufferSize - Residual.Count);
                    Receive(newArgs);

                    MessagePool.PushReceivePacket(args,this);
                }
                else
                {
                    ///没有完整包，继续接收
                    ///重设起始位置
                    args.SetBuffer(length, RemoteArgs.ReceiveBufferSize - length);///这一行代码耗费了我8个小时
                    Receive(args);
                    return;
                }
            }
            else
            {
                OnReceiveError(args, args.SocketError);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="length"></param>
        /// <param name="args">todo: describe args parameter on DealTcpPacket</param>
        /// <returns>IsHaveMesssag 是否至少含有一个完整消息。
        /// Residual剩余的半包。
        /// </returns>
        static (bool IsHaveMesssag, ArraySegment<byte> Residual) DealTcpPacket(int length,RemoteArgs args)
        {
            lock (args)
            {
                ///已经完整读取消息包的长度
                int tempoffset = 0;
                bool IsHaveMesssag = false;

                ///长度至少要大于8（2个字节也就是一个报头长度）
                while ((length - tempoffset) > TotalHeaderByteCount)
                {
                    ///取得当前消息包正文的长度
                    var (Size, MessageID, RpcID) = ParsePacketHeader(args.Buffer, tempoffset);

                    if (Size > length - tempoffset - TotalHeaderByteCount)
                    {
                        ///剩余消息长度不是一个完整包
                        break;
                    }
#if DEBUG
                    ///测试 这里可能存在解析错误
                    if (Size == 0)
                    {
                        throw new ArgumentException("Size =================0");
                    }
#endif
                    ///取得消息正文，起始偏移为tempoffset + 报头总长度；
                    ArraySegment<byte> messagePacket = new ArraySegment<byte>(args.Buffer, tempoffset + TotalHeaderByteCount, Size);
                    args.MessagePacket.Enqueue((MessageID, RpcID, messagePacket));
                    IsHaveMesssag = true;
                    ///识别的消息包，移动起始位置
                    tempoffset += (Size + TotalHeaderByteCount);
                }

                return (IsHaveMesssag, new ArraySegment<byte>(args.Buffer, tempoffset, length - tempoffset));
            }
        }

        void OnReceiveError(RemoteArgs args, SocketError socketError)
        {
            ///遇到错误关闭连接
            Socket.Shutdown(SocketShutdown.Both);
            Socket.Close();
            Socket.Dispose();
            onDisConnect?.Invoke(socketError);

            IsReceiving = false;
            args.Push2Pool();
        }

        #endregion

        #region Send

        List<ArraySegment<byte>> sendWaitList = new List<ArraySegment<byte>>(13);
        List<ArraySegment<byte>> dealList = new List<ArraySegment<byte>>(13);

        protected override void SendAsync<T>(short rpcID, T message)
        {
            if (!Connected)
            {
                return;
            }

            var bufferMsg = MessageLUT.Serialize(rpcID, message);

            bool sendNow = false;

            ///如果现在没有发送消息，手动调用发送。
            lock (this)
            {
                sendWaitList.Add(bufferMsg);
                sendNow = IsSending == false;
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
            IsSending = true;
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
                        IsSending = false;
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
                Socket.Dispose();
                onDisConnect?.Invoke(args.SocketError);
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
                IsSending = false;
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
}
