using System;
using System.Collections.Generic;
using System.Text;
using Network.Remote;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace MMONET.Remote
{
    /// <summary>
    /// 一个异步任务实现，特点是可以取消任务不会触发异常和后续方法。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class LazyTask<T> : ILazyAwaitable<T>,IPoolElement
    {
        enum State
        {
            InPool,
            Waiting,
            Success,
            Faild,
        }

        public static int MaxCount { get; set; } = 512;

        static ConcurrentQueue<LazyTask<T>> pool = new ConcurrentQueue<LazyTask<T>>();
        public static LazyTask<T> Rent()
        {
            if (pool.TryDequeue(out var task))
            {
                if (task != null)
                {
                    task.state = State.Waiting;
                    return task;
                }
            }

            return new LazyTask<T>() { state = State.Waiting };
        }

        public static void ClearPool()
        {
            lock (pool)
            {
                while (pool.Count > 0)
                {
                    pool.TryDequeue(out var task);
                    task?.Reset();
                }
            }
        }

        State state = State.InPool;

        private Action continuation;
        /// <summary>
        /// 是否进入异步挂起阶段
        /// </summary>
        private bool alreadyEnterAsync = false;

        public bool IsCompleted => state == State.Success || state == State.Faild;
        public T Result { get; protected set; }

        public void UnsafeOnCompleted(Action continuation)
        {
            if (state == State.InPool)
            {
                throw new ArgumentException($"{nameof(LazyTask<T>)}任务冲突，底层错误，请联系框架作者");
            }

            alreadyEnterAsync = true;
            this.continuation -= continuation;
            this.continuation += continuation;
            TryComplete();
        }

        public void OnCompleted(Action continuation)
        {
            if (state == State.InPool)
            {
                throw new ArgumentException($"{nameof(LazyTask<T>)}任务冲突，底层错误，请联系框架作者");
            }

            alreadyEnterAsync = true;
            this.continuation -= continuation;
            this.continuation += continuation;
            TryComplete();
        }

        public void SetResult(T result)
        {
            if (state == State.InPool)
            {
                throw new InvalidOperationException($"任务不存在");
            }
            this.Result = result;
            state = State.Success;
            TryComplete();

        }

        private void TryComplete()
        {
            if (alreadyEnterAsync)
            {
                if (state == State.Waiting)
                {
                    return;
                }

                if (state == State.Success)
                {
                    continuation?.Invoke();
                }

                ///处理后续方法结束，归还到池中
                ((IPoolElement)this).Return();
            }
        }

        public void CancelWithNotExceptionAndContinuation()
        {
            if (state == State.InPool)
            {
                throw new InvalidOperationException($"任务不存在");
            }

            Result = default;
            state = State.Faild;
            TryComplete();
        }

        void IPoolElement.Return()
        {
            Reset();

            if (state != State.InPool)
            {
                if (pool.Count < MaxCount)
                {
                    pool.Enqueue(this);
                    state = State.InPool;

                }
            }
        }

        void Reset()
        {
            alreadyEnterAsync = false;
            Result = default;
            continuation = null;
        }
    }
}
