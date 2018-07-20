using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Network.Remote;
using static MMONET.Message.MessageLUT;

namespace MMONET.Remote
{
    /// <summary>
    /// 不支持多播地址
    /// </summary>
    public partial class UDPRemote : UdpClient, IRemote, INetRemote
    {
        public int InstanceID { get; set; }
        public bool Connected { get; private set; }
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

        public int Guid { get; } = InterlockedID<IRemote>.NewID();

        bool isConnecting = false;
        public async Task<Exception> ConnectAsync(IPEndPoint endPoint, int retryCount = 0)
        {
            if (isConnecting)
            {
                return new Exception("连接正在进行中");
            }
            isConnecting = true;
            this.IPEndPoint = endPoint;
            while (retryCount >= 0)
            {
                var res = await this.Connect();
                if (res)
                {
                    isConnecting = false;
                    return null;
                }
                retryCount--;
            }

            isConnecting = false;
            return new SocketException((int)SocketError.AccessDenied);
        }

        public IPEndPoint IPEndPoint { get; set; }
    }

    partial class UDPRemote
    {
        async Task<bool> Connect()
        {
            lastseq = new Random().Next(0, 10000);
            this.WriteConnectMessage(1, 0, lastseq, lastack);

            ///SYN_SENT
            var res = await this.ReceiveAsync();

            var (Size, MessageID, RpcID) = ParsePacketHeader(res.Buffer, 0);
            if (MessageID != FrameworkConst.UdpConnectMessageID)
            {
                return false;
            }

            var (SYN, ACK, seq, ack) = ReadConnectMessage(res.Buffer);
            if (SYN == 1 && ACK == 1 && lastseq +1 == ack)
            {
                ///ESTABLISHED
                lastseq += 1;
                ///重定向到新的远端，并忽略所有其他远端消息。
                IPEndPoint = res.RemoteEndPoint;

                ///测试超时
                System.Threading.Thread.Sleep(6000);

                this.WriteConnectMessage(1, 1, lastseq, seq + 1);
                Connect(IPEndPoint);
                Connected = true;
                return true;
            }
            else
            {
                return false;
            }
        }

        UDPConnectState connectState = UDPConnectState.CLOSED;
        int lastseq;
        int lastack;

        internal async Task<bool> TryAccept(UdpReceiveResult udpReceive)
        {
            if (Connected)
            {
                ///已经成功连接，忽略连接请求
                return true;
            }
            
            ///LISTEN;

            var (SYN, ACK, seq, ack) = ReadConnectMessage(udpReceive.Buffer);

            if (SYN == 1 && ACK == 0)
            {
                ///SYN_RCVD;
                lastack = new Random().Next(0, 10000);
                lastseq = seq;

                IPEndPoint = udpReceive.RemoteEndPoint;
                this.WriteConnectMessage(1, 1, lastack, seq + 1);

                var res = await ReceiveAsync();

                var (Size, MessageID, RpcID) = ParsePacketHeader(res.Buffer, 0);
                if (MessageID != FrameworkConst.UdpConnectMessageID)
                {
                    return false;
                }

                (SYN, ACK, seq, ack) = ReadConnectMessage(res.Buffer);

                if (ACK == 1 && lastseq + 1 == seq && lastack + 1 == ack)
                {
                    Connected = true;
                    return true;
                }
                else
                {
                    ///INVALID;
                    return false;
                }
            }
            else
            {
                ///INVALID;
                return false;
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
        public static async Task WriteConnectMessage(this UDPRemote client, int SYN, int ACT, int seq, int ack)
        {
            var bf = BufferPool.Pop(32);
            MakePacket(16, FrameworkConst.UdpConnectMessageID, 0, bf);
            BitConverter.GetBytes(SYN).CopyTo(bf, TotalHeaderByteCount);
            BitConverter.GetBytes(ACT).CopyTo(bf, TotalHeaderByteCount + 4);
            BitConverter.GetBytes(seq).CopyTo(bf, TotalHeaderByteCount + 8);
            BitConverter.GetBytes(ack).CopyTo(bf, TotalHeaderByteCount + 12);
            var offset = await client.SendAsync(bf, bf.Length, client.IPEndPoint);
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
