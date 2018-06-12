using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace MMONET.Sockets
{
    /// <summary>
    /// Socket封装
    /// </summary>
    public interface IRemote
    {
        RemoteChannal ChannalType { get; }
        int InstanceID { get; set; }
        bool IsAnyChannelConnected { get; }
        bool IsTcpSending { get; }
        bool IsTcpSocketReceiving { get; }
        int ReceiveBufferSize { get; }
        IPAddress RemoteAddress { get; set; }
        int RemotePort { get; set; }

        event Action<RemoteChannal, SocketError> OnDisConnect;

        /// <summary>
        /// <para>异常在底层Task过程中捕获，返回值null表示成功，调用者不必写try catch</para>
        /// </summary>
        /// <param name="address"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        ValueTask<Exception> ConnectAsync(IPAddress address, int port);
        /// <summary>
        /// <para>异常在底层Task过程中捕获，返回值null表示成功，调用者不必写try catch</para>
        /// </summary>
        /// <param name="address"></param>
        /// <param name="port"></param>
        /// <param name="retryCount">重试次数，失败会返回最后一次的异常</param>
        /// <returns></returns>
        ValueTask<Exception> ConnectAsync(IPAddress address, int port, int retryCount);
        /// <summary>
        /// 主动断开连接
        /// <param name="channal"></param>
        /// <param name="triggerOnDisConnectEvent"></param>
        void Disconnect(RemoteChannal channal = RemoteChannal.TCP | RemoteChannal.KCP | RemoteChannal.UDP, bool triggerOnDisConnectEvent = true);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="mainPort"></param>
        /// <returns></returns>
        Task<IRemote> ListenAsync(int mainPort);
        /// <summary>
        /// 异步接受消息包
        /// </summary>
        /// <param name="onReceive"></param>
        void ReceiveAsync(OnReceiveMessage onReceive);
        /// <summary>
        /// 异步发送消息，封装Rpc过程
        /// <para>只要你调用了接收函数，即使回调函数为空，RPC过程的消息仍能正确处理。</para>
        /// </summary>
        /// <typeparam name="RpcResult">期待的Rpc结果类型，如果收到返回类型，但是类型不匹配，返回null</typeparam>
        /// <param name="message">发送消息的类型需要序列化 查找表<see cref="ILookUpTabal"/> 中指定ID和序列化函数</param>
        /// <param name="type"></param>
        /// <returns></returns>
        Task<(RpcResult Result, Exception Excption)> RpcSendAsync<RpcResult>(dynamic message, RemoteChannal type = RemoteChannal.TCP);
        /// <summary>
        /// 发送消息，无阻塞立刻返回
        /// <para>调用方 无法了解发送情况</para>
        /// 序列化过程同步执行，方法返回表示序列化已结束，修改message内容不影响发送数据。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="message"></param>
        /// <param name="type"></param>
        void Send<T>(T message, RemoteChannal type = RemoteChannal.TCP);
    }

    /// <summary>
    /// Remote 支持的协议
    /// </summary>
    [Flags]
    public enum RemoteChannal
    {
        None = 0,
        TCP = 1 << 0,
        KCP = 1 << 1,
        UDP = 1 << 2,
    }

    /// <summary>
    /// 处理收到消息委托
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    public delegate ValueTask<dynamic> OnReceiveMessage(dynamic message);
}