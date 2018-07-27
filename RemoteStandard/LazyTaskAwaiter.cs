using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Network.Remote;

namespace Network.Remote
{
    /// <summary>
    /// 可异步等待的
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface ICanAwaitable<T>
    {
        bool IsCompleted { get; }
        T Result { get; }

        void UnsafeOnCompleted(Action continuation);
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

    public struct LazyTaskAwaiter<T> : ICriticalNotifyCompletion
    {
        private ICanAwaitable<T> CanAwaiter;

        public bool IsCompleted => CanAwaiter.IsCompleted;

        public T GetResult()
        {
            return CanAwaiter.Result;
        }

        public LazyTaskAwaiter(ICanAwaitable<T> canAwait)
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
    public static LazyTaskAwaiter<T> GetAwaiter<T>(this ICanAwaitable<T> canAwaitable)
    {
        return new LazyTaskAwaiter<T>(canAwaitable);
    }
}