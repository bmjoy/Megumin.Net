using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using MMONET.Message;
using Network.Remote;

namespace MMONET.Remote
{
    /// <summary>
    /// 断线重连
    /// </summary>
    public class ReConnector : IReConnectable
    {
        private bool _isReConnect;

        public ReConnector(IRemote remote)
        {
            this.Remote = remote;
        }

        public IRemote Remote { get; }
        public bool IsReConnect
        {
            get => _isReConnect;
            set
            {
                if (_isReConnect != value)
                {
                    _isReConnect = value;

                    SendHeartAsync();
                }
            }
        }

        readonly HeartBeatsMessage heart = new HeartBeatsMessage() { Time = DateTime.Now };
        private async void SendHeartAsync()
        {
            int GetLast()
            {
                return (int)(DateTime.Now - Remote.LastReceiveTime).TotalMilliseconds;
            }

            var last = GetLast();
            while (last < 3000)
            {
                await Task.Delay(Math.Max(3000 - last,10));
                last = GetLast();
            }

            heart.Time = DateTime.Now;
            var (result, complete) = await Remote.RpcSendAsync<HeartBeatsMessage>(heart).WaitAsync(3000);

            if (complete)
            {
                var (resultMessage, exception) = result;
                switch (exception)
                {
                    case TimeoutException timeout:
                    case NullReferenceException nullReference:
                        var res = ReConnectAsync();
                        break;
                    default:
                        if (IsReConnect)
                        {
                            SendHeartAsync();
                        }
                        break;
                }
            }
            else
            {
                ///心跳丢失，开始重连
                var res = await ReConnectAsync();
                if (res)
                {
                    if (IsReConnect)
                    {
                        ///重连成功，继续心跳
                        SendHeartAsync();
                    }
                }
                else
                {
                    ///重连失败，断线
                    Remote.Disconnect(true);
                }
            }
            
        }

        async Task<bool> ReConnectAsync()
        {
            PreReConnect?.Invoke(this);
            var (result, complete) = await Remote.ConnectAsync(Remote.ConnectIPEndPoint,1).WaitAsync(ReConnectTime);
            if (complete&& result == null)
            {
                ReConnectSuccess?.Invoke(this);
                return true;
            }
            else
            {
                return false;
            }
        }

        public int ReConnectTime { get; set; } = 10000;

        public event Action<IReConnectable> PreReConnect;
        public event Action<IReConnectable> ReConnectSuccess;



    }

    /// <summary>
    /// 心跳包消息
    /// </summary>
    public class HeartBeatsMessage
    {
        static HeartBeatsMessage()
        {
            MessageLUT.AddFormatter<HeartBeatsMessage>(
                FrameworkConst.HeartbeatsMessageID, 
                Seiralizer, Deserilizer, KeyAlreadyHave.ThrowException);
        }

        /// <summary>
        /// 发送时间，服务器收到心跳包原样返回；
        /// </summary>
        public DateTime Time { get; set; }

        public static ushort Seiralizer(HeartBeatsMessage heartBeats, ref byte[] buffer)
        {
            BitConverter.GetBytes(heartBeats.Time.ToBinary()).CopyTo(buffer, 0);
            return sizeof(long);
        }

        public static dynamic Deserilizer(ArraySegment<byte> buffer)
        {
            long t = BitConverter.ToInt64(buffer.Array, buffer.Offset);
            return new HeartBeatsMessage() { Time = new DateTime(t) };
        }
    }
}
