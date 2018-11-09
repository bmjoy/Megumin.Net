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
        protected internal static void Regist(Type type,KeyAlreadyHave key = KeyAlreadyHave.Skip)
        {
            var attribute = type.GetFirstCustomAttribute<ProtoContractAttribute>();
            if (attribute != null)
            {
                var MSGID = type.GetFirstCustomAttribute<MSGID>();
                if (MSGID != null)
                {
                    Regist(type, MSGID.ID,
                        Protobuf_netSerializerEx.MakeS(type), Protobuf_netSerializerEx.MakeD(type), key);
                }
            }
        }

        /// <summary>
        /// 注册消息类型
        /// </summary>
        /// <param name="key"></param>
        public static void Regist<T>(KeyAlreadyHave key = KeyAlreadyHave.Skip)
        {
            var type = typeof(T);
            var attribute = type.GetFirstCustomAttribute<ProtoContractAttribute>();
            if (attribute != null)
            {
                var MSGID = type.GetFirstCustomAttribute<MSGID>();
                if (MSGID != null)
                {
                    Regist<T>(MSGID.ID,
                        Protobuf_netSerializerEx.Serialize,
                        Protobuf_netSerializerEx.MakeD(type), 
                        key);
                }
            }
        }
    }

    static class Protobuf_netSerializerEx
    {
        public static ushort Serialize<T>(T obj, Span<byte> buffer)
        {
            using (Stream s = new MemoryStream())
            {
                Serializer.Serialize(s, obj);
                byte[] temp = new byte[16384];
                s.Seek(0,SeekOrigin.Begin);
                int lenght = s.Read(temp, 0, buffer.Length);
                temp.AsSpan().Slice(0,lenght).CopyTo(buffer);
                return (ushort)lenght;
            }
        }

        public static Serialize MakeS2<T>() => MessageLUT.Convert<T>(Serialize);

        public static Serialize MakeS(Type type)
        {
            var methodInfo = typeof(Protobuf_netSerializerEx).GetMethod(nameof(MakeS2),
                BindingFlags.Static | BindingFlags.Public);

            var method = methodInfo.MakeGenericMethod(type);
            var res = method.Invoke(null, null);

            return res as Serialize;
        }

        public static Deserialize MakeD(Type type)
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
