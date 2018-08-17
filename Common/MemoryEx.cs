using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace System.Buffers
{
    /// <summary>
    /// ReadOnlyMemory的流包装器， 立刻使用，立刻丢弃，不应该保存。
    /// 这个类用于在 不支持Span的第三方API调用过程中转换参数，随着第三方类库的支持完成这类会删除。
    /// <para></para>
    /// https://gist.github.com/GrabYourPitchforks/4c3e1935fd4d9fa2831dbfcab35dffc6
    /// 参考第五条规则
    /// </summary>
    public class ReadOnlyMemrotyStream : Stream
    {
        /// 事实上，无法支持Span的类库永远都会存在，所以这个类可能永远不会废除……

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
    /// <para>**注意：堆外内存无法取出数组。MemoryMarshal.TryGetArray对此类无效。**</para>
    /// </summary>
    public class NativeMemory : MemoryManager<byte>
    {
        private readonly IntPtr ptr;

        /// <summary>
        /// 不要以任何方式修改长度。有可能导致内存泄漏。
        /// </summary>
        public int Lenght { get; private set; }

        /// <summary>
        /// <para>**注意：堆外内存无法取出数组。MemoryMarshal.TryGetArray对此类无效。**</para>
        /// </summary>
        /// <param name="size"></param>
        public NativeMemory(int size)
        {
            this.Lenght = size;

            if (Lenght > 0)
            {
                this.ptr = Marshal.AllocHGlobal(Lenght);
                ///*申请到的内存可能不是干净的，也许需要0填充。
                GetSpan().Clear();
            }
            else
            {
                this.ptr = IntPtr.Zero;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (ptr != default && Lenght > 0)
            {
                Marshal.FreeHGlobal(ptr);
                Lenght = -1;
            }
        }

        public override Span<byte> GetSpan()
        {
            if (Lenght > 0)
            {
                unsafe
                {
                    return new Span<byte>(ptr.ToPointer(), Lenght);
                }
            }
            else if (Lenght < 0)
            {
                throw new ObjectDisposedException(nameof(NativeMemory));
            }
            else
            {
                return Span<byte>.Empty;
            }
        }

        /// <summary>
        /// 堆外内存，指针不会移动。
        /// </summary>
        /// <param name="elementIndex"></param>
        /// <returns></returns>
        public unsafe override MemoryHandle Pin(int elementIndex = 0)
        {
            if (elementIndex > Lenght)
            {
                throw new IndexOutOfRangeException();
            }
            return new MemoryHandle(IntPtr.Add(ptr,elementIndex).ToPointer());
        }

        public override void Unpin()
        {

        }
    }
}
