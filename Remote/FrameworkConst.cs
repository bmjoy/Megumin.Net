using System;
using System.Collections.Generic;
using System.Text;

namespace MMONET.Remote
{
    /// <summary>
    /// 框架占用的常量
    /// </summary>
    public class FrameworkConst
    {

        /// <summary>
        /// Udp握手连接使用的消息ID编号
        /// </summary>
        public const int UdpConnectMessageID = 101;
        /// <summary>
        /// 心跳包ID，255好识别，buffer[2-5]=[255,0,0,0]
        /// </summary>
        public const int HeartbeatsMessageID = 255;

        

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
        /// 报头初始偏移8
        /// </summary>
        public const int HeaderOffset = 2 + 4 + 2;

        #endregion
    }

    internal static class BitConvert_C6FCE74980A447BBACC6792A9E36F323
    {
        public static void WriteToByte(this short num, byte[] buffer, int offset)
        {
            var b = BitConverter.GetBytes(num);
            Buffer.BlockCopy(b, 0, buffer, offset, 2);
        }

        public static void WriteToByte(this ushort num, byte[] buffer, int offset)
        {
            var b = BitConverter.GetBytes(num);
            Buffer.BlockCopy(b, 0, buffer, offset, 2);
        }

        public static void WriteToByte(this int num, byte[] buffer, int offset)
        {
            var b = BitConverter.GetBytes(num);
            Buffer.BlockCopy(b, 0, buffer, offset, 4);
        }

        public static void WriteToByte(this long num, byte[] buffer, int offset)
        {
            var b = BitConverter.GetBytes(num);
            Buffer.BlockCopy(b, 0, buffer, offset, 8);
        }

        public static short ReadShort(this byte[] buffer, int offset)
        {
            return BitConverter.ToInt16(buffer, offset);
        }

        public static ushort ReadUShort(this byte[] buffer, int offset)
        {
            return BitConverter.ToUInt16(buffer, offset);
        }

        public static int ReadInt(this byte[] buffer, int offset)
        {
            return BitConverter.ToInt32(buffer, offset);
        }

    }
}
