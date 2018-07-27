using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace MMONET
{
    public static class ListPool<T>
    {
        static ConcurrentQueue<List<T>> pool = new ConcurrentQueue<List<T>>();

        public static List<T> Pop()
        {
            if (pool.TryDequeue(out var list))
            {
                return list;
            }
            else
            {
                return new List<T>();
            }
        }

        public static void Push(List<T> list)
        {
            if (list == null)
            {
                return;
            }
            list.Clear();
            pool.Enqueue(list);
        }

        public static void Clear()
        {
            while (pool.Count > 0)
            {
                pool.TryDequeue(out var list);
            }
        }

    }

    /// <summary>
    /// 池元素
    /// </summary>
    public interface IPoolElement
    {
        /// <summary>
        /// 返回对象池中
        /// </summary>
        void Push2Pool();
    }
}
