using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using Network.Remote;

namespace MMONET.Remote
{
    #region Common

    internal partial class RemoteArgs
    {
        static int poolKeepCount = 200;
        static ConcurrentQueue<RemoteArgs> pool = new ConcurrentQueue<RemoteArgs>();
        static int newArgs = 0;

        internal static RemoteArgs Pop()
        {
            if (pool.TryDequeue(out RemoteArgs res))
            {
                return res;
            }
            else
            {
                res = new RemoteArgs();
                res.SetBuffer(BufferPool.Pop(ReceiveBufferSize), 0, ReceiveBufferSize);
                newArgs++;

                return res;
            }
        }

        public void Push2Pool()
        {
            if (pool.Count < poolKeepCount + newArgs / 3 - 5)
            {
                SetBuffer(0, ReceiveBufferSize);
                MessagePacket.Clear();
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

    internal partial class RemoteArgs : SocketAsyncEventArgs, IReceivedPacket
    {
        /// <summary>
        /// 接收缓冲区
        /// </summary>
        public const int ReceiveBufferSize = 8192;

        public Queue<(int messageID, short rpcID, ArraySegment<byte> body)> MessagePacket { get; }
            = new Queue<(int messageID, short rpcID, ArraySegment<byte> body)>(13);

    }

    #endregion
}