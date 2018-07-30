using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MMONET;

namespace UnitFunc
{
    [TestClass]
    public class UnitTestUDPClient
    {
        [TestMethod]
        public void TestMethod1()
        {
            UdpClient udp = new UdpClient(61111, AddressFamily.InterNetworkV6);
            ThreadPool.QueueUserWorkItem(async state => 
            {
                var res = await udp.ReceiveAsync();
                res.ToString();
                udp.Send(res.Buffer, 32, res.RemoteEndPoint);
            });
            UdpClient client = new UdpClient( AddressFamily.InterNetworkV6);
            client.Send(BufferPool.Pop(32), 32, new IPEndPoint(IPAddress.IPv6Loopback, 61111));
            IPEndPoint r = new IPEndPoint(IPAddress.None,0);
            var rc = client.Receive(ref r);
            rc.ToString();
            Thread.Sleep(1000);
        }
    }
}
