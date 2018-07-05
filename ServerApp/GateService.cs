using System;
using System.Threading.Tasks;
using Message;
using MMONET.DCS;
using MMONET.Sockets;

namespace ServerApp
{
    internal class GateService : IService
    {
        public int GUID { get; set; }

        TCPRemoteListener listener = new TCPRemoteListener(Config.MainPort);

        public void Start()
        {
            StartListenAsync();
        }

        public async void StartListenAsync()
        {
            var remote = await listener.ListenAsync();
            Console.WriteLine($"建立连接");
            StartListenAsync();
            remote.Receive(OnReceiveAsync);
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