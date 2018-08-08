using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Network.Remote;

namespace MMONET.Remote
{
    internal interface IDealObjectMessage : ISendMessage
    {
        ValueTask<dynamic> DealObjectMessage(dynamic message);
        void SendAsync<T>(short rpcID, T message);
        bool TrySetRpcResult(short rpcID, dynamic message);
    }
}
