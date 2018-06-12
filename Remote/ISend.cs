using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using static MMONET.Sockets.Remote;

namespace MMONET.Sockets
{
    internal interface ISend
    {
        bool IsSend { get; }
        void SendAsync<T>(short rpcID, T message);
        Task BroadCastSendAsync(ArraySegment<byte> msgBuffer);
    }
}
