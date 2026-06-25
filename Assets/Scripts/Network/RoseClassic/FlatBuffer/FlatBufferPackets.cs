using Google.FlatBuffers;

namespace RoseClassic.FlatBuffer
{
    public enum PacketDataType : byte
    {
        None = 0,
        CharacterCreateRequest = 1,
        CharacterMove = 2,
        CharacterMoveAttack = 3,
        LoginRequest = 4,
        LoginReply = 5,
        UpdateStats = 6,
    }

    public struct CharacterMovePacket
    {
        public uint CharacterId;
        public uint TargetId;
        public float TargetX;
        public float TargetY;
        public float TargetZ;
        public float TargetDistance;
        public ushort MoveSpeed;
        public byte MoveMode;
    }

    public static class FlatBufferPackets
    {
        public static byte[] BuildLoginRequest(string username, string passwordHash64)
        {
            var builder = new FlatBufferBuilder(256);
            StringOffset usernameOffset = builder.CreateString(username);
            StringOffset passwordOffset = builder.CreateString(passwordHash64);

            int loginReq = CreateLoginRequest(builder, usernameOffset, passwordOffset);
            int packetData = CreatePacketData(builder, PacketDataType.LoginRequest, loginReq);
            builder.Finish(packetData);

            return WrapPacket(builder.SizedByteArray());
        }

        public static byte[] BuildCharacterCreateRequest(string name, short hairId, short faceId, short jobId, byte genderId)
        {
            var builder = new FlatBufferBuilder(256);
            StringOffset nameOffset = builder.CreateString(name);

            int createReq = CreateCharacterCreateRequest(builder, nameOffset, hairId, faceId, jobId, genderId);
            int packetData = CreatePacketData(builder, PacketDataType.CharacterCreateRequest, createReq);
            builder.Finish(packetData);

            return WrapPacket(builder.SizedByteArray());
        }

        public static bool TryParseCharacterMove(byte[] packet, out CharacterMovePacket move)
        {
            move = default;
            if (packet == null || packet.Length < 8)
                return false;

            try
            {
                var bb = new ByteBuffer(packet, 2);
                PacketData? root = PacketData.GetRootAsPacketData(bb);
                if (!root.HasValue || root.Value.DataType != PacketDataType.CharacterMove)
                    return false;

                CharacterMove? cm = root.Value.DataAsCharacterMove();
                if (!cm.HasValue)
                    return false;

                CharacterMove data = cm.Value;
                Vec3? pos = data.TargetPos;
                move = new CharacterMovePacket
                {
                    CharacterId = data.CharacterId,
                    TargetId = data.TargetId,
                    TargetX = pos?.X ?? 0,
                    TargetY = pos?.Y ?? 0,
                    TargetZ = pos?.Z ?? 0,
                    TargetDistance = data.TargetDistance,
                    MoveSpeed = data.MoveSpeed,
                    MoveMode = data.MoveMode,
                };
                return true;
            }
            catch
            {
                return false;
            }
        }

        static byte[] WrapPacket(byte[] flatBufferData)
        {
            ushort totalSize = (ushort)(2 + flatBufferData.Length);
            var packet = new byte[totalSize];
            packet[0] = (byte)totalSize;
            packet[1] = (byte)(totalSize >> 8);
            System.Buffer.BlockCopy(flatBufferData, 0, packet, 2, flatBufferData.Length);
            return packet;
        }

        static int CreateLoginRequest(FlatBufferBuilder builder, StringOffset username, StringOffset password)
        {
            builder.StartTable(2);
            builder.AddOffset(1, password.Value, 0);
            builder.AddOffset(0, username.Value, 0);
            return builder.EndTable();
        }

        static int CreateCharacterCreateRequest(FlatBufferBuilder builder, StringOffset name, short hairId, short faceId, short jobId, byte genderId)
        {
            builder.StartTable(5);
            builder.AddByte(4, genderId, 0);
            builder.AddShort(3, jobId, 0);
            builder.AddShort(2, faceId, 0);
            builder.AddShort(1, hairId, 0);
            builder.AddOffset(0, name.Value, 0);
            return builder.EndTable();
        }

        static int CreatePacketData(FlatBufferBuilder builder, PacketDataType type, int dataOffset)
        {
            builder.StartTable(2);
            builder.AddOffset(1, dataOffset, 0);
            builder.AddByte(0, (byte)type, 0);
            return builder.EndTable();
        }
    }

    public struct Vec3 : IFlatbufferObject
    {
        struct Table_
        {
            public ByteBuffer bb;
            public int bb_pos;
            public void __init(int _i, ByteBuffer _bb) { bb_pos = _i; bb = _bb; }
            public float X => bb.GetFloat(bb_pos + 0);
            public float Y => bb.GetFloat(bb_pos + 4);
            public float Z => bb.GetFloat(bb_pos + 8);
        }

        Table_ table;

        public ByteBuffer ByteBuffer => table.bb;
        public void __init(int _i, ByteBuffer _bb) => table.__init(_i, _bb);
        public float X => table.X;
        public float Y => table.Y;
        public float Z => table.Z;
    }

    public struct CharacterMove : IFlatbufferObject
    {
        struct Table_
        {
            public ByteBuffer bb;
            public int bb_pos;
            public void __init(int _i, ByteBuffer _bb) { bb_pos = _i; bb = _bb; }

            int __offset(int vtableOffset)
            {
                int vtable = bb_pos - bb.GetInt(bb_pos);
                return vtableOffset < bb.GetShort(vtable) ? bb.GetShort(vtable + vtableOffset) : 0;
            }

            public uint CharacterId { get { int o = __offset(4); return o != 0 ? bb.GetUint(bb_pos + o) : 0; } }
            public uint TargetId { get { int o = __offset(6); return o != 0 ? bb.GetUint(bb_pos + o) : 0; } }
            public Vec3? TargetPos
            {
                get
                {
                    int o = __offset(8);
                    if (o == 0) return null;
                    var v = new Vec3();
                    v.__init(bb_pos + o + bb.GetInt(bb_pos + o), bb);
                    return v;
                }
            }
            public float TargetDistance { get { int o = __offset(10); return o != 0 ? bb.GetFloat(bb_pos + o) : 0; } }
            public ushort MoveSpeed { get { int o = __offset(12); return o != 0 ? bb.GetUshort(bb_pos + o) : (ushort)0; } }
            public byte MoveMode { get { int o = __offset(14); return o != 0 ? bb.Get(bb_pos + o) : (byte)0; } }
        }

        Table_ table;

        public ByteBuffer ByteBuffer => table.bb;
        public void __init(int _i, ByteBuffer _bb) => table.__init(_i, _bb);
        public uint CharacterId => table.CharacterId;
        public uint TargetId => table.TargetId;
        public Vec3? TargetPos => table.TargetPos;
        public float TargetDistance => table.TargetDistance;
        public ushort MoveSpeed => table.MoveSpeed;
        public byte MoveMode => table.MoveMode;
    }

    public struct PacketData : IFlatbufferObject
    {
        struct Table_
        {
            public ByteBuffer bb;
            public int bb_pos;
            public void __init(int _i, ByteBuffer _bb) { bb_pos = _i; bb = _bb; }

            int __offset(int vtableOffset)
            {
                int vtable = bb_pos - bb.GetInt(bb_pos);
                return vtableOffset < bb.GetShort(vtable) ? bb.GetShort(vtable + vtableOffset) : 0;
            }

            public PacketDataType DataType
            {
                get
                {
                    int o = __offset(4);
                    return o != 0 ? (PacketDataType)bb.Get(bb_pos + o) : PacketDataType.None;
                }
            }

            public CharacterMove? DataAsCharacterMove()
            {
                int o = __offset(6);
                if (o == 0 || DataType != PacketDataType.CharacterMove)
                    return null;
                var cm = new CharacterMove();
                cm.__init(bb_pos + o + bb.GetInt(bb_pos + o), bb);
                return cm;
            }
        }

        Table_ table;

        public ByteBuffer ByteBuffer => table.bb;
        public void __init(int _i, ByteBuffer _bb) => table.__init(_i, _bb);
        public PacketDataType DataType => table.DataType;
        public CharacterMove? DataAsCharacterMove() => table.DataAsCharacterMove();

        public static PacketData? GetRootAsPacketData(ByteBuffer bb)
        {
            var obj = new PacketData();
            obj.__init(bb.GetInt(bb.Position) + bb.Position, bb);
            return obj;
        }
    }
}
