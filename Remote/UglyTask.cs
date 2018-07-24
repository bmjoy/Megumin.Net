using System;
using System.Collections.Generic;
using System.Text;
using Network.Remote;
using System.Collections.Concurrent;

namespace MMONET.Remote
{
    public class UglyTask<T> : ICanAwaitable<T>,IPoolElement
    {
        static ConcurrentQueue<UglyTask<T>> pool = new ConcurrentQueue<UglyTask<T>>();
        public static UglyTask<T> Pop()
        {
            if (pool.TryDequeue(out var task))
            {
                if (task != null)
                {
                    task.InPool = false;
                    return task;
                }
            }

            return new UglyTask<T>();
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
                throw new ArgumentException($"{nameof(UglyTask<T>)}任务冲突，底层错误，请联系框架作者");
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
