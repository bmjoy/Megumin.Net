using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace MMONET.Sockets
{
    /// <summary>
    /// 末端
    /// </summary>
    public interface IEndPoint
    {
        /// <summary>
        /// 
        /// </summary>
        IPAddress Address { get; set; }
        /// <summary>
        /// 
        /// </summary>
        int Port { get; set; }
    }

    /// <summary>
    /// 连接
    /// </summary>
    public interface IConnect : IEndPoint
    {
        /// <summary>
        /// 断开连接事件
        /// </summary>
        event Action<SocketError> OnDisConnect;
        /// <summary>
        /// <para>异常在底层Task过程中捕获，返回值null表示成功，调用者不必写try catch</para>
        /// </summary>
        /// <param name="address"></param>
        /// <param name="port"></param>
        /// <param name="retryCount">重试次数，失败会返回最后一次的异常</param>
        /// <returns></returns>
        ValueTask<Exception> ConnectAsync(IPAddress address, int port, int retryCount = 0);
        /// <summary>
        /// 主动断开连接
        /// <param name="triggerOnDisConnectEvent">选则是否触发事件</param>
        /// </summary>
        void Disconnect(bool triggerOnDisConnectEvent = true);
    }

    /// <summary>
    /// <see cref="SendAsync{T}(T)"/>不会自动开始Receive，RpcSend会自动开始Receive。
    /// <para></para>
    /// 为什么使用dynamic 关键字而不是泛型？1.为了函数调用过程中更优雅。2.在序列化过程中，必须使用一次dynamic还原参数真实类型，所以省不掉。
    /// <para>dynamic导致值类型装箱是可以妥协的。</para>
    /// </summary>
    public interface ISendMessage
    {
        /// <summary>
        /// remote 是否在发送数据
        /// </summary>
        bool IsSending { get; }
        /// <summary>
        /// Rpc超时时间秒数
        /// </summary>
        double RpcTimeOut { get; set; }
        /// <summary>
        /// 发送消息，无阻塞立刻返回
        /// <para>调用方 无法了解发送情况</para>
        /// 序列化过程同步执行，方法返回表示序列化已结束，修改message内容不影响发送数据。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="message"></param>
        void SendAsync<T>(T message);
        /// <summary>
        /// 异步发送消息，封装Rpc过程
        /// <para>只要你调用了接收函数，即使回调函数为空，RPC过程的消息仍能正确处理。</para>
        /// </summary>
        /// <typeparam name="RpcResult">期待的Rpc结果类型，如果收到返回类型，但是类型不匹配，返回null</typeparam>
        /// <param name="message">发送消息的类型需要序列化 查找表<see cref="MessageLUT"/> 中指定ID和序列化函数</param>
        /// <returns>需要检测空值</returns>
        Task<(RpcResult Result, Exception Excption)> RpcSendAsync<RpcResult>(dynamic message);

        /// <summary>
        /// 异步发送消息，封装Rpc过程
        /// 结果值是保证有值的，如果结果值为空或其他异常,触发异常回调函数，异步方法的后续部分不会触发，所以后续部分可以省去空检查。
        /// </summary>
        /// <typeparam name="RpcResult"></typeparam>
        /// <param name="message"></param>
        /// <param name="OnException">发生异常时的回调函数</param>
        /// <returns></returns>
        Task<RpcResult> SafeRpcSendAsync<RpcResult>(dynamic message, Action<Exception> OnException = null);
    }

    /// <summary>
    /// 
    /// </summary>
    public interface INetRemote
    {
        /// <summary>
        /// 切换线程使用的回调
        /// </summary>
        /// <param name="messageID"></param>
        /// <param name="rpcID"></param>
        /// <param name="msg"></param>
        void ReceiveCallback(int messageID, short rpcID, dynamic msg);
    }

    /// <summary>
    /// 接收消息
    /// </summary>
    public interface IReceiveMessage
    {
        /// <summary>
        /// 接收缓冲区大小
        /// </summary>
        int ReceiveBufferSize { get; }
        /// <summary>
        /// 当前是否在接收
        /// </summary>
        bool IsReceiving { get; }

        /// <summary>
        /// 异步接受消息包
        /// <para>1.remote收到消息大包（拼合的小包组）</para>
        /// <para>2.remote 调用 <see cref="MessagePool.PushReceivePacket(IReceivedPacket, INetRemote)"/></para>
        /// <para>消息大包和remote一起放入接收消息池<see cref="MessagePool.receivePool"/>（这一环节为了切换执行异步方法后续的线程）</para>
        /// <para>3.（主线程）<see cref="MainThreadScheduler.Update(double)"/>时统一从池中取出消息，反序列化。
        ///          每个小包是一个消息，由remote <see cref="INetRemote.ReceiveCallback"/>>处理</para>
        /// <para>5.1 检查RpcID(内置不可见) 如果是Rpc结果，触发异步方法后续。如果rpc已经超时，消息被直接丢弃</para>
        /// <para>5.2 不是Rpc结果 则remote调用<paramref name="onReceive"/>回调函数(当前方法参数)处理消息</para>
        /// </summary>
        /// <param name="onReceive">处理消息方法，如果远端为RPC调用，那么应该返回一个合适的结果，否则返回null</param>
        void Receive(OnReceiveMessage onReceive);
    }

    /// <summary>
    /// Socket封装
    /// </summary>
    public interface IRemote : IEndPoint,ISendMessage,IReceiveMessage,IConnect
    {
        /// <summary>
        /// 预留给用户使用的ID，（用户自己赋值ID，自己管理引用，框架不做处理）
        /// </summary>
        int InstanceID { get; set; }
        /// <summary>
        /// 当前是否连接
        /// </summary>
        bool Connected { get; }
        /// <summary>
        /// 
        /// </summary>
        Socket Socket { get; }
        /// <summary>
        /// 
        /// </summary>
        bool IsVaild { get; }
    }

    /// <summary>
    /// 连接监听接口
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IRemoteListener<T> : IEndPoint
        where T: IRemote
    {
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        Task<T> ListenAsync();
    }

    /// <summary>
    /// 接收到的消息容器
    /// </summary>
    public interface IReceivedPacket : IPoolElement
    {
        Queue<(int messageID, short rpcID, ArraySegment<byte> body)> MessagePacket { get; }
    }

    /// <summary>
    /// 处理收到消息委托
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    public delegate ValueTask<dynamic> OnReceiveMessage(dynamic message);
}