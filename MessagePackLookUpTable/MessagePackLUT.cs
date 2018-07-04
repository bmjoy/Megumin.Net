using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using MessagePack;

namespace MMONET.Sockets
{
    /// <summary>
    /// 适用于MessagePack协议的查找表
    /// </summary>
    public class MessagePackLUT: MessageLUT
    {
        /// <summary>
        /// 注册程序集中所有MessagePack协议类
        /// </summary>
        /// <param name="assembly"></param>
        /// <param name="key"></param>
        public static void Regist(Assembly assembly, KeyAlreadyHave key = KeyAlreadyHave.Skip)
        {
            var types = assembly.GetTypes();
            foreach (var item in types)
            {
                Regist(item, key);
            }
        }

        /// <summary>
        /// 注册消息类型
        /// </summary>
        /// <param name="type"></param>
        /// <param name="key"></param>
        public static void Regist(Type type,KeyAlreadyHave key = KeyAlreadyHave.Skip)
        {
            var attribute = type.GetFirstCustomAttribute<MessagePackObjectAttribute>();
            if (attribute != null)
            {
                var MSGID = type.GetFirstCustomAttribute<MSGID>();
                if (MSGID != null)
                {
                    AddFormatter(type, MSGID.ID,
                        MessagePackSerializerEx.MakeS(type), MessagePackSerializerEx.MakeD(type), key);
                }
            }
        }
    }

    static class MessagePackSerializerEx
    {
        public static ushort Serialize<T>(T obj, ref byte[] buffer)
        {
            var formatter = MessagePackSerializer.DefaultResolver.GetFormatterWithVerify<T>();

            var len = formatter.Serialize(ref buffer, 0, obj, MessagePackSerializer.DefaultResolver);

            return (ushort)len;
        }

        public static Delegate MakeS(Type type)
        {
            var methodInfo = typeof(MessagePackSerializerEx).GetMethod(nameof(Serialize),
                BindingFlags.Static | BindingFlags.Public);

            var method = methodInfo.MakeGenericMethod(type);

            return method.CreateDelegate(typeof(Seiralizer<>).MakeGenericType(type));
        }

        public static Deserilizer MakeD(Type type)
        {
            var methodInfo = typeof(MessagePackSerializer).GetMethod(nameof(MessagePackSerializer.Deserialize),
                new Type[] { typeof(ArraySegment<byte>) });

            var method = methodInfo.MakeGenericMethod(type);

            return method.CreateDelegate(typeof(Deserilizer)) as Deserilizer;
        }
    }
}
