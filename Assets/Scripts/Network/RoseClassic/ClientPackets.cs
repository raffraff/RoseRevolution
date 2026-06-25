using RoseClassic.FlatBuffer;
using RoseClassic.PacketIO;

namespace RoseClassic
{
    public static class ClientPackets
    {
        public static byte[] AcceptRequest()
        {
            return PacketBuffer.CreateHeaderOnly(Opcodes.CliAcceptReq).ToArray();
        }

        public static byte[] SelectServer(uint serverId, byte channelNo)
        {
            var packet = PacketBuffer.CreateHeader(Opcodes.CliSelectServer, 5);
            packet.WriteUInt32(serverId);
            packet.WriteByte(channelNo);
            return packet.ToArray();
        }

        public static byte[] JoinServerRequest(uint id, string passwordHash64)
        {
            var packet = PacketBuffer.CreateHeader(Opcodes.CliJoinServerReq, 68);
            packet.WriteUInt32(id);
            var hashBytes = System.Text.Encoding.ASCII.GetBytes(passwordHash64);
            var password = new byte[64];
            System.Buffer.BlockCopy(hashBytes, 0, password, 0, System.Math.Min(hashBytes.Length, 64));
            packet.WriteBytes(password);
            return packet.ToArray();
        }

        public static byte[] CharListRequest()
        {
            return PacketBuffer.CreateHeaderOnly(Opcodes.CliCharList).ToArray();
        }

        public static byte[] SelectCharacter(byte slot, string name, byte runMode = 1, byte rideMode = 0)
        {
            var packet = PacketBuffer.CreateHeader(Opcodes.CliSelectChar, 3);
            packet.WriteByte(slot);
            packet.WriteByte(runMode);
            packet.WriteByte(rideMode);
            packet.WriteString(name);
            return packet.ToArray();
        }

        public static byte[] JoinZone(byte weightRate = 100, short posZ = 0)
        {
            var packet = PacketBuffer.CreateHeader(Opcodes.CliJoinZone, 3);
            packet.WriteByte(weightRate);
            packet.WriteInt16(posZ);
            return packet.ToArray();
        }

        public static byte[] ChatMessage(string message)
        {
            var packet = PacketBuffer.CreateHeader(Opcodes.CliChat, 0);
            packet.WriteString(message);
            return packet.ToArray();
        }

        public static byte[] MouseCommand(ushort targetObjectIndex, UnityEngine.Vector3 destination, short posZ)
        {
            var packet = PacketBuffer.CreateHeader(Opcodes.CliMouseCmd, 14);
            packet.WriteUInt16(targetObjectIndex);
            packet.WriteFloat(destination.x);
            packet.WriteFloat(destination.y);
            packet.WriteInt16(posZ);
            return packet.ToArray();
        }

        public static byte[] LoginRequest(string username, string password)
        {
            return FlatBufferPackets.BuildLoginRequest(username, Sha256Util.HashHex(password));
        }

        public static byte[] CreateCharacterRequest(string name, short hairId, short faceId, short jobId, byte genderId)
        {
            return FlatBufferPackets.BuildCharacterCreateRequest(name, hairId, faceId, jobId, genderId);
        }
    }
}
