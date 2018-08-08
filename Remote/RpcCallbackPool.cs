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
        /// 默认30000ms
        /// </summary>
        public int RpcTimeOutMilliseconds { get; set; } = 30000;
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
            short key = (short)(rpcID * -1);

            if (TryDequeue(key, out var callback))
            {
                ///如果出现RpcID冲突，认为前一个已经超时。
                callback.rpcCallback?.Invoke(null, new TimeoutException());
            }

            lock (this)
            {
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
                                    source.SetResult((default, new NullReferenceException()));
                                }
                                else
                                {
                                    ///转换类型错误
                                    source.SetResult((default, new InvalidCastException($"返回类型错误，无法转换")));
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

            CreateCheckTimeout(key);

            return (rpcID, source.Task);
        }

        public (short rpcID, ILazyAwaitable<RpcResult> source) Regist<RpcResult>(Action<Exception> OnException)
        {
            short rpcID = GetRpcID();
            ILazyAwaitable<RpcResult> source = LazyTask<RpcResult>.Pop();
            short key = (short)(rpcID * -1);
            if (TryDequeue(key, out var callback))
            {
                ///如果出现RpcID冲突，认为前一个已经超时。
                callback.rpcCallback?.Invoke(null, new TimeoutException());
            }

            lock (this)
            {
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
                                source.CancelWithNotExceptionAndContinuation();
                                if (resp == null)
                                {
                                    OnException?.Invoke(new NullReferenceException());
                                }
                                else
                                {
                                    ///转换类型错误
                                    OnException?.Invoke(new InvalidCastException($"返回类型错误，无法转换"));
                                }
                            }
                        }
                        else
                        {
                            source.CancelWithNotExceptionAndContinuation();
                            OnException?.Invoke(ex);
                        }
                    }
                );
            }

            CreateCheckTimeout(key);

            return (rpcID, source);
        }

        /// <summary>
        /// 创建超时检查
        /// </summary>
        /// <param name="rpcID"></param>
        void CreateCheckTimeout(short rpcID)
        {
            ///超时检查
            Task.Run(async () =>
            {
                if (RpcTimeOutMilliseconds >= 0)
                {
                    await Task.Delay(RpcTimeOutMilliseconds);
                    if (TryDequeue(rpcID, out var rpc))
                    {
                        MainThreadScheduler.Invoke(() =>
                        {
                            rpc.rpcCallback?.Invoke(null, new TimeoutException());
                        });
                    }
                }
            });
        }

        public bool TryDequeue(short rpcID, out (DateTime startTime, Network.Remote.RpcCallback rpcCallback) rpc)
        {
            lock (this)
            {
                if (TryGetValue(rpcID, out rpc))
                {
                    Remove(rpcID);
                    return true;
                }
            }

            return false;
        }

        //[Obsolete]
        //public void UpdateRpcResult(double delta)
        //{
        //    lock (this)
        //    {
        //        ///检查Rpc超时
        //        this.RemoveAll(kv =>
        //        {
        //            var es = DateTime.Now - kv.Value.startTime;
        //            var istimeout = es.TotalMilliseconds > RpcTimeOutMilliseconds;
        //            if (istimeout)
        //            {
        //                kv.Value.rpcCallback?.Invoke(null, new TimeoutException());
        //            }
        //            return istimeout;
        //        });
        //    }
        //}

        void IRpcCallbackPool.Remove(short rpcID) => Remove(rpcID);

        public bool TrySetResult(short rpcID, dynamic msg)
        {
            return TryComplate(rpcID, msg, null);
        }

        public bool TrySetException(short rpcID, Exception exception)
        {
            return TryComplate(rpcID, null, exception);
        }

        bool TryComplate(short rpcID, dynamic msg,Exception exception)
        {
            ///rpc响应
            if (TryDequeue(rpcID, out var rpc))
            {
                rpc.rpcCallback?.Invoke(msg, exception);
                return true;
            }
            return false;
        }
    }
}
