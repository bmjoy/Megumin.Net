using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace MMONET
{
    /// <summary>
    /// 线程安全ID生成器
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public static class InterlockedID<T>
    {
        static int id = 0;
        static readonly object locker = new object();
        public static int NewID(int min = 0)
        {
            lock (locker)
            {
                if (id < min)
                {
                    id = min;
                    return id;
                }

                return id++;
            }
        }
    }
}
