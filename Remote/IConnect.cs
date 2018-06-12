using System;
using System.Threading.Tasks;

namespace MMONET.Sockets
{
    internal interface IConnect
    {
        bool Connected { get; }

        Task<Exception> ConnectAsync();
        void Disconnect(bool triggerOnDisConnectEvent);
    }
}