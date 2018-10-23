using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Net.Remote;

namespace Net.Remote
{
    /// <summary>
    /// 可异步等待的
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IMiniAwaitable<T>
    {
        /// <summary>
        /// 
        /// </summary>
        bool IsCompleted { get; }
        /// <summary>
        /// 
        /// </summary>
        T Result { get; }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="continuation"></param>
        void UnsafeOnCompleted(Action continuation);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="continuation"></param>
        void OnCompleted(Action continuation);
        /// <summary>
        /// 通过设定结果值触发后续方法
        /// </summary>
        /// <param name="result"></param>
        void SetResult(T result);
        /// <summary>
        /// 通过此方法结束一个await 而不触发后续方法，也不触发异常，并释放所有资源
        /// </summary>
        void CancelWithNotExceptionAndContinuation();
    }

    public struct MiniTaskAwaiter<T> : ICriticalNotifyCompletion
    {
        private IMiniAwaitable<T> CanAwaiter;

        public bool IsCompleted => CanAwaiter.IsCompleted;

        public T GetResult()
        {
            return CanAwaiter.Result;
        }

        public MiniTaskAwaiter(IMiniAwaitable<T> canAwait)
        {
            this.CanAwaiter = canAwait;
        }
        public void UnsafeOnCompleted(Action continuation)
        {
            CanAwaiter.UnsafeOnCompleted(continuation);
        }

        public void OnCompleted(Action continuation)
        {
            CanAwaiter.OnCompleted(continuation);
        }
    }
}

public static class ICanAwaitableEx_D248AE7ECAD0420DAF1BCEA2801012FF
{
    public static MiniTaskAwaiter<T> GetAwaiter<T>(this IMiniAwaitable<T> canAwaitable)
    {
        return new MiniTaskAwaiter<T>(canAwaitable);
    }
}