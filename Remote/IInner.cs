using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Network.Remote;

namespace MMONET.Remote
{
    internal interface IDealMessage : ISendMessage
    {
        ValueTask<object> OnReceiveMessage(object message);
        void SendAsync<T>(short rpcID, T message);
        bool TrySetRpcResult(short rpcID, object message);
    }
}
