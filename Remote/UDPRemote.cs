using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Network.Remote;
using static MMONET.Message.MessageLUT;
using static MMONET.Remote.FrameworkConst;

namespace MMONET.Remote
{
    /// <summary>
    /// 
    /// </summary>
    public partial class UDPRemote : UdpClient, IRemote, INetRemote
    {
        public int InstanceID { get; set; }
        public bool Connected { get; }
        public Socket Socket => this.Client;
        public bool IsVaild { get; }
        public bool IsSending { get; }
        public double RpcTimeOut { get; set; }

        public new void SendAsync<T>(T message)
        {
            throw new NotImplementedException();
        }

        public Task<(RpcResult Result, Exception Excption)> RpcSendAsync<RpcResult>(dynamic message)
        {
            throw new NotImplementedException();
        }

        public Task<RpcResult> SafeRpcSendAsync<RpcResult>(dynamic message, Action<Exception> OnException = null)
        {
            throw new NotImplementedException();
        }

        public int ReceiveBufferSize { get; }
        public bool IsReceiving { get; }

        public void Receive(OnReceiveMessage onReceive)
        {
            throw new NotImplementedException();
        }

        public event Action<SocketError> OnDisConnect;

        public void Disconnect(bool triggerOnDisConnectEvent = true)
        {
            throw new NotImplementedException();
        }

        public void ReceiveCallback(int messageID, short rpcID, dynamic msg)
        {
            throw new NotImplementedException();
        }

        public int Guid { get; }

        public ValueTask<Exception> ConnectAsync(IPEndPoint endPoint, int retryCount = 0)
        {
            throw new NotImplementedException();
        }

        public IPEndPoint IPEndPoint { get; set; }
    }

    partial class UDPRemote
    {
        public async void Connect()
        {
            lastseq = new Random().Next(0, 10000);
            this.WriteConnectMessage(1, 0, lastseq, lastack);
            connectState = UDPConnectState.SYN_SENT;
            var rec = await this.ReceiveAsync();
            var (SYN, ACK, seq, ack) = ReadConnectMessage(rec.Buffer);
            if (SYN == 1 && ACK == 1 && lastseq +1 == ack)
            {
                connectState = UDPConnectState.ESTABLISHED;
                lastseq += 1;
                this.WriteConnectMessage(1, 1, lastseq, ack + 1);
            }
            else
            {
                connectState = UDPConnectState.INVALID;
            }
        }

        UDPConnectState connectState = UDPConnectState.CLOSED;
        int lastseq;
        int lastack;

        internal void TryAccept(UdpReceiveResult udpReceive)
        {
            if (connectState == UDPConnectState.ESTABLISHED)
            {
                ///已经成功连接，忽略连接请求
                return;
            }

            if (connectState == UDPConnectState.CLOSED)
            {
                connectState = UDPConnectState.LISTEN;
            }

            var (Size, MessageID, RpcID) = ParsePacketHeader(udpReceive.Buffer, udpReceive.Buffer.Length);

            if (MessageID != UdpConnectMessageID)
            {
                connectState = UDPConnectState.INVALID;
                return;
            }

            var (SYN, ACK, seq, ack) = ReadConnectMessage(udpReceive.Buffer);

            switch (connectState)
            {
                case UDPConnectState.LISTEN:
                    if (SYN == 1 && ACK == 0)
                    {
                        connectState = UDPConnectState.SYN_RCVD;
                        lastack = new Random().Next(0, 10000);
                        lastseq = seq;
                        this.WriteConnectMessage(1, 1, lastack, seq + 1);
                    }
                    else
                    {
                        connectState = UDPConnectState.INVALID;
                    }
                    break;
                case UDPConnectState.SYN_RCVD:
                    if (ACK == 1 && lastseq +1 == seq && lastack +1 == ack)
                    {
                        connectState = UDPConnectState.ESTABLISHED;
                    }
                    else
                    {
                        connectState = UDPConnectState.INVALID;
                    }
                    break;
                case UDPConnectState.ESTABLISHED:
                    break;
                case UDPConnectState.INVALID:
                    break;
                default:
                    break;
            }

        }

        static (int SYN,int ACK,int seq,int ack) ReadConnectMessage(byte[] buffer)
        {
            int SYN = BitConverter.ToInt32(buffer, TotalHeaderByteCount);
            int ACK = BitConverter.ToInt32(buffer, TotalHeaderByteCount + 4);
            int seq = BitConverter.ToInt32(buffer, TotalHeaderByteCount + 8);
            int ack = BitConverter.ToInt32(buffer, TotalHeaderByteCount + 12);
            return (SYN, ACK, seq, ack);
        }
    }

    static class ConnectEX
    {
        public static async void WriteConnectMessage(this UdpClient client, int SYN, int ACT, int seq, int ack)
        {
            var bf = BufferPool.Pop(32);
            MakePacket(16, UdpConnectMessageID, 0, bf);
            BitConverter.GetBytes(SYN).CopyTo(bf, TotalHeaderByteCount);
            BitConverter.GetBytes(ACT).CopyTo(bf, TotalHeaderByteCount + 4);
            BitConverter.GetBytes(seq).CopyTo(bf, TotalHeaderByteCount + 8);
            BitConverter.GetBytes(ack).CopyTo(bf, TotalHeaderByteCount + 12);
            var offset = await client.SendAsync(bf, bf.Length);
            BufferPool.Push(bf);
        }
    }

    enum UDPConnectState
    {
        CLOSED,
        LISTEN,
        SYN_SENT,
        SYN_RCVD,
        ESTABLISHED,
        INVALID,
    }
}
