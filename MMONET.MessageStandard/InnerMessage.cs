using System;
using System.Collections.Generic;
using System.Text;

namespace MMONET.Message
{


    /// <summary>
    /// 心跳包消息
    /// </summary>
    public class HeartBeatsMessage
    {
        /// <summary>
        /// 发送时间，服务器收到心跳包原样返回；
        /// </summary>
        public DateTime Time { get; set; }

        public static ushort Seiralizer(HeartBeatsMessage heartBeats, Span<byte> buffer)
        {
            heartBeats.Time.ToBinary().WriteTo(buffer);
            return sizeof(long);
        }

        public static HeartBeatsMessage Deserilizer(ReadOnlyMemory<byte> buffer)
        {
            long t = buffer.Span.ReadLong();
            return new HeartBeatsMessage() { Time = new DateTime(t) };
        }
    }


    public class UdpConnectMessage
    {
        public int SYN;
        public int ACT;
        public int seq;
        public int ack;
        internal static UdpConnectMessage Deserialize(ReadOnlyMemory<byte> buffer)
        {
            int SYN = buffer.Span.ReadInt();
            int ACT = buffer.Span.Slice(4).ReadInt();
            int seq = buffer.Span.Slice(8).ReadInt();
            int ack = buffer.Span.Slice(12).ReadInt();
            return new UdpConnectMessage() { SYN = SYN, ACT = ACT, seq = seq, ack = ack };
        }

        internal static ushort Serialize(UdpConnectMessage connectMessage, Span<byte> bf)
        {
            connectMessage.SYN.WriteTo(bf);
            connectMessage.ACT.WriteTo(bf.Slice(4));
            connectMessage.seq.WriteTo(bf.Slice(8));
            connectMessage.ack.WriteTo(bf.Slice(12));
            return 16;
        }

        public void Deconstruct(out int SYN, out int ACT, out int seq, out int ack)
        {
            SYN = this.SYN;
            ACT = this.ACT;
            seq = this.seq;
            ack = this.ack;
        }
    }
}
