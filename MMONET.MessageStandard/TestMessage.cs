using System;
using System.Collections.Generic;
using System.Text;

namespace MMONET.Message.TestMessage
{
    public class TestPacket1
    {
        static TestPacket1()
        {
            MessageLUT.AddFormatter<TestPacket1>(-101, S, D);
        }


        public int Value { get; set; }

        public static ushort S(TestPacket1 message, Span<byte> buffer)
        {
            message.Value.WriteTo(buffer);
            return 1000;
        }

        public static TestPacket1 D(ReadOnlyMemory<byte> buffer)
        {
            var res = new TestPacket1();
            res.Value = buffer.Span.ReadInt();
            return res;
        }
    }

    public class TestPacket2
    {
        static TestPacket2()
        {
            MessageLUT.AddFormatter<TestPacket2>(-102, S, D);
        }

        public float Value { get; set; }

        public static ushort S(TestPacket2 message, Span<byte> buffer)
        {
            BitConverter.GetBytes(message.Value).AsSpan().CopyTo(buffer);
            return 1000;
        }

        public static TestPacket2 D(ReadOnlyMemory<byte> buffer)
        {
            var res = new TestPacket2();
            var temp = new byte[4];
            buffer.Span.Slice(0, 4).CopyTo(temp);
            res.Value = BitConverter.ToSingle(temp,0);
            return res;
        }
    }
}
