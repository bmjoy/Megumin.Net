using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MMONET.Remote;
using System.Threading.Tasks;
using System.Threading;
using System.Net;
using Network.Remote;

namespace UnitFunc
{
    [TestClass]
    public class UnitTestUDPRemote
    {
        [TestMethod]
        public async void TestConnect()
        {
            IRemoteListener<UDPRemote> listener = new UDPRemoteListener(54321);

            IRemote remote = new UDPRemote();
            ThreadPool.QueueUserWorkItem(state =>
            {
                Thread.Sleep(1000);
                remote.ConnectAsync(new System.Net.IPEndPoint(IPAddress.Loopback, 54321));
            });
            IRemote remoteServer = await listener.ListenAsync();
            Assert.AreEqual(true, remoteServer.Connected);
            Assert.AreEqual(true, remote.Connected);
        }
    }
}
