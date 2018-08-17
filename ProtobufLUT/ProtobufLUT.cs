using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Google.Protobuf;
using System.IO;
using System.Buffers;

namespace MMONET.Message
{
    /// <summary>
    /// 适用于Protobuf协议的查找表           没有测试可能有BUG
    /// </summary>
    public class ProtobufLUT : MessageLUT
    {
        /// <summary>
        /// 注册程序集中所有议类
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
        public static void Regist(Type type, KeyAlreadyHave key = KeyAlreadyHave.Skip)
        {
            var MSGID = type.GetFirstCustomAttribute<MSGID>();
            if (MSGID != null)
            {
                AddFormatter(type, MSGID.ID,
                    ProtobufLUTSerializerEx.MakeS(type), ProtobufLUTSerializerEx.MakeD(type), key);
            }
            
        }
    }

    static class ProtobufLUTSerializerEx
    {
        public static ushort Serialize<T>(IMessage<T> obj, byte[] buffer)
            where T:IMessage<T>
        {
            using (CodedOutputStream co = new CodedOutputStream(buffer))
            {
                obj.WriteTo(co);
            }

            return (ushort)obj.CalculateSize();
        }

        public static Delegate MakeS(Type type)
        {
            var methodInfo = typeof(ProtobufLUTSerializerEx).GetMethod(nameof(Serialize),
                BindingFlags.Static | BindingFlags.Public);

            var method = methodInfo.MakeGenericMethod(type);

            return method.CreateDelegate(typeof(Seiralizer<>).MakeGenericType(type));
        }

        public static Deserilizer MakeD(Type type)
        {
            var parsertype = typeof(Google.Protobuf.MessageParser<>).MakeGenericType(type);
            dynamic dformatter = Activator.CreateInstance(parsertype);
            return  (buffer) =>
                    {
                        using (ReadOnlyMemrotyStream stream = new ReadOnlyMemrotyStream(buffer))
                        {
                            IMessage message = dformatter.ParseFrom(stream);
                            return message;
                        }
                    };
        }
    }
}
