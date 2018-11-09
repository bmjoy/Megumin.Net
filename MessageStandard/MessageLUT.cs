using Megumin.Message.TestMessage;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Megumin.Message
{
    /// <summary>
    /// Key冲突改怎么做
    /// </summary>
    public enum KeyAlreadyHave
    {
        /// <summary>
        /// 替换
        /// </summary>
        Replace,
        /// <summary>
        /// 跳过
        /// </summary>
        Skip,
        /// <summary>
        /// 抛出异常
        /// </summary>
        ThrowException,
    }

    /// <summary>
    /// 等待所有框架支持完毕ReadOnlyMemory 切换为ReadOnlySpan，现在需要将ReadOnlyMemory包装成流
    /// </summary>
    /// <param name="buffer"></param>
    /// <returns></returns>
    public delegate object Deserialize(ReadOnlyMemory<byte> buffer);
    /// <summary>
    /// 将消息从0位置开始 序列化 到 指定buffer中,返回序列化长度
    /// </summary>
    /// <param name="message">消息实例</param>
    /// <param name="buffer">给定的buffer,长度为16384</param>
    /// <returns>序列化消息的长度</returns>
    public delegate ushort RegistSerialize<in T>(T message, Span<byte> buffer);

    public delegate ushort Serialize(object message, Span<byte> buffer);

    /// <summary>
    /// 消息查找表
    /// <seealso cref="Seiralizer{T}"/><seealso cref="Message.Deserialize"/>
    /// </summary>
    public class MessageLUT
    {
        static MessageLUT()
        {
            ///注册测试消息和内置消息
            Regist<TestPacket1>(MSGID.TestPacket1ID, TestPacket1.S, TestPacket1.D);
            Regist<TestPacket2>(MSGID.TestPacket2ID, TestPacket2.S, TestPacket2.D);
            ///5个基础类型
            Regist<string>(MSGID.StringID, BaseType.Serialize, BaseType.StringDeserialize);
            Regist<int>(MSGID.IntID, BaseType.Serialize,BaseType.IntDeserialize);
            Regist<long>(MSGID.IntID, BaseType.Serialize,BaseType.LongDeserialize);
            Regist<float>(MSGID.IntID, BaseType.Serialize,BaseType.FloatDeserialize);
            Regist<double>(MSGID.IntID, BaseType.Serialize,BaseType.DoubleDeserialize);


            ///框架用类型
            Regist<HeartBeatsMessage>(MSGID.HeartbeatsMessageID,
                HeartBeatsMessage.Seiralizer, HeartBeatsMessage.Deserilizer, KeyAlreadyHave.ThrowException);


            Regist<UdpConnectMessage>(MSGID.UdpConnectMessageID,
                UdpConnectMessage.Serialize, UdpConnectMessage.Deserialize);
        }

        public static Serialize Convert<T>(RegistSerialize<T> registSerialize)
        {
            return (obj, buffer) =>
            {
                if (obj is T message)
                {
                    return registSerialize(message, buffer);
                }
                throw new InvalidCastException(typeof(T).FullName);
            };
        }

        static readonly Dictionary<int, (Type type,Deserialize deserialize)> dFormatter = new Dictionary<int, (Type type,Deserialize deserialize)>();
        static readonly Dictionary<Type, (int MessageID, Serialize serialize)> sFormatter = new Dictionary<Type, (int MessageID, Serialize serialize)>();
        
        protected static void AddSFormatter(Type type, int messageID, Serialize seiralize, KeyAlreadyHave key = KeyAlreadyHave.Skip)
        {
            if (type == null || seiralize == null)
            {
                throw new ArgumentNullException();
            }

            switch (key)
            {
                case KeyAlreadyHave.Replace:
                    sFormatter[type] = (messageID, seiralize);
                    return;
                case KeyAlreadyHave.Skip:
                    if (sFormatter.ContainsKey(type))
                    {
                        return;
                    }
                    else
                    {
                        sFormatter.Add(type, (messageID, seiralize));
                    }
                    break;
                case KeyAlreadyHave.ThrowException:
                default:
                    sFormatter.Add(type, (messageID, seiralize));
                    break;
            }
        }

        protected static void AddDFormatter(int messageID,Type type, Deserialize deserilize, KeyAlreadyHave key = KeyAlreadyHave.Skip)
        {
            if (deserilize == null)
            {
                throw new ArgumentNullException();
            }

            switch (key)
            {
                case KeyAlreadyHave.Replace:
                    dFormatter[messageID] = (type, deserilize);
                    return;
                case KeyAlreadyHave.Skip:
                    if (dFormatter.ContainsKey(messageID))
                    {
                        return;
                    }
                    else
                    {
                        dFormatter.Add(messageID, (type, deserilize));
                    }
                    break;
                case KeyAlreadyHave.ThrowException:
                default:
                    dFormatter.Add(messageID, (type, deserilize));
                    break;
            }
        }

        public static void Regist(Type type, int messageID, Serialize seiralize, Deserialize deserilizer, KeyAlreadyHave key = KeyAlreadyHave.Skip)
        {
            AddSFormatter(type, messageID, seiralize, key);
            AddDFormatter(messageID,type, deserilizer, key);
        }

        public static void Regist<T>(int messageID, RegistSerialize<T> seiralize, Deserialize deserilizer, KeyAlreadyHave key = KeyAlreadyHave.Skip)
        {
            AddSFormatter(typeof(T), messageID, Convert(seiralize), key);
            AddDFormatter(messageID,typeof(T), deserilizer, key);
        }

        /// <summary>
        /// 长度不能大于8192
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="buffer16384"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"> 消息长度大于8192 - 25(框架用长度),请拆分发送。"</exception>
        /// <remarks>框架中TCP接收最大支持8192，所以发送也不能大于8192，为了安全起见，框架提供的字节数组长度是16384的。</remarks>
        public static (int messageID, ushort length)
            Serialize(object message,Span<byte> buffer16384)
        {
            var type = message.GetType();
            if (sFormatter.TryGetValue(type, out var sf))
            {
                ///序列化消息
                var (MessageID, Seiralize) = sf;

                if (Seiralize == null)
                {
                    OnMissSeiralizer?.Invoke(type);
                    return (-1, default);
                }

                ushort length = Seiralize(message, buffer16384);

                if (length > 8192 - 25)
                {
                    //BufferPool.Push16384(buffer16384);
                    ///消息过长
                    throw new ArgumentOutOfRangeException($"消息长度大于{8192 - 25}," +
                        $"请拆分发送。");
                }

                return (MessageID, length);
            }
            else
            {
                OnMissSeiralizer?.Invoke(type);
                return (-1, default);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="messageID"></param>
        /// <param name="body"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object Deserialize(int messageID,in ReadOnlyMemory<byte> body)
        {
            if (dFormatter.ContainsKey(messageID))
            {
                return dFormatter[messageID].deserialize(body);
            }
            else
            {
                OnMissDeserializer?.Invoke(messageID);
                return null;
            }
        }


        public static event Action<int> OnMissDeserializer;
        public static event Action<Type> OnMissSeiralizer;

        public static Type GetMessageType(int messageID)
        {
            if (dFormatter.TryGetValue(messageID,out var res))
            {
                return res.type;
            }
            else
            {
                return null;
            }
        }

        public static int? GetMsgID<T>()
        {
            if (sFormatter.TryGetValue(typeof(T),out var res))
            {
                return res.MessageID;
            }
            return null;
        }
    }

}
