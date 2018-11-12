using Net.Remote;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Megumin.Remote
{


    /// <summary>
    /// Rpc回调注册池
    /// 每个session大约每秒30个包，超时时间默认为30秒；
    /// </summary>
    public class RpcCallbackPool : Dictionary<int, (DateTime startTime, RpcCallback rpcCallback)>, IRpcCallbackPool
    {
        int rpcCursor = 0;
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
        delegate void RpcCallback(object message, Exception exception);
        /// <summary>
        /// 原子操作 取得RpcId,发送方的的RpcID为1~32767，回复的RpcID为-1~-32767，正负一一对应
        /// <para>0,-32768 为无效值</para>
        /// 最多同时维持32767个Rpc调用
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int GetRpcID()
        {
            lock (rpcCursorLock)
            {
                if (rpcCursor <= 0 || rpcCursor == int.MaxValue)
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

        public (int rpcID, Task<(RpcResult result, Exception exception)> source) Regist<RpcResult>()
        {
            int rpcID = GetRpcID();

            TaskCompletionSource<(RpcResult Result, Exception Excption)> source
                = new TaskCompletionSource<(RpcResult Result, Exception Excption)>();
            short key = (short)(rpcID * -1);

            if (TryDequeue(key, out var callback))
            {
                ///如果出现RpcID冲突，认为前一个已经超时。
                callback.rpcCallback?.Invoke(null, new TimeoutException("RpcID 重叠，对前一个回调进行超时处理"));
            }

            lock (dequeueLock)
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
                                    source.SetResult((default, new InvalidCastException($"返回{resp.GetType()}类型，无法转换为{typeof(RpcResult)}")));
                                }

                            }
                        }
                        else
                        {
                            source.SetResult((default, ex));
                        }

                        //todo
                        //source 回收
                    }
                );
            }

            CreateCheckTimeout(key);

            return (rpcID, source.Task);
        }

        public (int rpcID, IMiniAwaitable<RpcResult> source) Regist<RpcResult>(Action<Exception> OnException)
        {
            int rpcID = GetRpcID();
            IMiniAwaitable<RpcResult> source = MiniTask<RpcResult>.Rent();
            short key = (short)(rpcID * -1);
            if (TryDequeue(key, out var callback))
            {
                ///如果出现RpcID冲突，认为前一个已经超时。
                callback.rpcCallback?.Invoke(null, new TimeoutException("RpcID 重叠，对前一个回调进行超时处理"));
            }

            lock (dequeueLock)
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
                                    OnException?.Invoke(new InvalidCastException($"返回{resp.GetType()}类型，无法转换为{typeof(RpcResult)}"));
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
                        ThreadScheduler.Invoke(() =>
                        {
                            rpc.rpcCallback?.Invoke(null, new TimeoutException($"RPC {rpcID} 回调超时，没有得到远端响应。"));
                        });
                    }
                }
            });
        }

        readonly object dequeueLock = new object();
        public bool TryDequeue(int rpcID, out (DateTime startTime, Net.Remote.RpcCallback rpcCallback) rpc)
        {
            lock (dequeueLock)
            {
                if (TryGetValue(rpcID, out rpc))
                {
                    Remove(rpcID);
                    return true;
                }
            }

            return false;
        }

        void IRpcCallbackPool.Remove(int rpcID) => Remove(rpcID);

        public bool TrySetResult(int rpcID, object msg)
        {
            return TryComplate(rpcID, msg, null);
        }

        public bool TrySetException(int rpcID, Exception exception)
        {
            return TryComplate(rpcID, null, exception);
        }

        bool TryComplate(int rpcID, object msg,Exception exception)
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
