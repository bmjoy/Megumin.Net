using System;
using System.Collections.Generic;
using System.Text;

namespace MMONET.Remote.Test
{
    public class TestPacket1
    {
        public int Value { get; set; }

        public static ushort S<T>(T message, byte[] buffer)
        {
            if (message is TestPacket1 packet)
            {
                BitConverter.GetBytes(packet.Value).CopyTo(buffer, 0);
            }

            return 1000;
        }

        public static TestPacket1 D(ArraySegment<byte> buffer)
        {
            var res = new TestPacket1();
            res.Value = BitConverter.ToInt32(buffer.Array, buffer.Offset);
            return res;
        }
    }

    public class TestPacket2
    {
        public float Value { get; set; }

        public static ushort S<T>(T message, byte[] buffer)
        {
            if (message is TestPacket2 packet)
            {
                BitConverter.GetBytes(packet.Value).CopyTo(buffer, 0);
            }

            return 1000;
        }

        public static TestPacket2 D(ArraySegment<byte> buffer)
        {
            var res = new TestPacket2();
            res.Value = BitConverter.ToSingle(buffer.Array, buffer.Offset);
            return res;
        }
    }
}
