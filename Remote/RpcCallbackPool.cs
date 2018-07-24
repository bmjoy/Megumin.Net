using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Network.Remote;

namespace MMONET.Remote
{


    /// <summary>
    /// Rpc回调注册池
    /// </summary>
    public class RpcCallbackPool : Dictionary<short, (DateTime startTime, RpcCallback rpcCallback)>, IRpcCallbackPool
    {
        short rpcCursor = 0;
        readonly object rpcCursorLock = new object();

        public RpcCallbackPool()
        {

        }

        public RpcCallbackPool(int capacity) : base(capacity)
        {
        }

        /// <summary>
        /// 默认30s
        /// </summary>
        public double RpcTimeOut { get; set; } = 30;
        delegate void RpcCallback(dynamic message, Exception exception);
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

        public (short rpcID, Task<(RpcResult result, Exception exception)> source) Regist<RpcResult>()
        {
            short rpcID = GetRpcID();

            TaskCompletionSource<(RpcResult Result, Exception Excption)> source
                = new TaskCompletionSource<(RpcResult Result, Exception Excption)>();
            lock (this)
            {
                short key = (short)(rpcID * -1);
                if (ContainsKey(key))
                {
                    ///如果出现RpcID冲突，认为前一个已经超时。
                    var callback = this[key];
                    Remove(key);
                    callback.rpcCallback?.Invoke(null, new TimeoutException());
                }

                this[key] = (DateTime.Now,
                    (resp, ex) =>
                    {
                        if (ex == null)
                        {
                            if (resp is RpcResult result)
                            {
                                source.SetResult((result, null));
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

            return (rpcID, source.Task);
        }

        public (short rpcID, Task<RpcResult> source) Regist<RpcResult>(Action<Exception> OnException)
        {
            short rpcID = GetRpcID();

            TaskCompletionSource<RpcResult> source = new TaskCompletionSource<RpcResult>();

            lock (this)
            {
                short key = (short)(rpcID * -1);
                if (this.ContainsKey(key))
                {
                    ///如果出现RpcID冲突，认为前一个已经超时。
                    var callback = this[key];
                    this.Remove(key);
                    callback.rpcCallback?.Invoke(null, new TimeoutException());
                }

                this[key] = (DateTime.Now,
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

            return (rpcID, source.Task);
        }

        public void UpdateRpcResult(double delta)
        {
            lock (this)
            {
                ///检查Rpc超时
                this.RemoveAll(kv =>
                {
                    var es = DateTime.Now - kv.Value.startTime;
                    var istimeout = es.TotalSeconds > RpcTimeOut;
                    if (istimeout)
                    {
                        kv.Value.rpcCallback?.Invoke(null, new TimeoutException());
                    }
                    return istimeout;
                });
            }
        }

        void IRpcCallbackPool.Remove(short rpcID) => Remove(rpcID);
    }
}
