using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace MMONET.Message
{
    /// <summary>
    /// Key冲突改怎么做
    /// </summary>
    public enum KeyAlreadyHave
    {
        /// <summary>
        /// 替换
        /// </summary>
        Replace,
        /// <summary>
        /// 跳过
        /// </summary>
        Skip,
        /// <summary>
        /// 抛出异常
        /// </summary>
        ThrowException,
    }

    /// <summary>
    /// 消息查找表
    /// <seealso cref="Seiralizer{T}"/><seealso cref="Deserilizer"/>
    /// </summary>
    public class MessageLUT
    {

        static readonly Dictionary<int, Deserilizer> dFormatter = new Dictionary<int, Deserilizer>();
        static readonly Dictionary<Type, (int MessageID, Delegate Seiralizer)> sFormatter = new Dictionary<Type, (int MessageID, Delegate Seiralizer)>();
        ///序列化方法第二个参数必须为 byte[]
        static Type args2type = typeof(Span<byte>);

        protected static void AddSFormatter(Type type, int messageID, Delegate seiralizer, KeyAlreadyHave key = KeyAlreadyHave.Skip)
        {
            if (type == null || seiralizer == null)
            {
                throw new ArgumentNullException();
            }

            var args = seiralizer.Method.GetParameters();
            if (args.Length != 2)
            {
                throw new ArgumentException("序列化函数参数数量不匹配");
            }
            if (type != args[0].ParameterType && !type.IsSubclassOf(args[0].ParameterType))
            {
                throw new ArgumentException($"序列化参数1:类型不匹配,{type}不是{nameof(seiralizer)}的第一个参数类型或它的子类。");
            }

            if (args[1].ParameterType != args2type)
            {
                throw new ArgumentException($"序列化函数参数2:不是 byte[]");
            }
            if (seiralizer.Method.ReturnType != typeof(ushort))
            {
                throw new ArgumentException($"序列化函数返回类型不是 ushort");
            }

            switch (key)
            {
                case KeyAlreadyHave.Replace:
                    sFormatter[type] = (messageID, seiralizer);
                    return;
                case KeyAlreadyHave.Skip:
                    if (sFormatter.ContainsKey(type))
                    {
                        return;
                    }
                    else
                    {
                        sFormatter.Add(type, (messageID, seiralizer));
                    }
                    break;
                case KeyAlreadyHave.ThrowException:
                default:
                    sFormatter.Add(type, (messageID, seiralizer));
                    break;
            }
        }

        protected static void AddDFormatter(int messageID, Deserilizer deserilizer, KeyAlreadyHave key = KeyAlreadyHave.Skip)
        {
            if (deserilizer == null)
            {
                throw new ArgumentNullException();
            }

            switch (key)
            {
                case KeyAlreadyHave.Replace:
                    dFormatter[messageID] = deserilizer;
                    return;
                case KeyAlreadyHave.Skip:
                    if (dFormatter.ContainsKey(messageID))
                    {
                        return;
                    }
                    else
                    {
                        dFormatter.Add(messageID, deserilizer);
                    }
                    break;
                case KeyAlreadyHave.ThrowException:
                default:
                    dFormatter.Add(messageID, deserilizer);
                    break;
            }
        }

        public static void AddFormatter(Type type, int messageID, Delegate seiralizer, Deserilizer deserilizer, KeyAlreadyHave key = KeyAlreadyHave.Skip)
        {
            AddSFormatter(type, messageID, seiralizer, key);
            AddDFormatter(messageID, deserilizer, key);
        }

        public static void AddFormatter<T>(int messageID, Seiralizer<T> seiralizer, Deserilizer deserilizer, KeyAlreadyHave key = KeyAlreadyHave.Skip)
        {
            AddSFormatter(typeof(T), messageID, seiralizer, key);
            AddDFormatter(messageID, deserilizer, key);
        }

        /// <summary>
        /// 长度不能大于8192
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="buffer16384"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"> 消息长度大于8192 - 25(框架用长度),请拆分发送。"</exception>
        /// <remarks>框架中TCP接收最大支持8192，所以发送也不能大于8192，为了安全起见，框架提供的字节数组长度是16384的。</remarks>
        public static (int messageID, ushort length)
            Serialize<T>(Span<byte> buffer16384, T message)
        {
            if (sFormatter.TryGetValue(message.GetType(),out var sf))
            {
                ///序列化消息
                var (MessageID, Seiralizer) = sf;

                Seiralizer<T> seiralizer = Seiralizer as Seiralizer<T>;

                ushort length = seiralizer(message, buffer16384);

                if (length > 8192 - 25)
                {
                    //BufferPool.Push16384(buffer16384);
                    ///消息过长
                    throw new ArgumentOutOfRangeException($"消息长度大于{8192 - 25}," +
                        $"请拆分发送。");
                }

                return (MessageID, length);
            }

            return (-1,default);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="messageID"></param>
        /// <param name="body"></param>
        /// <returns></returns>
        public static dynamic Deserialize(int messageID,ReadOnlyMemory<byte> body)
        {
            if (dFormatter.ContainsKey(messageID))
            {
                return dFormatter[messageID](body);
            }
            else
            {
                return null;
            }
        }
    }

    /// <summary>
    /// 立刻使用，立刻丢弃
    /// </summary>
    public class ReadOnlyMemrotyStream : Stream
    {
        private ReadOnlyMemory<byte> memory;

        public ReadOnlyMemrotyStream(ReadOnlyMemory<byte> memory)
        {
            this.memory = memory;
        }

        public override void Flush()
        {

        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
            {
                //   T:System.ArgumentNullException:
                //     buffer is null.
                throw new ArgumentNullException();
            }


            //   T:System.ObjectDisposedException:
            //     Methods were called after the stream was closed.

            if (buffer.Length - offset <= count)
            {
                //   T:System.ArgumentException:
                //     The sum of offset and count is larger than the buffer length.
                throw new ArgumentException();
            }

            var curCount = Length - Position;
            if (curCount <= 0)
            {
                return 0;
            }

            int copyCount = curCount >= count ? count : (int)curCount;

            memory.Span.Slice((int)Position, copyCount).CopyTo(buffer.AsSpan(offset, copyCount));
            Position += copyCount;
            return count;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            var tar = 0L;
            switch (origin)
            {
                case SeekOrigin.Begin:
                    tar = 0 + offset;
                    break;
                case SeekOrigin.Current:
                    tar = Position + offset;
                    break;
                case SeekOrigin.End:
                    tar = Length + offset;
                    break;
                default:
                    break;
            }

            if (tar >= 0 && tar < Length)
            {
                Position = offset;
            }

            return Position;
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => memory.Length;
        public override long Position { get; set; } = 0;

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                {
                    memory = null;
                }
            }
            finally
            {
                // Call base.Close() to cleanup async IO resources
                base.Dispose(disposing);
            }
        }
    }

    /// <summary>
    /// 堆外内存
    /// </summary>
    public class NativeMemory : System.Buffers.MemoryManager<byte>
    {
        private IntPtr ptr;

        public int Lenght { get; private set; }

        public NativeMemory(int size)
        {
            this.Lenght = size;
            unsafe
            {
                if (Lenght > 0)
                {
                    this.ptr = Marshal.AllocHGlobal(Lenght);
                }
                else
                {
                    this.ptr = default;
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (ptr != default && Lenght > 0)
            {
                Marshal.FreeHGlobal(ptr);
                ptr = IntPtr.Zero;
                Lenght = 0;
            }
        }

        public override Span<byte> GetSpan()
        {
            unsafe
            {
                if (Lenght > 0)
                {
                    return new Span<byte>(ptr.ToPointer(), Lenght);
                }
                else
                {
                    return Span<byte>.Empty;
                }
            }
            
        }

        public override MemoryHandle Pin(int elementIndex = 0)
        {
            unsafe
            {
                return new MemoryHandle(ptr.ToPointer());
            }
        }

        public override void Unpin()
        {

        }
    }


    public static class SpanByteEX_3451DB8C29134366946FF9D778779EEC
    {
        public static void WriteTo(this int num, Span<byte> span)
            => BinaryPrimitives.WriteInt32BigEndian(span, num);
        public static void WriteTo(this ushort num, Span<byte> span)
            => BinaryPrimitives.WriteUInt16BigEndian(span, num);
        public static void WriteTo(this short num, Span<byte> span)
            => BinaryPrimitives.WriteInt16BigEndian(span, num);
        public static void WriteTo(this long num, Span<byte> span)
            => BinaryPrimitives.WriteInt64BigEndian(span, num);

        public static int ReadInt(this ReadOnlySpan<byte> span)
            => BinaryPrimitives.ReadInt32BigEndian(span);
        public static ushort ReadUshort(this ReadOnlySpan<byte> span)
            => BinaryPrimitives.ReadUInt16BigEndian(span);
        public static short ReadShort(this ReadOnlySpan<byte> span)
            => BinaryPrimitives.ReadInt16BigEndian(span);
        public static long ReadLong(this ReadOnlySpan<byte> span)
            => BinaryPrimitives.ReadInt64BigEndian(span);
    }
}
