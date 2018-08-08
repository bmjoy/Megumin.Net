using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using MMONET.Message;
using ExtraMessage = System.ValueTuple<int?, int?, int?, int?>;

namespace MMONET.Remote
{
    ///可由继承类修改的关键部分
    public abstract partial class RemoteCore
    {


        /// <summary>
        /// 
        /// </summary>
        /// <param name="messageID"></param>
        /// <param name="rpcID"></param>
        /// <param name="extraType"></param>
        /// <param name="extraMessage"></param>
        /// <param name="byteUserMessage"></param>
        /// <returns></returns>
        protected virtual (bool IsContinue, bool SwitchThread, short rpcID, dynamic objectMessage)
            DealBytesMessage(int messageID, short rpcID, byte extraType, ExtraMessage extraMessage, ArraySegment<byte> byteUserMessage)
        {
            if (extraType == 0)
            {
                ///没有外部消息
                return WhenNoExtra(messageID, rpcID, byteUserMessage);
            }

            return default;
        }

        /// <summary>
        /// 当附加消息为空的时候。
        /// </summary>
        /// <param name="messageID"></param>
        /// <param name="rpcID"></param>
        /// <param name="byteUserMessage"></param>
        /// <returns></returns>
        protected virtual (bool IsContinue, bool SwitchThread, short rpcID, dynamic objectMessage)
            WhenNoExtra(int messageID, short rpcID, ArraySegment<byte> byteUserMessage)
        {
            var message = DeserializeMessage(messageID, byteUserMessage);
            return (true, true, rpcID, message);
        }
    }

    /// 用户发送的消息处理
    partial class RemoteCore
    {
        /// <summary>
        /// 序列化消息阶段
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="buffer65536"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        protected virtual (int messageID, ArraySegment<byte> byteUserMessage)
            SerializeMessage<T>(byte[] buffer65536, T message) => MessageLUT.Serialize(buffer65536, message);

        /// <summary>
        /// 反序列化消息阶段
        /// </summary>
        /// <returns></returns>
        protected virtual dynamic DeserializeMessage(int messageID, ArraySegment<byte> byteUserMessage)
            => MessageLUT.Deserialize(messageID, byteUserMessage);
    }

    ///外部消息处理
    partial class RemoteCore
    {
        /// <summary>
        /// 序列化外部附加的消息
        /// </summary>
        /// <param name="buffer65536"></param>
        /// <param name="offset"></param>
        /// <param name="extraMessage"></param>
        /// <returns>返回长度</returns>
        protected virtual ushort SerializeExtraMessage(byte[] buffer65536, int offset, ExtraMessage extraMessage)
        {
            if (extraMessage.Item1 == null)
            {
                buffer65536[offset] = 0;
                return 1;
            }

            return 0;
        }

        /// <summary>
        /// 反序列化外部附加的消息
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        protected virtual (ushort length, byte extraType, ExtraMessage extraMessage) 
            DeserializeExtraMessage(byte[] buffer, int offset)
        {
            byte extraType = buffer[offset];
            switch (extraType)
            {
                case 0:
                    return (1, extraType, default);
                default:
                    break;
            }

            return default;
        }

    }

    ///封包解包处理
    partial class RemoteCore
    {
        /// <summary>
        /// 封装将要发送的字节消息,这个方法控制消息字节的布局。并调用了 SerializeExtraMessage。
        /// </summary>
        /// <param name="messageID"></param>
        /// <param name="rpcID"></param>
        /// <param name="extraMessage"></param>
        /// <param name="byteUserMessage"></param>
        /// <returns></returns>
        protected virtual ArraySegment<byte> PacketBuffer(int messageID, short rpcID, ExtraMessage extraMessage, ArraySegment<byte> byteUserMessage)
        {
            int byteMessageLength = byteUserMessage.Count;
            ///申请发送用 buffer ((框架约定1)发送字节数组发送完成后由发送逻辑回收)         额外信息的最大长度17
            var sendbuffer = BufferPool.Pop(byteMessageLength + FrameworkConst.HeaderOffset + 17);


            ushort offset = FrameworkConst.HeaderOffset;
            var forwardPartLength = offset + SerializeExtraMessage(sendbuffer, offset, extraMessage);

            ///拷贝消息正文
            Buffer.BlockCopy(byteUserMessage.Array, byteUserMessage.Offset, sendbuffer, forwardPartLength, byteUserMessage.Count);

            ushort totolLength = (ushort)(offset + byteMessageLength);

            ///写入报头
            totolLength.WriteToByte(sendbuffer, 0);
            messageID.WriteToByte(sendbuffer, 2);
            rpcID.WriteToByte(sendbuffer, 2 + 4);

            return new ArraySegment<byte>(sendbuffer, 0, totolLength);
        }

        /// <summary>
        /// 解包。 这个方法解析消息字节的布局。并调用了 DeserializeExtraMessage。
        /// <para> 和 <see cref="PacketBuffer(int, short, ExtraMessage, ArraySegment{byte})"/> 对应</para>
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
        protected virtual (int messageID, short rpcID, byte extraType, ExtraMessage extraMessage, ArraySegment<byte> byteUserMessage)
            UnPacketBuffer(ArraySegment<byte> buffer)
        {
            int offset = buffer.Offset;
            ushort totalLength = buffer.Array.ReadUShort(offset);
            offset += 2;

            int messageID = buffer.Array.ReadInt(offset);
            offset += 4;

            short rpcID = buffer.Array.ReadShort(offset);
            offset += 2;

            var (length, extratype, extraMessage) = DeserializeExtraMessage(buffer.Array, offset);
            offset += length;

            return (messageID, rpcID, extratype, extraMessage, new ArraySegment<byte>(buffer.Array, offset, totalLength - offset));
        }
    }
}
