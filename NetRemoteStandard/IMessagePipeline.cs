using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Buffers;
using System.Buffers.Binary;
using System.Threading.Tasks;
using Net.Remote;

namespace Megumin.Message
{
    public interface ITcpPacker
    {
        /// <summary>
        /// 处理粘包，将分好的包放入list中。这里产生一次数据拷贝。
        /// </summary>
        /// <param name="source"></param>
        /// <param name="pushCompleteMessage"></param>
        /// <returns>返回剩余部分</returns>
        ReadOnlySpan<byte> CutOff(ReadOnlySpan<byte> source, IList<IMemoryOwner<byte>> pushCompleteMessage);
    }

    public interface IMessagePipeline:ITcpPacker
    {
        void Push<T>(IMemoryOwner<byte> byteMessage, T remote)
            where T:ISendMessage,IRemoteID,IUID,IObjectMessageReceiver;
        IMemoryOwner<byte> Packet(int rpcID, object message);
        IMemoryOwner<byte> Packet(int rpcID, object message, int identifier);
        IMemoryOwner<byte> Packet(int rpcID, object message, ReadOnlySpan<byte> extraMessage);
    }

    public interface IObjectMessageReceiver
    {
        ValueTask<object> Deal(int rpcID, object message);
    }

    public interface IDeserializeHandle
    {
        (int rpcID, object message) DeserializeMessage(int messageID,in ReadOnlyMemory<byte> messageBody);
    }
}
