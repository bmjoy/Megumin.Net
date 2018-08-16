using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using MMONET.Message;
using ExtraMessage = System.ValueTuple<int?, int?, int?, int?>;
using static MMONET.Remote.FrameworkConst;
using System.Buffers;
using System.Buffers.Binary;

namespace MMONET.Remote
{
    ///可由继承类修改的关键部分
    public abstract partial class RemoteCore
    {
        /// <summary>
        /// 处理收到的字节消息，这个时候ExtraMessage 已被反序列化完成，返回值决定是否继续执行下一步消息处理。
        /// 在这个方法中反序列化byteUserMessage -> objectMessage
        /// </summary>
        /// <param name="messageID"></param>
        /// <param name="rpcID"></param>
        /// <param name="extraType"></param>
        /// <param name="extraMessage"></param>
        /// <param name="byteUserMessage"></param>
        /// <returns></returns>
        protected virtual (bool IsContinue, bool SwitchThread, short rpcID, dynamic objectMessage)
            DealBytesMessage(int messageID, short rpcID, byte extraType, ExtraMessage extraMessage, ReadOnlyMemory<byte> byteUserMessage)
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
            WhenNoExtra(int messageID, short rpcID, ReadOnlyMemory<byte> byteUserMessage)
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
        /// <param name="buffer16384"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        protected virtual (int messageID, ushort length)
            SerializeMessage<T>(Span<byte> buffer16384, T message) => MessageLUT.Serialize(buffer16384, message);

        /// <summary>
        /// 反序列化消息阶段
        /// </summary>
        /// <returns></returns>
        protected virtual dynamic DeserializeMessage(int messageID, ReadOnlyMemory<byte> byteUserMessage)
            => MessageLUT.Deserialize(messageID, byteUserMessage);
    }

    ///外部消息处理
    partial class RemoteCore
    {
        /// <summary>
        /// 序列化外部附加的消息
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="extraMessage"></param>
        /// <returns>返回长度</returns>
        protected virtual ushort SerializeExtraMessage(Span<byte> buffer, ExtraMessage extraMessage)
        {
            int offset = 0;
            if (extraMessage.Item1 == null)
            {
                buffer[offset] = 0;
                return 1;
            }

            return 0;
        }

        /// <summary>
        /// 反序列化外部附加的消息
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
        protected virtual (ushort length, byte extraType, ExtraMessage extraMessage) 
            DeserializeExtraMessage(ReadOnlySpan<byte> buffer)
        {
            if (buffer.IsEmpty)
            {
                return default;
            }

            byte extraType = buffer[0];
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
        /// <para>框架使用的字节布局 2总长度 + 4消息ID + 2RpcID + (ExtraMessageByte) + UserMessageByte</para>
        /// </summary>
        /// <param name="messageID"></param>
        /// <param name="rpcID"></param>
        /// <param name="extraMessage"></param>
        /// <param name="byteUserMessage"></param>
        /// <returns>框架使用BigEndian</returns>
        protected virtual IMemoryOwner<byte> PacketBuffer(int messageID, short rpcID, ExtraMessage extraMessage, Span<byte> byteUserMessage)
        {
            Span<byte> extrabyte = stackalloc byte[17];

            ///序列化额外附加信息
            var extralenght = SerializeExtraMessage(extrabyte, extraMessage);
            ushort totolLength = (ushort)(FrameworkConst.HeaderOffset + extralenght + byteUserMessage.Length);
 
            ///申请发送用 buffer ((框架约定1)发送字节数组发送完成后由发送逻辑回收)         额外信息的最大长度17
            var sendbuffer2 = MemoryPool<byte>.Shared.Rent(totolLength);

            ///写入报头 大端字节序写入
            BinaryPrimitives.WriteUInt16BigEndian(sendbuffer2.Memory.Span, totolLength);
            BinaryPrimitives.WriteInt32BigEndian(sendbuffer2.Memory.Span.Slice(2), messageID);
            BinaryPrimitives.WriteInt16BigEndian(sendbuffer2.Memory.Span.Slice(6), rpcID);

            ///拷贝额外消息
            extrabyte.CopyTo(sendbuffer2.Memory.Span.Slice(FrameworkConst.HeaderOffset));
            ///拷贝消息正文
            byteUserMessage.CopyTo(sendbuffer2.Memory.Span.Slice(FrameworkConst.HeaderOffset + extralenght));

            return sendbuffer2;
        }

        /// <summary>
        /// 解包。 这个方法解析消息字节的布局。并调用了 DeserializeExtraMessage。
        /// <para> 和 <see cref="PacketBuffer(int, short, ExtraMessage, Span{byte})"/> 对应</para>
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
        protected virtual (int messageID, short rpcID, byte extraType, ExtraMessage extraMessage, ReadOnlyMemory<byte> byteUserMessage)
            UnPacketBuffer(ReadOnlyMemory<byte> buffer)
        {
            var (totalLenght, messageID, rpcID) = ReadPacketHeader(buffer.Span);

            var (length, extratype, extraMessage) = DeserializeExtraMessage(buffer.Span.Slice(FrameworkConst.HeaderOffset));

            return (messageID, rpcID, extratype, extraMessage, buffer.Slice(FrameworkConst.HeaderOffset + length));
        }

        /// <summary>
        /// 解析报头 (长度至少要大于8（8个字节也就是一个报头长度）)
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException">数据长度小于报头长度</exception>
        public virtual (ushort totalLenght, int messageID, short rpcID)
            ReadPacketHeader(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length >= HeaderOffset)
            {
                ushort size = BinaryPrimitives.ReadUInt16BigEndian(buffer);

                int messageID = BinaryPrimitives.ReadInt32BigEndian(buffer.Slice(2));

                short rpcID = BinaryPrimitives.ReadInt16BigEndian(buffer.Slice(6));

                return (size, messageID, rpcID);
            }
            else
            {
                throw new ArgumentOutOfRangeException("数据长度小于报头长度");
            }
        }
    }
}
