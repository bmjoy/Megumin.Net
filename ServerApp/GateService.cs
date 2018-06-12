using System;
using System.Threading.Tasks;
using Message;
using MMONET.DCS;
using MMONET.Sockets;
using MMONET.Sockets.Test;

namespace ServerApp
{
    internal class GateService : IService
    {
        public int GUID { get; set; }

        Remote listener = new Remote();

        public void Start()
        {
            StartListenAsync();
        }

        public async void StartListenAsync()
        {
            Remote remote = await listener.ListenAsync(Config.MainPort);
            Console.WriteLine($"建立连接");
            StartListenAsync();
            remote.ReceiveAsync(OnReceiveAsync);
        }

        private async ValueTask<object> OnReceiveAsync(object message)
        {
            switch (message)
            {
                case Login2Gate login:

                    Console.WriteLine($"客户端登陆请求：{login.Account}-----{login.Password}");

                    Login2GateResult resp = new Login2GateResult();
                    resp.IsSuccess = true;
                    return resp;
                case TestPacket1 packet1:
                    Console.WriteLine($"客户端登陆请求：{packet1}");
                    Remote.BroadCastAsync(packet1, new Remote(), new Remote());
                    break;
                case TestPacket2 packet2:
                    return null;
                default:
                    break;
            }
            return null;
        }

        public void Update(double deltaTime)
        {
            throw new System.NotImplementedException();
        }
    }
}