using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
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

namespace MMONET
{
    public static class SpanByteEX_3451DB8C29134366946FF9D778779EEC
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteTo(this int num, Span<byte> span)
            => BinaryPrimitives.WriteInt32BigEndian(span, num);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteTo(this ushort num, Span<byte> span)
            => BinaryPrimitives.WriteUInt16BigEndian(span, num);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteTo(this short num, Span<byte> span)
            => BinaryPrimitives.WriteInt16BigEndian(span, num);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteTo(this long num, Span<byte> span)
            => BinaryPrimitives.WriteInt64BigEndian(span, num);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadInt(this ReadOnlySpan<byte> span)
            => BinaryPrimitives.ReadInt32BigEndian(span);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ReadUshort(this ReadOnlySpan<byte> span)
            => BinaryPrimitives.ReadUInt16BigEndian(span);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short ReadShort(this ReadOnlySpan<byte> span)
            => BinaryPrimitives.ReadInt16BigEndian(span);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ReadLong(this ReadOnlySpan<byte> span)
            => BinaryPrimitives.ReadInt64BigEndian(span);
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

            unsafe
            {
                var rl = stackalloc IntPtr[source.Count];
                int index = 0;
                lock (source)
                {
                    try
                    {
                        foreach (var item in source)
                        {
                            if (predicate(item))
                            {
                                rl[index] = (IntPtr)GCHandle.Alloc(item.Key);
                            }
                            else
                            {
                                rl[index] = IntPtr.Zero;
                            }
                            index++;
                        }

                        for (int i = 0; i < index; i++)
                        {
                            IntPtr intPtr = rl[i];
                            if (intPtr != IntPtr.Zero)
                            {
                                GCHandle handle = (GCHandle)intPtr;
                                source.Remove((K)handle.Target);
                            }
                        }
                    }
                    finally
                    {
                        for (int i = 0; i < index; i++)
                        {
                            IntPtr intPtr = rl[i];
                            if (intPtr != IntPtr.Zero)
                            {
                                GCHandle handle = (GCHandle)intPtr;
                                if (handle.IsAllocated)
                                {
                                    handle.Free();
                                }
                            }
                        }
                    }
                    
                }
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

namespace System.Net.Sockets
{
    public static class UDPClientEx_102F7D01C985465EB23822F83FDE9C75
    {
        public static Task<UdpReceiveResult_E74D> ReceiveAsync(this UdpClient udp, ArraySegment<byte> receiveBuffer)
        {
            return Task<UdpReceiveResult_E74D>.Factory.FromAsync(
                (callback, state) => ((UdpClient)state).BeginReceive(callback, state, receiveBuffer),
                asyncResult =>
                {
                    var client = (UdpClient)asyncResult.AsyncState;
                    IPEndPoint remoteEP = null;
                    int length = client.EndReceive2(asyncResult, ref remoteEP);
                    var resbuffer = new ArraySegment<byte>(receiveBuffer.Array, receiveBuffer.Offset, length);
                    return new UdpReceiveResult_E74D(resbuffer, remoteEP);
                },
                state: udp);
        }

        internal static class IPEndPointStatics_9931EFCAB48741B998C533DF851CB575
        {
            internal const int AnyPort = IPEndPoint.MinPort;
            internal static readonly IPEndPoint Any = new IPEndPoint(IPAddress.Any, AnyPort);
            internal static readonly IPEndPoint IPv6Any = new IPEndPoint(IPAddress.IPv6Any, AnyPort);
        }

        public static IAsyncResult BeginReceive(this UdpClient udp, AsyncCallback requestCallback, object state, ArraySegment<byte> buffer)
        {
            // Due to the nature of the ReceiveFrom() call and the ref parameter convention,
            // we need to cast an IPEndPoint to its base class EndPoint and cast it back down
            // to IPEndPoint.
            EndPoint tempRemoteEP;
            if (udp.Client.AddressFamily == AddressFamily.InterNetwork)
            {
                tempRemoteEP = IPEndPointStatics_9931EFCAB48741B998C533DF851CB575.Any;
            }
            else
            {
                tempRemoteEP = IPEndPointStatics_9931EFCAB48741B998C533DF851CB575.IPv6Any;
            }

            return udp.Client.BeginReceiveFrom(buffer.Array, buffer.Offset, buffer.Count, SocketFlags.None, ref tempRemoteEP, requestCallback, state);
        }

        static int EndReceive2(this UdpClient udp, IAsyncResult asyncResult, ref IPEndPoint remoteEP)
        {
            EndPoint tempRemoteEP;
            if (udp.Client.AddressFamily == AddressFamily.InterNetwork)
            {
                tempRemoteEP = IPEndPointStatics_9931EFCAB48741B998C533DF851CB575.Any;
            }
            else
            {
                tempRemoteEP = IPEndPointStatics_9931EFCAB48741B998C533DF851CB575.IPv6Any;
            }

            int received = udp.Client.EndReceiveFrom(asyncResult, ref tempRemoteEP);
            remoteEP = (IPEndPoint)tempRemoteEP; 

            return received;
        }
    }

    /// <summary>
    /// https://source.dot.net/#System.Net.Sockets/System/Net/Sockets/UdpReceiveResult.cs,3adcfc441b5a5fd9
    /// Presents UDP receive result information from a call to the <see cref="UDPClientEx_102F7D01C985465EB23822F83FDE9C75.ReceiveAsync(UdpClient, ArraySegment{byte})"/> method
    /// </summary>
    public struct UdpReceiveResult_E74D : IEquatable<UdpReceiveResult_E74D>
    {
        private ArraySegment<byte> _buffer;
        private IPEndPoint _remoteEndPoint;

        /// <summary>
        /// Initializes a new instance of the <see cref="UdpReceiveResult"/> class
        /// </summary>
        /// <param name="buffer">A buffer for data to receive in the UDP packet</param>
        /// <param name="remoteEndPoint">The remote endpoint of the UDP packet</param>
        public UdpReceiveResult_E74D(ArraySegment<byte> buffer, IPEndPoint remoteEndPoint)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (remoteEndPoint == null)
            {
                throw new ArgumentNullException(nameof(remoteEndPoint));
            }

            _buffer = buffer;
            _remoteEndPoint = remoteEndPoint;
        }

        /// <summary>
        /// Gets a buffer with the data received in the UDP packet
        /// </summary>
        public ArraySegment<byte> Buffer
        {
            get
            {
                return _buffer;
            }
        }

        /// <summary>
        /// Gets the remote endpoint from which the UDP packet was received
        /// </summary>
        public IPEndPoint RemoteEndPoint
        {
            get
            {
                return _remoteEndPoint;
            }
        }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>The hash code</returns>
        public override int GetHashCode()
        {
            return (_buffer != null) ? (_buffer.GetHashCode() ^ _remoteEndPoint.GetHashCode()) : 0;
        }

        /// <summary>
        /// Returns a value that indicates whether this instance is equal to a specified object
        /// </summary>
        /// <param name="obj">The object to compare with this instance</param>
        /// <returns>true if obj is an instance of <see cref="UdpReceiveResult"/> and equals the value of the instance; otherwise, false</returns>
        public override bool Equals(object obj)
        {
            if (!(obj is UdpReceiveResult))
            {
                return false;
            }

            return Equals((UdpReceiveResult)obj);
        }

        /// <summary>
        /// Returns a value that indicates whether this instance is equal to a specified object
        /// </summary>
        /// <param name="other">The object to compare with this instance</param>
        /// <returns>true if other is an instance of <see cref="UdpReceiveResult"/> and equals the value of the instance; otherwise, false</returns>
        public bool Equals(UdpReceiveResult_E74D other)
        {
            return object.Equals(_buffer, other._buffer) && object.Equals(_remoteEndPoint, other._remoteEndPoint);
        }

        /// <summary>
        /// Tests whether two specified <see cref="UdpReceiveResult"/> instances are equivalent
        /// </summary>
        /// <param name="left">The <see cref="UdpReceiveResult"/> instance that is to the left of the equality operator</param>
        /// <param name="right">The <see cref="UdpReceiveResult"/> instance that is to the right of the equality operator</param>
        /// <returns>true if left and right are equal; otherwise, false</returns>
        public static bool operator ==(UdpReceiveResult_E74D left, UdpReceiveResult_E74D right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Tests whether two specified <see cref="UdpReceiveResult"/> instances are not equal
        /// </summary>
        /// <param name="left">The <see cref="UdpReceiveResult"/> instance that is to the left of the not equal operator</param>
        /// <param name="right">The <see cref="UdpReceiveResult"/> instance that is to the right of the not equal operator</param>
        /// <returns>true if left and right are unequal; otherwise, false</returns>
        public static bool operator !=(UdpReceiveResult_E74D left, UdpReceiveResult_E74D right)
        {
            return !left.Equals(right);
        }
    }
}