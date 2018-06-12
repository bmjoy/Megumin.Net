using System;
using System.Collections.Generic;
using System.Text;

namespace MMONET.Sockets.Test
{


    public class TestPacket1
    {
        public int Value { get; set; }

        public static ushort S<T>(T message, ref byte[] buffer)
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

        public static ushort S<T>(T message, ref byte[] buffer)
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

    public class TestLut : ILookUpTabal
    {
        public IEnumerable<KeyValuePair<int, Deserilizer>> DeserilizerKV => D;

        public IEnumerable<KeyValuePair<Type, (int MessageID, Delegate Seiralizer)>> SeiralizerKV => S;


        public Dictionary<int, Deserilizer> D = new Dictionary<int, Deserilizer>()
            {
                { 1000,TestPacket1.D},
                { 1001,TestPacket2.D},
            };

        public Dictionary<Type, (int MessageID, Delegate Seiralizer)> S = new Dictionary<Type, (int MessageID, Delegate Seiralizer)>()
        {
            { typeof(TestPacket1),(1000,(Seiralizer<TestPacket1>)TestPacket1.S) },
            { typeof(TestPacket2),(1001,(Seiralizer<TestPacket2>)TestPacket2.S) },
        };


    }
}
