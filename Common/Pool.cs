using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace MMONET
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public static class ListPool<T>
    {
        static ConcurrentQueue<List<T>> pool = new ConcurrentQueue<List<T>>();

        /// <summary>
        /// 默认容量3
        /// </summary>
        public static int MaxSize { get; set; } = 10;

        public static List<T> Rent()
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

        /// <summary>
        /// 调用者保证归还后不在使用当前list
        /// </summary>
        /// <param name="list"></param>
        public static void Return(List<T> list)
        {
            if (list == null)
            {
                return;
            }

            if (pool.Count < MaxSize)
            {
                list.Clear();
                pool.Enqueue(list);
            }
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
        void Return();
    }
}
