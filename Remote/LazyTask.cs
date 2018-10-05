﻿using System;
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
        readonly object innerlock = new object();

        public void UnsafeOnCompleted(Action continuation)
        {
            lock (innerlock)
            {
                if (state == State.InPool)
                {
                    ///这里被触发一定是是类库BUG。
                    throw new ArgumentException($"{nameof(LazyTask<T>)}任务冲突，底层错误，请联系框架作者");
                }

                alreadyEnterAsync = true;
                this.continuation -= continuation;
                this.continuation += continuation;
                TryComplete(); 
            }
        }

        public void OnCompleted(Action continuation)
        {
            lock (innerlock)
            {
                if (state == State.InPool)
                {
                    ///这里被触发一定是是类库BUG。
                    throw new ArgumentException($"{nameof(LazyTask<T>)}任务冲突，底层错误，请联系框架作者");
                }

                alreadyEnterAsync = true;
                this.continuation -= continuation;
                this.continuation += continuation;
                TryComplete();
            }
        }

        public void SetResult(T result)
        {
            lock (innerlock)
            {
                if (state == State.InPool)
                {
                    throw new InvalidOperationException($"任务不存在");
                }
                this.Result = result;
                state = State.Success;
                TryComplete();
            }
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
            lock (innerlock)
            {
                if (state == State.InPool)
                {
                    throw new InvalidOperationException($"任务不存在");
                }

                Result = default;
                state = State.Faild;
                TryComplete();
            }
        }

#if DEBUG
        int lastThreadID;
#endif

        void IPoolElement.Return()
        {
            Reset();

            if (state != State.InPool)
            {
                ///state = State.InPool;必须在pool.Enqueue(this);之前。
                ///因为当pool为空的时候，放入池的元素会被立刻取出。并将状态设置为Waiting。
                ///如果state = State.InPool;在pool.Enqueue(this)后，那么会导致Waiting 状态被错误的设置为InPool;
                /// **** 我在这里花费了4个小时（sad）。
                state = State.InPool;

#if DEBUG
                lastThreadID = System.Threading.Thread.CurrentThread.ManagedThreadId;
#endif

                if (pool.Count < MaxCount)
                {
                    pool.Enqueue(this);
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
