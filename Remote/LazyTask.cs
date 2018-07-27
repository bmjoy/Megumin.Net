using System;
using System.Collections.Generic;
using System.Text;
using Network.Remote;
using System.Collections.Concurrent;

namespace MMONET.Remote
{
    /// <summary>
    /// 一个异步任务实现，特点是可以取消任务不会触发异常和后续方法。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class LazyTask<T> : ICanAwaitable<T>,IPoolElement
    {
        static ConcurrentQueue<LazyTask<T>> pool = new ConcurrentQueue<LazyTask<T>>();
        public static LazyTask<T> Pop()
        {
            if (pool.TryDequeue(out var task))
            {
                if (task != null)
                {
                    task.InPool = false;
                    return task;
                }
            }

            return new LazyTask<T>();
        }

        public static void ClearPool()
        {
            lock (pool)
            {
                while (pool.Count > 0)
                {
                    pool.TryDequeue(out var task);
                }
            }
        }

        bool InPool = false;

        private Action continuation;
        public bool IsCompleted { get; protected set; } = false;
        public T Result { get; protected set; }

        public void UnsafeOnCompleted(Action continuation)
        {
            if (InPool)
            {
                throw new ArgumentException($"{nameof(LazyTask<T>)}任务冲突，底层错误，请联系框架作者");
            }
            this.continuation -= continuation;
            this.continuation += continuation;
        }

        public void OnCompleted(Action continuation)
        {
            this.continuation -= continuation;
            this.continuation += continuation;
        }

        public void SetResult(T result)
        {
            if (InPool)
            {
                throw new InvalidOperationException($"任务不存在");
            }
            this.Result = result;
            IsCompleted = true;
            continuation?.Invoke();
            ///处理后续方法结束，归还到池中
            ((IPoolElement)this).Push2Pool();
        }

        public void CancelWithNotExceptionAndContinuation()
        {
            ((IPoolElement)this).Push2Pool();
        }

        void IPoolElement.Push2Pool()
        {
            Reset();

            if (!InPool)
            {
                if (pool.Count < 150)
                {
                    pool.Enqueue(this);
                    InPool = true;
                }
            }
        }

        void Reset()
        {
            IsCompleted = false;
            Result = default;
            continuation = null;
        }
    }
}
