using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using MMONET.Message;
using Network.Remote;
using ExtraMessage = System.ValueTuple<int?, int?, int?, int?>;

namespace MMONET.Remote
{
    public abstract partial class RemoteBase:RemoteCore,IDealObjectMessage
    {
        protected void DealObjectMessage(bool IsContinue, bool SwitchThread, short rpcID, dynamic objectMessage)
        {
            if (IsContinue)
            {
                ///处理实例消息
                MessageThreadTransducer.PushReceivePacket(rpcID, objectMessage, this, SwitchThread);
            }
        }

        ValueTask<dynamic> IDealObjectMessage.DealObjectMessage(dynamic message)
        {
            throw new NotImplementedException();
        }

        void IDealObjectMessage.SendAsync<T>(short rpcID, T message)
        {
            throw new NotImplementedException();
        }

        bool IDealObjectMessage.TrySetRpcResult(short rpcID, dynamic message)
        {
            throw new NotImplementedException();
        }

        void ISendMessage.SendAsync<T>(T message)
        {
            throw new NotImplementedException();
        }
    }
}
