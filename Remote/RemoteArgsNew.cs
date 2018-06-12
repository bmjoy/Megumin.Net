using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using static MMONET.Sockets.Remote;

namespace MMONET.Sockets
{
    #region Common

    internal partial class RemoteArgsNew
    {
        static int poolKeepCount = 200;
        static ConcurrentQueue<RemoteArgsNew> pool = new ConcurrentQueue<RemoteArgsNew>();
        static int newArgs = 0;

        internal static RemoteArgsNew Pop()
        {
            if (pool.TryDequeue(out RemoteArgsNew res))
            {
                return res;
            }
            else
            {
                res = new RemoteArgsNew();
                res.SetBuffer(BufferPool.Pop(ReceiveBufferSize), 0, ReceiveBufferSize);
                newArgs++;

                return res;
            }
        }


        public RemoteChannal Channal { get; internal set; }

        public void Push2Pool()
        {
            if (pool.Count < poolKeepCount + newArgs / 3 - 5)
            {
                SetBuffer(0, ReceiveBufferSize);
                MessagePacket.Clear();
                Channal = RemoteChannal.None;
                pool.Enqueue(this);
            }
            else
            {
                newArgs--;
                ///args 舍弃 buffer 回收
                BufferPool.Push(Buffer);
            }
        }
    }

    #endregion


    #region TcpReceive

    internal partial class RemoteArgsNew : SocketAsyncEventArgs, IReceivedPacket
    {
        /// <summary>
        /// 接收缓冲区
        /// </summary>
        public const int ReceiveBufferSize = 8192;

        public Queue<(int messageID, short rpcID, ArraySegment<byte> body)> MessagePacket { get; }
            = new Queue<(int messageID, short rpcID, ArraySegment<byte> body)>(13);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="length"></param>
        /// <returns>IsHaveMesssag 是否至少含有一个完整消息。
        /// Residual剩余的半包。
        /// </returns>
        public (bool IsHaveMesssag, ArraySegment<byte> Residual) DealTcpPacket(int length)
        {
            lock (this)
            {
                ///已经完整读取消息包的长度
                int tempoffset = 0;
                bool IsHaveMesssag = false;

                ///长度至少要大于8（2个字节也就是一个报头长度）
                while ((length - tempoffset) > TotalHeaderByteCount)
                {
                    ///取得当前消息包正文的长度
                    var (Size, MessageID, RpcID) = ParsePacketHeader(Buffer, tempoffset);

                    ///测试
                    if (Size == 0)
                    {
                        throw new ArgumentException("Size =================0");
                    }

                     ///取得消息正文，起始偏移为tempoffset + 报头总长度；
                    ArraySegment<byte> messagePacket = new ArraySegment<byte>(Buffer, tempoffset + TotalHeaderByteCount, Size);
                    MessagePacket.Enqueue((MessageID, RpcID, messagePacket));
                    IsHaveMesssag = true;
                    ///识别的消息包，移动起始位置
                    tempoffset += (Size + TotalHeaderByteCount);
                }

                return (IsHaveMesssag, new ArraySegment<byte>(Buffer, tempoffset, length - tempoffset));
            }
        }
    }

    #endregion


    #region BroadCast

    internal partial class RemoteBroadCastArgs : SocketAsyncEventArgs
    {
        internal static RemoteBroadCastArgs Pop()
        {
            throw new NotImplementedException();
        }
    }

    #endregion
}