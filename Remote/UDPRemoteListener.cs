using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Network.Remote;
using static MMONET.Message.MessageLUT;

namespace MMONET.Remote
{
    public class UDPRemoteListener : UdpClient, IRemoteListener<UDPRemote>
    {
        public IPEndPoint IPEndPoint { get; set; }
        EndPoint IEndPoint.OverrideEndPoint { get; }

        public UDPRemoteListener(int port):base(port)
        {
            this.IPEndPoint = new IPEndPoint(IPAddress.None,port);
        }

        public bool IsListening { get; private set; }
        public TaskCompletionSource<UDPRemote> TaskCompletionSource { get; private set; }

        async void AcceptAsync()
        {
            while (IsListening)
            {
                var res = await ReceiveAsync();
                var (Size, MessageID, RpcID) = ParsePacketHeader(res.Buffer, 0);
                if (MessageID == FrameworkConst.UdpConnectMessageID)
                {
                    ReMappingAsync(res);
                }
            }
        }

        /// <summary>
        /// 重映射
        /// </summary>
        /// <param name="res"></param>
        private async void ReMappingAsync(UdpReceiveResult res)
        {
            UDPRemote remoteNew = new UDPRemote();
            var (Result, Complete) = await remoteNew.TryAccept(res).WaitAsync(5000);
            if (Result && Complete)
            {
                ///连接成功
                RemotePool.Add(remoteNew);
                if (TaskCompletionSource == null)
                {
                    connected.Enqueue(remoteNew);
                }
                else
                {
                    TaskCompletionSource.SetResult(remoteNew);
                }
            }
            else
            {
                remoteNew.Dispose();
            }
        }

        ConcurrentQueue<UDPRemote> connected = new ConcurrentQueue<UDPRemote>();

        public async Task<UDPRemote> ListenAsync()
        {
            IsListening = true;
            System.Threading.ThreadPool.QueueUserWorkItem(state =>
            {
                AcceptAsync();
            });

            if (connected.TryDequeue(out var remote))
            {
                if (remote != null)
                {
                    return remote;
                }
            }
            if (TaskCompletionSource == null)
            {
                TaskCompletionSource = new TaskCompletionSource<UDPRemote>();
            }

            var res = await TaskCompletionSource.Task;
            TaskCompletionSource = null;
            return res;
        }

        public void Stop()
        {
            IsListening = false;
        }
    }


    public class TaskEx
    {
        
    }
}
