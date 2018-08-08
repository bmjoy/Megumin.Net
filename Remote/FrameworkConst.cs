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

        /// <summary>
        /// 报头初始偏移8
        /// </summary>
        public const ushort HeaderOffset = 2 + 4 + 2;
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
