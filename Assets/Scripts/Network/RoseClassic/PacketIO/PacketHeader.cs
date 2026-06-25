using System;

namespace RoseClassic.PacketIO
{
    public static class PacketConstants
    {
        public const int HeaderSize = 6;
        public const int MaxPacketSize = 4096;
    }

    public readonly struct PacketHeader
    {
        public readonly short Size;
        public readonly ushort Type;
        public readonly ushort Reserved;

        public PacketHeader(short size, ushort type, ushort reserved = 0)
        {
            Size = size;
            Type = type;
            Reserved = reserved;
        }

        public static PacketHeader Read(byte[] buffer, int offset = 0)
        {
            short size = BitConverter.ToInt16(buffer, offset);
            ushort type = BitConverter.ToUInt16(buffer, offset + 2);
            ushort reserved = BitConverter.ToUInt16(buffer, offset + 4);
            return new PacketHeader(size, type, reserved);
        }

        public void Write(byte[] buffer, int offset = 0)
        {
            BitConverter.GetBytes(Size).CopyTo(buffer, offset);
            BitConverter.GetBytes(Type).CopyTo(buffer, offset + 2);
            BitConverter.GetBytes(Reserved).CopyTo(buffer, offset + 4);
        }
    }
}
