using System;
using System.Collections.Generic;

namespace MMONET.Message
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
    /// 消息查找表
    /// <seealso cref="Seiralizer{T}"/><seealso cref="Deserilizer"/>
    /// </summary>
    public class MessageLUT
    {

        static readonly Dictionary<int, Deserilizer> dFormatter = new Dictionary<int, Deserilizer>();
        static readonly Dictionary<Type, (int MessageID, Delegate Seiralizer)> sFormatter = new Dictionary<Type, (int MessageID, Delegate Seiralizer)>();
        ///序列化方法第二个参数必须为 byte[]
        static Type args2type = typeof(byte[]);

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

        public static void AddFormatter(Type type, int messageID, Delegate seiralizer, Deserilizer deserilizer, KeyAlreadyHave key = KeyAlreadyHave.Skip)
        {
            AddSFormatter(type, messageID, seiralizer, key);
            AddDFormatter(messageID, deserilizer, key);
        }

        public static void AddFormatter<T>(int messageID, Seiralizer<T> seiralizer, Deserilizer deserilizer, KeyAlreadyHave key = KeyAlreadyHave.Skip)
        {
            AddSFormatter(typeof(T), messageID, seiralizer, key);
            AddDFormatter(messageID, deserilizer, key);
        }

        /// <summary>
        /// 长度不能大于8192
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="buffer16384"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"> 消息长度大于8192,请拆分发送。"</exception>
        /// <remarks>框架中TCP接收最大支持8192，所以发送也不能大于8192，为了安全起见，框架提供的字节数组长度是16384的。</remarks>
        public static (int messageID, ArraySegment<byte> byteUserMessage) 
            Serialize<T>(byte[] buffer16384, T message)
        {
            if (sFormatter.TryGetValue(message.GetType(),out var sf))
            {
                ///序列化消息
                var (MessageID, Seiralizer) = sf;

                Seiralizer<T> seiralizer = Seiralizer as Seiralizer<T>;

                ushort length = seiralizer(message, buffer16384);

                if (length > 8192)
                {
                    BufferPool.Push16384(buffer16384);
                    ///消息过长
                    throw new ArgumentOutOfRangeException($"消息长度大于{8192}," +
                        $"请拆分发送。");
                }

                return (MessageID, new ArraySegment<byte>(buffer16384, 0, length));
            }

            return (-1,default);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="messageID"></param>
        /// <param name="body"></param>
        /// <returns></returns>
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
    }
}
