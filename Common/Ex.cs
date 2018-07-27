using System;
using System.Collections.Generic;
using System.Linq;
using MMONET;

namespace System
{
    public static class Extention_7AE0B2E4B4124A53AE87CE8D95431431
    {
        /// <summary>
        /// 尝试取得第一个指定属性
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="type"></param>
        /// <returns></returns>
        public static T GetFirstCustomAttribute<T>(this Type type)
            where T : Attribute
        {
            return type.GetCustomAttributes(typeof(T), true).FirstOrDefault() as T;
        }
    }
}

namespace System.Threading.Tasks
{
    public static class Task_49A548505C7242BEBD1AD43D876BC1B0
    {
        public async static Task<(T result, bool complete)> WaitAsync<T>(this Task<T> task, int millisecondsTimeout)
        {
            var complete = await Task.Run(() => task.Wait(millisecondsTimeout));
            return (complete ? task.Result : default, complete);
        }

        public async static Task<bool> WaitAsync(this Task task, int millisecondsTimeout)
        {
            return await Task.Run(() => task.Wait(millisecondsTimeout));
        }
    }
}

namespace System.Collections.Generic
{
    public static class DicEx_991308D5D27E43A98301E51BEF820AB3
    {
        public static void RemoveAll<K, V>(this IDictionary<K, V> source, Func<KeyValuePair<K, V>, bool> predicate)
        {
            if (predicate == null || source == null)
            {
                return;
            }

            #region Obsolete

            /////方案一
            //if (source.Count > 32)
            //{
            //    ///时间复杂度O(1) ，有GC
            //    var rDic = source.Where(predicate).ToArray();
            //    foreach (var item in rDic)
            //    {
            //        source.Remove(item.Key);
            //    }
            //}
            //else
            //{
            //    ///效率略高于elementAt 无GC 时间复杂度O(n)

            //    ///检查的元素个数
            //    int checkCount = 0;

            //    while (checkCount < source.Count)
            //    {
            //        using (var e = source.GetEnumerator())
            //        {
            //            ///跳过已经检查的元素
            //            for (int i = 0 ; i < checkCount ; i++)
            //            {
            //                e.MoveNext();
            //            }

            //            while (true)
            //            {
            //                if (e.MoveNext())
            //                {
            //                    if (predicate(e.Current))
            //                    {
            //                        ///符合条件 删除元素，迭代器失效
            //                        source.Remove(e.Current.Key);
            //                        break;
            //                    }
            //                }
            //                else
            //                {
            //                    break;
            //                }

            //                checkCount++;
            //            }
            //        }
            //    }
            //}

            /////方案二
            //unsafe
            //{
            //    var rList = stackalloc K[source.Count];
            //    int index = 0;
            //    foreach (var item in source)
            //    {
            //        if (predicate(item))
            //        {
            //            rList[index] = item.Key;
            //        }
            //        index++;
            //    }

            //    foreach (var item in rList)
            //    {
            //        source.Remove(item);
            //    }

            //}

            #endregion

            List<K> klist = null;
            lock (source)
            {
                foreach (var item in source)
                {
                    if (predicate(item))
                    {
                        if (klist == null)
                        {
                            klist = ListPool<K>.Pop();
                        }
                        klist.Add(item.Key);
                    }
                }
            }

            if (klist != null)
            {
                foreach (var item in klist)
                {
                    source.Remove(item);
                }

                ListPool<K>.Push(klist);
            }
        }


        public static void RemoveAll<V>(this IDictionary<int, V> source, Func<KeyValuePair<int, V>, bool> predicate)
        {
            if (predicate == null || source == null)
            {
                return;
            }

            unsafe
            {
                var rList = stackalloc int[source.Count];
                int index = 0;

                lock (source)
                {
                    foreach (var item in source)
                    {
                        if (predicate(item))
                        {
                            rList[index] = item.Key;
                            index++;
                        }
                    }

                    for (int i = 0; i < index; i++)
                    {
                        source.Remove(rList[i]);
                    }
                }
            }
        }
    }
}