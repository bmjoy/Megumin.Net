using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using MMONET.Message.TestMessage;
using Network.Remote;

namespace MMONET.Message
{
    public partial class MessagePipline:IPacker<ISuperRemote>,ITcpPacker<ISuperRemote>,
        IReceiver<ISuperRemote>
    {
        #region Message

        /// <summary>
        /// 描述消息包长度字节所占的字节数
        /// <para>长度类型ushort，所以一个包理论最大长度不能超过65535字节，框架要求一个包不能大于8192 - 25 个 字节</para>
        /// 
        /// 按照千兆网卡计算，一个玩家每秒10~30包，大约10~30KB，大约能负载3000玩家。
        /// </summary>
        public const int MessageLengthByteCount = sizeof(ushort);

        /// <summary>
        /// 消息包类型ID 字节长度
        /// </summary>
        public const int MessageIDByteCount = sizeof(int);

        /// <summary>
        /// 消息包类型ID 字节长度
        /// </summary>
        public const int RpcIDByteCount = sizeof(ushort);

        /// <summary>
        /// 报头初始偏移8
        /// </summary>
        public const int HeaderOffset = 2 + 4 + 2;

        #endregion

        public static readonly MessagePipline Default = new MessagePipline();

        /// <summary>
        /// 测试用
        /// </summary>
        public static readonly IReceiver<ISuperRemote> TestReceiver= new TestMessagePipline();








        public virtual IMemoryOwner<byte> Packet<T>(short rpcID, T message, ISuperRemote remote)
        {
            ///序列化用buffer,使用内存池
            using (var memoryOwner = BufferPool.Rent(16384))
            {
                Span<byte> span = memoryOwner.Memory.Span;

                var (messageID, length) = SerializeMessage(message,span);
                ///省略了额外消息
                var sendbuffer = PacketBuffer(messageID, rpcID, EmptyExtraMessage, span.Slice(0, length));
                return sendbuffer;
            }
        }

        /// <summary>
        /// 分离粘包
        /// <para> <see cref="Packet(short, object, ISuperRemote)"/> 对应 </para>
        /// </summary>
        /// <param name="source"></param>
        /// <param name="pushCompleteMessage"></param>
        /// <returns>剩余的半包。</returns>
        public virtual ReadOnlySpan<byte> CutOff(ReadOnlySpan<byte> source, IList<IMemoryOwner<byte>> pushCompleteMessage)
        {
            var length = source.Length;
            ///已经完整读取消息包的长度
            int offset = 0;
            ///长度至少要大于2（2个字节表示消息总长度）
            while (length - offset > 2)
            {

                ///取得单个消息总长度
                ushort size = source.Slice(offset).ReadUshort();
                if (length - offset < size)
                {
                    ///剩余消息长度不是一个完整包
                    break;
                }

                /// 使用内存池
                var newMsg = BufferPool.Rent(size);

                source.Slice(offset, size).CopyTo(newMsg.Memory.Span);
                pushCompleteMessage.Add(newMsg);

                offset += size;
            }

            ///返回剩余的半包。
            return source.Slice(offset, length - offset);
        }
        public void Receive(IMemoryOwner<byte> packet, ISuperRemote remote)
        {
            try
            {
                var memory = packet.Memory;

                ///解包
                (int messageID,
                 short rpcID,
                 ReadOnlyMemory<byte> extraMessage,
                 ReadOnlyMemory<byte> messageBody) = UnPacketBuffer(memory);

                if (DealExtrMessage(messageID, rpcID, extraMessage, messageBody, remote))
                {
                    var message = DeserializeMessage(messageID, messageBody);

                    ///处理实例消息
                    MessageThreadTransducer.Push(rpcID, message, remote);
                }
            }
            finally
            {
                packet.Dispose();
            }
        }

        /// <summary>
        /// 消息管线终点
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public virtual ValueTask<object> DealMessage(object message)
        {
            return default;
        }
    }

    ///打包封包
    partial class MessagePipline
    {
        /// <summary>
        /// 解析报头 (长度至少要大于8（8个字节也就是一个报头长度）)
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException">数据长度小于报头长度</exception>
        public virtual (ushort totalLenght, int messageID, short rpcID)
            ParsePacketHeader(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length >= HeaderOffset)
            {
                ushort size = buffer.ReadUshort();

                int messageID = buffer.Slice(2).ReadInt();

                short rpcID = buffer.Slice(6).ReadShort();

                return (size, messageID, rpcID);
            }
            else
            {
                throw new ArgumentOutOfRangeException("数据长度小于报头长度");
            }
        }

        /// <summary>
        /// 封装将要发送的字节消息,这个方法控制消息字节的布局。并调用了 SerializeExtraMessage。
        /// <para>框架使用的字节布局 2总长度 + 4消息ID + 2RpcID + (ExtraMessageByte) + UserMessageByte</para>
        /// </summary>
        /// <param name="messageID"></param>
        /// <param name="rpcID"></param>
        /// <param name="extraMessage"></param>
        /// <param name="messageBody"></param>
        /// <returns>框架使用BigEndian</returns>
        public virtual IMemoryOwner<byte> PacketBuffer(int messageID, short rpcID, ReadOnlySpan<byte> extraMessage, ReadOnlySpan<byte> messageBody)
        {
            if (extraMessage.IsEmpty)
            {
                throw new ArgumentNullException($"额外消息部分至少长度为1，请使用{EmptyExtraMessage}");
            }
            ushort totolLength = (ushort)(HeaderOffset + extraMessage.Length + messageBody.Length);

            ///申请发送用 buffer ((框架约定1)发送字节数组发送完成后由发送逻辑回收)         额外信息的最大长度17
            var sendbufferOwner = BufferPool.Rent(totolLength);
            var span = sendbufferOwner.Memory.Span;

            ///写入报头 大端字节序写入
            totolLength.WriteTo(span);
            messageID.WriteTo(span.Slice(2));
            rpcID.WriteTo(span.Slice(6));

            ///拷贝额外消息
            extraMessage.CopyTo(span.Slice(HeaderOffset));
            ///拷贝消息正文
            messageBody.CopyTo(span.Slice(HeaderOffset + extraMessage.Length));

            return sendbufferOwner;
        }


        /// <summary>
        /// 解包。 这个方法解析消息字节的布局
        /// <para> 和 <see cref="PacketBuffer(int, short, ReadOnlySpan{byte}, ReadOnlySpan{byte})"/> 对应</para>
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
        /// <remarks>分离消息是使用报头描述的长度而不能依赖于Span长度</remarks>
        public virtual (int messageID, short rpcID, ReadOnlyMemory<byte> extraMessage, ReadOnlyMemory<byte> messageBody)
            UnPacketBuffer(ReadOnlyMemory<byte> buffer)
        {
            ReadOnlySpan<byte> span = buffer.Span;
            var (totalLenght, messageID, rpcID) = ParsePacketHeader(span);
            var extralength = ParseExtraMessageLength(span);

            var extraMessage = buffer.Slice(HeaderOffset, extralength);

            int start = HeaderOffset + extralength;
            ///分离消息是使用报头描述的长度而不能依赖于Span长度
            var messageBody = buffer.Slice(start, totalLenght - start);
            return (messageID, rpcID, extraMessage, messageBody);
        }
    }

    ///额外消息
    partial class MessagePipline
    {
        /// <summary>
        /// 额外信息的最大长度
        /// </summary>
        protected virtual int MaxExtraMessageLength { get; set; } = 32;

        /// <summary>
        /// 用数字255表示空的附加消息
        /// </summary>
        protected static readonly byte[] EmptyExtraMessage = new byte[1] { byte.MaxValue };

        /// <summary>
        /// 根据调用这发送的消息和ID,生成一些额外信息（例如消息ID为x的消息同时转发给另一个remote）。
        /// 最短也需要一个字节占位。
        /// </summary>
        /// <param name="messageID"></param>
        /// <param name="rpcID"></param>
        /// <param name="message"></param>
        /// <param name="buffer"></param>
        /// <returns>消息长度</returns>
        protected virtual ushort GenerateExtraMessage(int messageID, short rpcID, object message, Span<byte> buffer)
        {
            ///255 表示占位，方便测试
            buffer[0] = byte.MaxValue;
            return 1;
        }

        /// <summary>
        /// 解析附加信息长度。
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException">数据长度小于报头长度</exception>
        protected virtual ushort
            ParseExtraMessageLength(ReadOnlySpan<byte> buffer)
        {
            ///因为附加信息内容有限，所以没有使用ushort表示长度。用第一个字节表示附加信息类型。
            ///根据类型返回长度，通常不会有很多钟类型。
            if (buffer[0] == byte.MaxValue)
            {
                return 1;
            }
            return 1;
        }

        /// <summary>
        /// 处理额外消息，并返回是否继续执行后续处理过程
        /// </summary>
        /// <param name="messageID"></param>
        /// <param name="rpcID"></param>
        /// <param name="extraMessage"></param>
        /// <param name="messageBody"></param>
        /// <param name="remote"></param>
        /// <returns></returns>
        protected virtual bool DealExtrMessage(int messageID, short rpcID, ReadOnlyMemory<byte> extraMessage, ReadOnlyMemory<byte> messageBody,ISuperRemote remote)
        {
            return true;
        }
    }

    ///消息正文
    partial class MessagePipline
    {
        /// <summary>
        /// 序列化消息阶段
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="buffer16384"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        protected virtual (int messageID, ushort length)
            SerializeMessage<T>(T message, Span<byte> buffer16384) => MessageLUT.Serialize(message, buffer16384);

        /// <summary>
        /// 反序列化消息阶段
        /// </summary>
        /// <returns></returns>
        protected virtual object DeserializeMessage(int messageID, ReadOnlyMemory<byte> messageBody)
            => MessageLUT.Deserialize(messageID, messageBody);
    }

    internal class TestMessagePipline : MessagePipline
    {
        static int totalCount = 0;
        public async override ValueTask<object> DealMessage(object message)
        {
            totalCount++;
            switch (message)
            {
                case TestPacket1 packet1:
                    Console.WriteLine($"接收消息{nameof(TestPacket1)}--{packet1.Value}------总消息数{totalCount}");
                    return null;
                case TestPacket2 packet2:
                    Console.WriteLine($"接收消息{nameof(TestPacket2)}--{packet2.Value}");
                    return new TestPacket2 { Value = packet2.Value };
                default:
                    break;
            }
            return null;
        }
    }
}
