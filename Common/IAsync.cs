using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace System.Threading.Tasks
{
    public interface IGetAwaiter
    {
        IAwaiterResult GetAwaiter();
    }

    public interface IAwaiterResult : ICriticalNotifyCompletion
    {
        bool IsCompleted { get; }
        void GetResult();
    }

    public interface IGetAwaiter<T>
    {
        IAwaiterResult<T> GetAwaiter();
    }

    public interface IAwaiterResult<T> : ICriticalNotifyCompletion
    {
        bool IsCompleted { get; }
        T GetResult();
    }

    public class FastAwaiter : IGetAwaiter
    {
        public IAwaiterResult GetAwaiter()
        {
            throw new NotImplementedException();
        }


    }
}
