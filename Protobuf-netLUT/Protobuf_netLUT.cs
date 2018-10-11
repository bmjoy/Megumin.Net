using System;
using System.Buffers;
using System.IO;
using System.Reflection;
using ProtoBuf;

namespace Megumin.Message
{
    /// <summary>
    /// 适用于Protobuf-net协议的查找表    没有测试
    /// </summary>
    public class Protobuf_netLUT: MessageLUT
    {
        /// <summary>
        /// 注册程序集中所有协议类
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
            var attribute = type.GetFirstCustomAttribute<ProtoContractAttribute>();
            if (attribute != null)
            {
                var MSGID = type.GetFirstCustomAttribute<MSGID>();
                if (MSGID != null)
                {
                    AddFormatter(type, MSGID.ID,
                        Protobuf_netSerializerEx.MakeS(type), Protobuf_netSerializerEx.MakeD(type), key);
                }
            }
        }
    }

    static class Protobuf_netSerializerEx
    {
        public static ushort Serialize<T>(T obj, byte[] buffer)
        {
            using (Stream s = new MemoryStream())
            {
                Serializer.Serialize(s, obj);
                int lenght = s.Read(buffer, 0, buffer.Length);
                return (ushort)lenght;
            }
        }

        public static Delegate MakeS(Type type)
        {
            var methodInfo = typeof(Protobuf_netSerializerEx).GetMethod(nameof(Serialize),
                BindingFlags.Static | BindingFlags.Public);

            var method = methodInfo.MakeGenericMethod(type);

            return method.CreateDelegate(typeof(Seiralizer<>).MakeGenericType(type));
        }

        public static Deserilizer MakeD(Type type)
        {
            return (buffer) =>
            {
                using (Stream st = new ReadOnlyMemrotyStream(buffer))
                {
                    return Serializer.Deserialize(type, st);
                }
            };
        }
    }
}
