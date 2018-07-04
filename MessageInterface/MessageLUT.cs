using System;
using System.Collections.Generic;
using System.Text;

namespace MMONET.Sockets
{
    /// <summary>
    /// 消息查找表
    /// <seealso cref="Seiralizer{T}"/><seealso cref="Deserilizer"/>
    /// </summary>
    public class MessageLUT
    {
        #region Message

        /// <summary>
        /// 描述消息包长度字节所占的字节数
        /// <para>长度类型ushort，所以一个包理论最大长度不能超过65535字节，框架要求一个包不能大于8192 - 8 个 字节</para>
        /// <para>建议单个包大小10到1024字节</para>
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
        /// 报头总长度
        /// </summary>
        public const int TotalHeaderByteCount =
            MessageLengthByteCount + MessageIDByteCount + RpcIDByteCount;

        #endregion

        static readonly Dictionary<int, Deserilizer> dFormatter = new Dictionary<int, Deserilizer>();
        static readonly Dictionary<Type, (int MessageID, Delegate Seiralizer)> sFormatter = new Dictionary<Type, (int MessageID, Delegate Seiralizer)>();
        ///序列化方法第二个参数必须为 ref byte[]
        static Type args2type = typeof(byte[]).MakeByRefType();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="lut"></param>
        /// <exception cref="ArgumentException">反序列化messageID冲突，或者序列化类型冲突</exception>
        public static void AddFormatterLookUpTabal(ILookUpTabal lut)
        {
            foreach (var item in lut.DeserilizerKV)
            {
                AddDFormatter(item.Key,item.Value);
            }

            foreach (var item in lut.SeiralizerKV)
            {
                AddSFormatter(item.Key,item.Value.MessageID,item.Value.Seiralizer);
            }
        }

        protected static void AddSFormatter(Type type, int messageID, Delegate seiralizer, KeyAlreadyHave key = KeyAlreadyHave.Skip)
        {
            if (type == null || seiralizer == null)
            {
                throw new ArgumentNullException();
            }

            var args = seiralizer.Method.GetParameters();
            if (args.Length != 2)
            {
                throw new ArgumentException("序列化函数参数数量不匹配");
            }
            if (type != args[0].ParameterType && !type.IsSubclassOf(args[0].ParameterType))
            {
                throw new ArgumentException($"序列化参数1:类型不匹配,{type}不是{nameof(seiralizer)}的第一个参数类型或它的子类。");
            }

            if (args[1].ParameterType != args2type)
            {
                throw new ArgumentException($"序列化函数参数2:不是 byte[]");
            }
            if (seiralizer.Method.ReturnType != typeof(ushort))
            {
                throw new ArgumentException($"序列化函数返回类型不是 ushort");
            }

            switch (key)
            {
                case KeyAlreadyHave.Replace:
                    sFormatter[type] = (messageID, seiralizer);
                    return;
                case KeyAlreadyHave.Skip:
                    if (sFormatter.ContainsKey(type))
                    {
                        return;
                    }
                    else
                    {
                        sFormatter.Add(type, (messageID, seiralizer));
                    }
                    break;
                case KeyAlreadyHave.ThrowException:
                default:
                    sFormatter.Add(type, (messageID, seiralizer));
                    break;
            }
        }

        protected static void AddDFormatter(int messageID, Deserilizer deserilizer, KeyAlreadyHave key = KeyAlreadyHave.Skip)
        {
            if (deserilizer == null)
            {
                throw new ArgumentNullException();
            }

            switch (key)
            {
                case KeyAlreadyHave.Replace:
                    dFormatter[messageID] = deserilizer;
                    return;
                case KeyAlreadyHave.Skip:
                    if (dFormatter.ContainsKey(messageID))
                    {
                        return;
                    }
                    else
                    {
                        dFormatter.Add(messageID, deserilizer);
                    }
                    break;
                case KeyAlreadyHave.ThrowException:
                default:
                    dFormatter.Add(messageID, deserilizer);
                    break;
            }
        }

        protected static void AddFormatter(Type type, int messageID, Delegate seiralizer, Deserilizer deserilizer, KeyAlreadyHave key = KeyAlreadyHave.Skip)
        {
            AddSFormatter(type, messageID, seiralizer, key);
            AddDFormatter(messageID, deserilizer, key);
        }

        public static ArraySegment<byte> Serialize<T>(short rpcID, T message)
        {
            ///序列化消息

            var (MessageID, Seiralizer) = sFormatter[message.GetType()];

            ///序列化用buffer
            var buffer65536 = BufferPool.Pop65536();

            Seiralizer<T> seiralizer = Seiralizer as Seiralizer<T>;

            ushort length = seiralizer(message, ref buffer65536);

            if (length > 8192 - TotalHeaderByteCount)
            {
                BufferPool.Push65536(buffer65536);
                ///消息过长
                throw new ArgumentOutOfRangeException($"消息长度大于{8192 - TotalHeaderByteCount}," +
                    $"请拆分发送。");
            }

            ///待发送buffer
            var messagebuffer = BufferPool.Pop(length + TotalHeaderByteCount);

            ///封装报头
            MakePacket(length, MessageID, rpcID, messagebuffer);
            ///第一次消息值拷贝
            Buffer.BlockCopy(buffer65536, 0, messagebuffer, TotalHeaderByteCount, length);
            ///返还序列化用buffer
            BufferPool.Push65536(buffer65536);

            return new ArraySegment<byte>(messagebuffer, 0, length + TotalHeaderByteCount);
        }

        public static dynamic Deserialize(int messageID,ArraySegment<byte> body)
        {
            if (dFormatter.ContainsKey(messageID))
            {
                return dFormatter[messageID](body);
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// 封包
        /// </summary>
        /// <param name="length"></param>
        /// <param name="messageID"></param>
        /// <param name="rpcID"></param>
        /// <param name="sbuffer"></param>
        public static void MakePacket(ushort length, int messageID, short rpcID, byte[] sbuffer)
        {
            int offset = 0;

            sbuffer[offset] = unchecked((byte)(length >> 8));
            sbuffer[offset + 1] = unchecked((byte)(length));
            //BitConverter.GetBytes(length).CopyTo(sbuffer, 0);
            offset += MessageLengthByteCount;


            sbuffer[offset] = unchecked((byte)(messageID >> 24));
            sbuffer[offset + 1] = unchecked((byte)(messageID >> 16));
            sbuffer[offset + 2] = unchecked((byte)(messageID >> 8));
            sbuffer[offset + 3] = unchecked((byte)(messageID));
            offset += MessageIDByteCount;


            sbuffer[offset] = unchecked((byte)(rpcID >> 8));
            sbuffer[offset + 1] = unchecked((byte)(rpcID));

            offset += RpcIDByteCount;
        }

        /// <summary>
        /// 解析报头 (长度至少要大于8（8个字节也就是一个报头长度）)
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException">数据长度小于报头长度</exception>
        public static (ushort Size, int MessageID, short RpcID)
            ParsePacketHeader(byte[] buffer, int offset)
        {
            if (buffer.Length - offset >= TotalHeaderByteCount)
            {
                ushort size = (ushort)(buffer[offset] << 8 | buffer[offset + 1]);

                int messageID = (int)(buffer[offset + MessageLengthByteCount] << 24
                                    | buffer[offset + MessageLengthByteCount + 1] << 16
                                    | buffer[offset + MessageLengthByteCount + 2] << 8
                                    | buffer[offset + MessageLengthByteCount + 3]);

                short rpcID = (short)(buffer[offset + MessageLengthByteCount + MessageIDByteCount] << 8
                                    | buffer[offset + MessageLengthByteCount + MessageIDByteCount + 1]);

                return (size, messageID, rpcID);
            }
            else
            {
                throw new ArgumentOutOfRangeException("数据长度小于报头长度");
            }
        }
    }
}
