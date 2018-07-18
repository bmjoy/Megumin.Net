using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Network.Remote;
using MMONET.Message;

namespace MMONET.Remote
{
    /// <summary>
    /// <para></para>
    /// <para>数据在网络上传输的时候，是以“帧”为单位的，帧最大为1518个字节，最小为64字节。</para>
    /// </summary>
    public abstract partial class RemoteBase : ISendMessage, IReceiveMessage, IConnect, 
        INetRemote,IRemote
    {
        static RemoteBase()
        {
            MainThreadScheduler.Add(StaticUpdate);
        }
        /// <summary>
        /// remote 在构造函数中自动添加到RemoteDic 中,需要手动移除 或者调用<see cref="Disconnect(bool)"/> + <see cref="MainThreadScheduler.Update(double)"/>移除。
        /// </summary>
        static readonly Dictionary<int, RemoteBase> remoteDic = new Dictionary<int, RemoteBase>();
        /// <summary>
        /// 添加队列，防止多线程阻塞
        /// </summary>
        static readonly ConcurrentQueue<RemoteBase> tempAddQ = new ConcurrentQueue<RemoteBase>();

        public static void Add(RemoteBase remote)
        {
            tempAddQ.Enqueue(remote);
        }

        public static bool TryGet(int Guid,out RemoteBase remote)
        {
            return remoteDic.TryGetValue(Guid, out remote);
        }

        /// <summary>
        /// 
        /// </summary>
        public static readonly CoolDownTime UpdateDelta = new CoolDownTime();

        static void StaticUpdate(double delta)
        {
            if (UpdateDelta.CoolDown)
            {
                lock (remoteDic)
                {
                    foreach (var item in remoteDic)
                    {
                        item.Value.Update(delta);
                    }
                }

                while (tempAddQ.Count > 0)
                {
                    if (tempAddQ.TryDequeue(out var remote))
                    {
                        if (remote != null)
                        {
                            if (remoteDic.ContainsKey(remote.Guid))
                            {
                                ///理论上不会冲突
                                Console.WriteLine($"remoteDic 键值冲突");
                            }
                            remoteDic[remote.Guid] = remote;
                        }
                    }
                }

                ///移除释放的连接
                remoteDic.RemoveAll(r => !r.Value.IsVaild);
            }
        }

        public Socket Socket { get; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="socket"></param>
        protected RemoteBase(Socket socket)
        {
            this.Socket = socket;
            ///断开连接将remote设置为无效
            onDisConnect += (er) => { IsVaild = false; };
            Add(this);
        }

        public bool Connected
        {
            get
            {
                return Socket?.Connected ?? false;
            }
        }

        /// <summary>
        /// RemoteBase类型中的唯一ID。
        /// </summary>
        public int Guid { get; } = InterlockedID<RemoteBase>.NewID();
        public int InstanceID { get; set; }
        public IPAddress Address { get; set; }
        public int Port { get; set; }
        /// <summary>
        /// 如果为false <see cref="MainThreadScheduler.Update(double)"/>会检测从<see cref="RemoteDic"/>中移除
        /// </summary>
        public bool IsVaild { get; protected set; } = true;

        protected virtual void Update(double delta)
        {
            lock (rpcCallbackPool)
            {
                ///检查Rpc超时
                rpcCallbackPool.RemoveAll(kv =>
                {
                    var es = DateTime.Now - kv.Value.StartTime;
                    var istimeout = es.TotalSeconds > RpcTimeOut;
                    if (istimeout)
                    {
                        kv.Value.RpcCallback?.Invoke(null, new TimeoutException());
                    }
                    return istimeout;
                });
            }
        }

        #region RPC

        short rpcCursor = 0;
        readonly object rpcCursorLock = new object();
        /// <summary>
        /// 默认30s
        /// </summary>
        public double RpcTimeOut { get; set; } = 30;
        delegate void RpcCallback(dynamic message, Exception exception);
        /// <summary>
        /// 每个session大约每秒30个包，超时时间为30秒；
        /// </summary>
        readonly Dictionary<short, (DateTime StartTime, RpcCallback RpcCallback)> rpcCallbackPool
            = new Dictionary<short, (DateTime StartTime, RpcCallback RpcCallback)>(31);

        /// <summary>
        /// 原子操作 取得RpcId,发送方的的RpcID为1~32767，回复的RpcID为-1~-32767，正负一一对应
        /// <para>0,-32768 为无效值</para>
        /// 最多同时维持32767个Rpc调用
        /// </summary>
        /// <returns></returns>
        short GetRpcID()
        {
            lock (rpcCursorLock)
            {
                if (rpcCursor <= 0 || rpcCursor == short.MaxValue)
                {
                    rpcCursor = 1;
                }
                else
                {
                    rpcCursor++;
                }

                return rpcCursor;
            }
        }

        #endregion

        #region Connect

        protected Action<SocketError> onDisConnect;
        public event Action<SocketError> OnDisConnect
        {
            add
            {
                onDisConnect += value;
            }
            remove
            {
                onDisConnect -= value;
            }
        }

        public void Disconnect(bool triggerOnDisConnectEvent)
        {
            IsVaild = false;
            if (Connected)
            {
                Socket.Shutdown(SocketShutdown.Both);
                Socket.Disconnect(false);
                Socket.Dispose();
            }

            if (triggerOnDisConnectEvent)
            {
                onDisConnect?.Invoke(SocketError.Disconnecting);
            }
        }

        bool isConnecting = false;
        async Task<Exception> ConnectAsync()
        {
            if (isConnecting)
            {
                return new Exception("连接正在进行中");
            }

            isConnecting = true;
            Exception exception = null;
            await Task.Run(() =>
            {
                try
                {
                    Socket.Connect(Address, Port);
                }
                catch (Exception e)
                {
                    exception = e;
                }
            });

            isConnecting = false;
            return exception;
        }

        public async ValueTask<Exception> ConnectAsync(IPAddress address, int port, int retryCount = 0)
        {
            this.Address = address;
            this.Port = port;
            while (retryCount >= 0)
            {
                var ex = await ConnectAsync();
                if (ex == null)
                {
                    return null;
                }
                else
                {
                    if (retryCount <= 0)
                    {
                        return ex;
                    }
                    else
                    {
                        retryCount--;
                    }
                }
            }

            return new NullReferenceException();
        }

        #endregion

        #region Send

        public bool IsSending { get; protected set; }

        public virtual Task<(RpcResult Result, Exception Excption)> RpcSendAsync<RpcResult>(dynamic message)
        {
            if (!IsReceiving)
            {
                Receive();
            }

            short rpcID = GetRpcID();

            SendAsync(rpcID, message);

            TaskCompletionSource<(RpcResult Result, Exception Excption)> source
                = new TaskCompletionSource<(RpcResult Result, Exception Excption)>();
            lock (rpcCallbackPool)
            {
                short key = (short)(rpcID * -1);
                if (rpcCallbackPool.ContainsKey(key))
                {
                    ///如果出现RpcID冲突，认为前一个已经超时。
                    var callback = rpcCallbackPool[key];
                    rpcCallbackPool.Remove(key);
                    callback.RpcCallback?.Invoke(null, new TimeoutException());
                }

                rpcCallbackPool[key] = (DateTime.Now,
                    (resp, ex) =>
                    {
                        if (ex == null)
                        {
                            if (resp is RpcResult result)
                            {
                                source.SetResult((result,null));
                            }
                            else
                            {
                                if (resp == null)
                                {
                                    source.SetResult((resp, new NullReferenceException()));
                                }
                                else
                                {
                                    ///返回类型错误
                                    source.SetResult((resp, new ArgumentException($"返回类型错误，无法识别")));
                                }

                            }
                        }
                        else
                        {
                            source.SetResult((resp, ex));
                        }

                        //todo
                        //source 回收
                    }
                );
            }

            return source.Task;
        }


        /// <summary>
        /// <see cref="ISendMessage.SafeRpcSendAsync{RpcResult}(dynamic, Action{Exception})"/>
        /// </summary>
        /// <typeparam name="RpcResult"></typeparam>
        /// <param name="message"></param>
        /// <param name="OnException"></param>
        /// <returns></returns>
        public virtual Task<RpcResult> SafeRpcSendAsync<RpcResult>(dynamic message, Action<Exception> OnException = null)
        {
            if (!IsReceiving)
            {
                Receive();
            }

            short rpcID = GetRpcID();

            SendAsync(rpcID, message);

            TaskCompletionSource<RpcResult> source = new TaskCompletionSource<RpcResult>();

            lock (rpcCallbackPool)
            {
                short key = (short)(rpcID * -1);
                if (rpcCallbackPool.ContainsKey(key))
                {
                    ///如果出现RpcID冲突，认为前一个已经超时。
                    var callback = rpcCallbackPool[key];
                    rpcCallbackPool.Remove(key);
                    callback.RpcCallback?.Invoke(null, new TimeoutException());
                }

                rpcCallbackPool[key] = (DateTime.Now,
                    (resp, ex) =>
                    {
                        if (ex == null)
                        {
                            if (resp is RpcResult result)
                            {
                                source.SetResult(result);
                            }
                            else
                            {
                                if (resp == null)
                                {
                                    OnException?.Invoke(new NullReferenceException());
                                }
                                else
                                {
                                    ///返回类型错误
                                    OnException?.Invoke(new ArgumentException($"返回类型错误，无法识别"));
                                }
                            }
                        }
                        else
                        {
                            OnException?.Invoke(ex);
                        }

                        //todo
                        //source 回收
                    }
                );
            }

            

            return source.Task;
        }

        public virtual void SendAsync<T>(T message)
        {
            SendAsync(0, message);
        }

        /// <summary>
        /// 异步发送
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="rpcID"></param>
        /// <param name="message"></param>
        protected abstract void SendAsync<T>(short rpcID, T message);

        #endregion

        #region Receive

        public abstract int ReceiveBufferSize { get; }
        public bool IsReceiving { get; protected set; }

        /// <summary>
        /// 接受消息的回调函数
        /// </summary>
        protected OnReceiveMessage onReceive;

        /// <summary>
        /// 异步接受消息包
        /// <para>1.remote收到消息大包（拼合的小包组）</para>
        /// <para>2.remote 调用 <see cref="MessagePool.PushReceivePacket(IReceivedPacket, INetRemote)"/></para>
        /// <para>消息大包和remote一起放入接收消息池<see cref="MessagePool"/>（这一环节为了切换执行异步方法后续的线程）</para>
        /// <para>3.（主线程）<see cref="MainThreadScheduler.Update(double)"/>时统一从池中取出消息，反序列化。
        ///          每个小包是一个消息，由remote <see cref="INetRemote.ReceiveCallback"/>>处理</para>
        /// <para>4.1 检查RpcID(内置不可见) 如果是Rpc结果，触发异步方法后续。如果rpc已经超时，消息被直接丢弃</para>
        /// <para>4.2 不是Rpc结果 则remote调用<paramref name="onReceive"/>回调函数(当前方法参数)处理消息</para>
        /// </summary>
        /// <param name="onReceive">处理消息方法，如果远端为RPC调用，那么应该返回一个合适的结果，否则返回null</param>
        public void Receive(OnReceiveMessage onReceive)
        {
            this.onReceive = onReceive;

            Receive();
        }

        protected abstract void Receive();

        #endregion

        async void INetRemote.ReceiveCallback(int messageID, short rpcID, dynamic msg)
        {
            if (rpcID == 0 || rpcID == short.MinValue)
            {
                if (onReceive == null)
                {
                    return;
                }
                ///这个消息是非Rpc请求
                ///普通响应onRely
                var response = await onReceive(msg);

                if (response is Task<dynamic> task)
                {
                    response = await task ?? null;
                }

                if (response is ValueTask<dynamic> vtask)
                {
                    response = await vtask ?? null;
                }

                if (response == null)
                {
                    return;
                }
                /// 普通返回
                SendAsync(response);
            }
            else if (rpcID > 0)
            {
                if (onReceive == null)
                {
                    return;
                }

                ///这个消息rpc的请求 
                ///普通响应onRely
                var response = await onReceive(msg);
                if (response is Task<object> task)
                {
                    response = await task ?? null;
                }

                if (response is ValueTask<object> vtask)
                {
                    response = await vtask ?? null;
                }

                if (response == null)
                {
                    return;
                }
                ///rpc的返回 
                SendAsync((short)(rpcID * -1),response);
            }
            else
            {
                ///这个消息是rpc返回（回复的RpcID为-1~-32767）
                ///rpc响应
                if (rpcCallbackPool.ContainsKey(rpcID))
                {
                    rpcCallbackPool[rpcID].RpcCallback?.Invoke(msg,null);
                    rpcCallbackPool.Remove(rpcID);
                }

                ///无返回
            }
        }


        #region BroadCast

        /// <summary>
        /// 广播
        /// </summary>
        /// <param name="message"></param>
        /// <param name="remotes"></param>
        public static void BroadCastAsync<T>(T message, params RemoteBase[] remotes)
        {
            BroadCastAsync(message, remotes as IEnumerable<RemoteBase>);
        }

        public static void BroadCastAsync<T>(T message, IEnumerable<RemoteBase> remotes)
        {

            var msgBuffer = MessageLUT.Serialize(0, message);

            ///这里需要测试
            Task.Run(() =>
            {
                Parallel.ForEach(remotes,
                async (item) =>
                {
                    //(Action<INetRemote>)
                    await item?.BroadCastSendAsync(msgBuffer);
                });
            }).ContinueWith((t) =>
            {
                ///这里需要测试
                BufferPool.Push(msgBuffer.Array);
            });
        }

        Task BroadCastSendAsync(ArraySegment<byte> msgBuffer)
        {
            return Task.Run(() =>
            {
                if (Connected)
                {
                    Socket.Send(msgBuffer.Array, msgBuffer.Offset, msgBuffer.Count, SocketFlags.None);
                }
            });
        }

        #endregion
    }

}
