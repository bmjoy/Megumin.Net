using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Network.Remote;

namespace MMONET.Remote
{
    internal interface IDealMessage : ISendMessage
    {
        ValueTask<dynamic> OnReceiveMessage(dynamic message);
        void SendAsync<T>(short rpcID, T message);
        bool TrySetRpcResult(short rpcID, dynamic message);
    }
}
