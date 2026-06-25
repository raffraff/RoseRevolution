using System;
using System.Collections.Generic;
using System.Text;

namespace RoseClassic.PacketIO
{
    public sealed class PacketBuffer
    {
        readonly List<byte> bytes = new List<byte>(256);

        public int Length => bytes.Count;
        public byte[] ToArray() => bytes.ToArray();

        public void Clear() => bytes.Clear();

        public void WriteInt16(short value) => bytes.AddRange(BitConverter.GetBytes(value));
        public void WriteUInt16(ushort value) => bytes.AddRange(BitConverter.GetBytes(value));
        public void WriteInt32(int value) => bytes.AddRange(BitConverter.GetBytes(value));
        public void WriteUInt32(uint value) => bytes.AddRange(BitConverter.GetBytes(value));
        public void WriteInt64(long value) => bytes.AddRange(BitConverter.GetBytes(value));
        public void WriteFloat(float value) => bytes.AddRange(BitConverter.GetBytes(value));
        public void WriteByte(byte value) => bytes.Add(value);
        public void WriteBytes(byte[] data) => bytes.AddRange(data);
        public void WriteBytes(byte[] data, int offset, int count) => bytes.AddRange(new ArraySegment<byte>(data, offset, count));

        public void WriteString(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                bytes.Add(0);
            }
            else
            {
                bytes.AddRange(Encoding.ASCII.GetBytes(value));
                bytes.Add(0);
            }

            UpdateHeaderSize();
        }

        public void UpdateHeaderSize()
        {
            if (bytes.Count >= 2)
            {
                short size = (short)bytes.Count;
                bytes[0] = (byte)size;
                bytes[1] = (byte)(size >> 8);
            }
        }

        public static PacketBuffer CreateHeader(ushort type, int bodyLength)
        {
            var packet = new PacketBuffer();
            packet.WriteInt16((short)(PacketConstants.HeaderSize + bodyLength));
            packet.WriteUInt16(type);
            packet.WriteUInt16(0);
            return packet;
        }

        public static PacketBuffer CreateHeaderOnly(ushort type)
        {
            return CreateHeader(type, 0);
        }

        public static short ReadInt16(byte[] data, ref int offset)
        {
            short value = BitConverter.ToInt16(data, offset);
            offset += 2;
            return value;
        }

        public static ushort ReadUInt16(byte[] data, ref int offset)
        {
            ushort value = BitConverter.ToUInt16(data, offset);
            offset += 2;
            return value;
        }

        public static int ReadInt32(byte[] data, ref int offset)
        {
            int value = BitConverter.ToInt32(data, offset);
            offset += 4;
            return value;
        }

        public static uint ReadUInt32(byte[] data, ref int offset)
        {
            uint value = BitConverter.ToUInt32(data, offset);
            offset += 4;
            return value;
        }

        public static long ReadInt64(byte[] data, ref int offset)
        {
            long value = BitConverter.ToInt64(data, offset);
            offset += 8;
            return value;
        }

        public static float ReadFloat(byte[] data, ref int offset)
        {
            float value = BitConverter.ToSingle(data, offset);
            offset += 4;
            return value;
        }

        public static byte ReadByte(byte[] data, ref int offset) => data[offset++];

        public static string ReadString(byte[] data, ref int offset)
        {
            int start = offset;
            while (offset < data.Length && data[offset] != 0)
                offset++;

            string value = Encoding.ASCII.GetString(data, start, offset - start);
            if (offset < data.Length)
                offset++;

            return value;
        }

        public static uint ReadPartItem(byte[] data, ref int offset)
        {
            uint value = ReadUInt32(data, ref offset);
            return value;
        }

        public static int GetPartItemNo(uint packed) => (int)(packed & 0x3FF);
    }
}
