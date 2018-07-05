using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using MMONET.Remote;

namespace MMONET.DCS
{
    public partial class DCSContainer
    {
        public readonly static DCSContainer Instance = new DCSContainer();

        public static IContainer MainContainer;

        int GUID = 0;
        private async Task<int> GetNewSeviceIDAsync()
        {
            return GUID++;
        }

        List<IPAddress> iPAddresses = new List<IPAddress>();
        Dictionary<int, IService> serviceDic = new Dictionary<int, IService>();
        public async void AddService(IService service)
        {
            service.GUID = await GetNewSeviceIDAsync();
            serviceDic.Add(service.GUID, service);
            service.Start();
            //await Sockets.BroadCastAsync(new Login(), MainContainer.Sockets);
        }

        public void Init()
        {
            iPAddresses.Add(IPAddress.IPv6Loopback);
        }

        public async Task Start()
        {
            IPAddress my = Remote.Address;
            if (my == MainIP)
            {
                //if (CheckSocketPort(MainPort))
                //{
                //    ///本机第一个进程
                //    Sockets.StartListen(MainPort);
                //    //Sockets.StopListen(MainPort);
                //}
                //else
                //{
                //    ///本机其他进程，尝试分布间通讯
                //    var ex = await Sockets.ConnectAsync(MainIP,MainPort);
                //    if (ex == null)
                //    {
                //        ///成功连接，开始注册
                //        ///
                //        await Sockets.Send<TestMessage>(new byte[10]);
                //    }
                //}
            }
        }

        /// <summary>
        /// 检测端口是否可用，TCP,UDP同时可用返回true
        /// </summary>
        /// <param name="port"></param>
        /// <returns></returns>
        private bool CheckSocketPort(int port)
        {
            var ipProperties = IPGlobalProperties.GetIPGlobalProperties();

            var tcpPort = ipProperties.GetActiveTcpListeners().FirstOrDefault(p => p.Port == port);
            if (tcpPort != null)
            {
                return false;
            }

            var udpPort = ipProperties.GetActiveUdpListeners().FirstOrDefault(p => p.Port == port);
            if (udpPort != null)
            {
                return false;
            }
            return true;
        }

        private DCSContainer() { }

        public Remote.IRemote Remote { get; private set; } = new TCPRemote();
        /// <summary>
        /// 起始端口
        /// </summary>
        public int MainPort { get; private set; } = 54321;
        /// <summary>
        /// 分布式中第一个默认IP
        /// </summary>
        public IPAddress MainIP { get; private set; } = IPAddress.IPv6Loopback;
    }


    public class TestMessage
    {

    }

    public class TestMessage2
    {

    }
}
