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
    /// <summary>
    /// IPV4 IPV6 udp中不能混用
    /// </summary>
    public class UDPRemoteListener : UdpClient, IRemoteListener<UDPRemote>
    {
        public IPEndPoint ConnectIPEndPoint { get; set; }
        EndPoint IRemoteEndPoint.RemappedEndPoint { get; }

        public UDPRemoteListener(int port,AddressFamily addressFamily = AddressFamily.InterNetworkV6)
            : base(port, addressFamily)
        {
            this.ConnectIPEndPoint = new IPEndPoint(IPAddress.None,port);
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
        /// 正在连接的
        /// </summary>
        readonly Dictionary<IPEndPoint, UDPRemote> connecting = new Dictionary<IPEndPoint, UDPRemote>();
        /// <summary>
        /// 连接成功的
        /// </summary>
        readonly ConcurrentQueue<UDPRemote> connected = new ConcurrentQueue<UDPRemote>();
        /// <summary>
        /// 重映射
        /// </summary>
        /// <param name="res"></param>
        private async void ReMappingAsync(UdpReceiveResult res)
        {
            if (!connecting.TryGetValue(res.RemoteEndPoint,out var remote))
            {
                remote = new UDPRemote(this.Client.AddressFamily);
                connecting[res.RemoteEndPoint] = remote;

                var (Result, Complete) = await remote.TryAccept(res).WaitAsync(5000);

                if (Complete)
                {
                    ///完成
                    if (Result)
                    {
                        ///连接成功
                        if (TaskCompletionSource == null)
                        {
                            connected.Enqueue(remote);
                        }
                        else
                        {
                            TaskCompletionSource.SetResult(remote);
                        }
                    }
                    else
                    {
                        ///连接失败但没有超时
                        remote.Dispose();
                    }
                }
                else
                {
                    ///超时，手动断开，释放remote;
                    remote.Disconnect();
                    remote.Dispose();
                }
            }
        }



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
}
