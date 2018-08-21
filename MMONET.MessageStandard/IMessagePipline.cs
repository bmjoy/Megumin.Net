using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using ExtraMessage = System.ValueTuple<int?, int?, int?, int?>;
using System.Buffers;
using System.Buffers.Binary;
using System.Threading.Tasks;

namespace MMONET.Message
{
    public interface IPacker<Remote>
    {
        /// <summary>
        /// 封装消息，然后由框架发送
        /// </summary>
        /// <param name="rpcID"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        IMemoryOwner<byte> Packet<T>(short rpcID, T message, Remote remote);
    }

    public interface ITcpPacker<Remote> : IPacker<Remote>
    {
        /// <summary>
        /// 处理粘包，将分好的包放入list中。这里产生一次数据拷贝。
        /// </summary>
        /// <param name="source"></param>
        /// <param name="pushCompleteMessage"></param>
        /// <returns>返回剩余部分</returns>
        ReadOnlySpan<byte> CutOff(ReadOnlySpan<byte> source, IList<IMemoryOwner<byte>> pushCompleteMessage);
    }

    public interface IReceiver<Remote>
    {
        /// <summary>
        /// 处理收到的消息
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="remote"></param>
        void Receive(IMemoryOwner<byte> packet, Remote remote);
        /// <summary>
        /// 通常用户接收反序列化完毕的消息的函数
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        ValueTask<object> DealMessage(object message);
    }
}
