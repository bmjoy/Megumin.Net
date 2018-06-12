using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace MMONET.Sockets
{
    internal class MessageHeader
    {
    }

    //不管用和预期不一致
    // safe accessor of Single/Double's underlying byte.
    // This code is borrowed from MsgPack-Cli https://github.com/msgpack/msgpack-cli

    [StructLayout(LayoutKind.Explicit)]
    internal struct IntBytes
    {
        [FieldOffset(0)]
        public readonly int Value;

        [FieldOffset(0)]
        public readonly Byte Byte0;

        [FieldOffset(1)]
        public readonly Byte Byte1;

        [FieldOffset(2)]
        public readonly Byte Byte2;

        [FieldOffset(3)]
        public readonly Byte Byte3;

        public IntBytes(byte[] buffer, int offset)
        {
            this = default(IntBytes);

            this.Byte0 = buffer[offset];
            this.Byte1 = buffer[offset + 1];
            this.Byte0 = buffer[offset + 2];
            this.Byte1 = buffer[offset + 3];
        }

        public static implicit operator int(IntBytes shortBytes)
        {
            return shortBytes.Value;
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct UshortBytes
    {
        [FieldOffset(0)]
        public readonly ushort Value;

        [FieldOffset(0)]
        public readonly Byte Byte0;

        [FieldOffset(1)]
        public readonly Byte Byte1;

        public UshortBytes(byte[] buffer, int offset)
        {
            this = default(UshortBytes);

            this.Byte0 = buffer[offset];
            this.Byte1 = buffer[offset + 1];
        }

        public static implicit operator ushort(UshortBytes shortBytes)
        {
            return shortBytes.Value;
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct ShortBytes
    {
        [FieldOffset(0)]
        public readonly short Value;

        [FieldOffset(0)]
        public readonly Byte Byte0;

        [FieldOffset(1)]
        public readonly Byte Byte1;

        public ShortBytes(byte[] buffer, int offset)
        {
            this = default(ShortBytes);

            this.Byte0 = buffer[offset];
            this.Byte1 = buffer[offset + 1];
        }

        public static implicit operator short(ShortBytes shortBytes)
        {
            return shortBytes.Value;
        }
    }
}
