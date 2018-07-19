using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Network.Remote;


namespace MMONET.Remote
{
    public class UDPRemoteListener : UdpClient, IRemoteListener<UDPRemote>
    {
        public IPEndPoint IPEndPoint { get; set; }
        public UDPRemoteListener(int port):base(port)
        {
            this.IPEndPoint = new IPEndPoint(IPAddress.None,port);


            System.Threading.ThreadPool.QueueUserWorkItem(state =>
            {
                RAsync();
            });
        }

        async void RAsync()
        {
            while (true)
            {
                var res = await ReceiveAsync();
                UDPRemote remoteNew = new UDPRemote();
                remoteNew.TryAccept(res);
            }
        }

        public Task<UDPRemote> ListenAsync()
        {
            return null;

        }
    }
}
