using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RoseClassic.FlatBuffer;
using RoseClassic.PacketIO;
using UnityEngine;

namespace RoseClassic
{
    public enum RoseConnectionPhase
    {
        Disconnected,
        LoginServer,
        WorldServer,
        ZoneServer,
        InWorld,
    }

    public class RoseNetworkManager : MonoBehaviour
    {
        public static RoseNetworkManager Instance { get; private set; }

        [Header("Login Server")]
        public string loginHost = "127.0.0.1";
        public int loginPort = RosePorts.Login;

        [Header("Defaults")]
        public byte defaultChannel = 1;

        readonly RoseTcpClient loginSocket = new RoseTcpClient();
        readonly RoseTcpClient worldSocket = new RoseTcpClient();
        readonly RoseTcpClient zoneSocket = new RoseTcpClient();

        string passwordHash64 = "";
        uint worldSessionId;
        uint[] zoneSessionIds = new uint[2];
        string pendingWorldHost;
        int pendingWorldPort;
        string pendingZoneHost;
        int pendingZonePort;
        bool zoneDataReady;
        bool zoneQuestReceived;
        byte pendingCharSlot;
        string pendingCharName;

        RoseTcpClient activeWorldSocket;
        RoseConnectionPhase phase = RoseConnectionPhase.Disconnected;

        public RoseConnectionPhase Phase => phase;
        public string PasswordHash64 => passwordHash64;
        public ushort LocalObjectIndex { get; private set; }
        public SelectedCharacterData SelectedCharacter { get; private set; }

        public List<ServerEntry> Servers { get; } = new List<ServerEntry>();
        public List<CharacterEntry> Characters { get; } = new List<CharacterEntry>();
        public Dictionary<ushort, RemoteEntity> Entities { get; } = new Dictionary<ushort, RemoteEntity>();

        public event Action<string> StatusChanged;
        public event Action<string> LoginFailed;
        public event Action ServerListUpdated;
        public event Action CharacterListUpdated;
        public event Action CharacterCreateResult;
        public event Action CharacterReadyToEnter;
        public event Action EnteredWorld;
        public event Action<ushort, string> ChatReceived;
        public event Action<RemoteEntity> EntitySpawned;
        public event Action<ushort> EntityRemoved;
        public event Action<ushort, Vector3> EntityMoved;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Update()
        {
            ProcessSocket(loginSocket, HandleLoginPacket);
            ProcessSocket(worldSocket, HandleWorldPacket);
            ProcessSocket(zoneSocket, HandleZonePacket);
        }

        void OnDestroy()
        {
            DisconnectAll();
        }

        public async Task ConnectLoginAsync(string username, string password)
        {
            DisconnectAll();
            SetPhase(RoseConnectionPhase.LoginServer, $"Connecting to login server {loginHost}:{loginPort}...");
            passwordHash64 = Sha256Util.HashHex(password);

            try
            {
                await loginSocket.ConnectAsync(loginHost, loginPort);
                activeWorldSocket = loginSocket;
            }
            catch (Exception ex)
            {
                SetPhase(RoseConnectionPhase.Disconnected, $"Connection failed: {ex.Message}");
                LoginFailed?.Invoke($"Could not connect to {loginHost}:{loginPort}. Is the login server running?");
            }
        }

        public void SendLogin(string username, string password)
        {
            passwordHash64 = Sha256Util.HashHex(password);
            loginSocket.Send(ClientPackets.LoginRequest(username, password));
            SetStatus("Login request sent...");
        }

        public void SelectServer(uint serverId, byte channelNo = 1)
        {
            loginSocket.Send(ClientPackets.SelectServer(serverId, channelNo));
            SetStatus("Selecting server...");
        }

        public void RequestCharacterList()
        {
            GetWorldSocket().Send(ClientPackets.CharListRequest());
        }

        public void CreateCharacter(string name, short hairId, short faceId, short jobId, byte genderId)
        {
            GetWorldSocket().Send(ClientPackets.CreateCharacterRequest(name, hairId, faceId, jobId, genderId));
        }

        public void SelectCharacter(byte slot, string name)
        {
            pendingCharSlot = slot;
            pendingCharName = name;
            GetWorldSocket().Send(ClientPackets.SelectCharacter(slot, name));
            SetStatus($"Selecting character {name}...");
        }

        public void SendChat(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            zoneSocket.Send(ClientPackets.ChatMessage(message));
        }

        public void SendMove(Vector3 destination, short posZ, ushort targetObjectIndex = 0)
        {
            Vector3 server = RoseCoordinates.UnityToServer(destination);
            zoneSocket.Send(ClientPackets.MouseCommand(targetObjectIndex, new Vector3(server.x, server.y, 0), posZ));
        }

        public void DisconnectAll()
        {
            loginSocket.Disconnect();
            worldSocket.Disconnect();
            zoneSocket.Disconnect();
            phase = RoseConnectionPhase.Disconnected;
            Servers.Clear();
            Characters.Clear();
            Entities.Clear();
            SelectedCharacter = null;
            LocalObjectIndex = 0;
            activeWorldSocket = null;
            ResetZoneDataState();
        }

        RoseTcpClient GetWorldSocket() => activeWorldSocket ?? worldSocket;

        void ProcessSocket(RoseTcpClient socket, Action<byte[]> handler)
        {
            while (socket.TryDequeue(out byte[] packet))
                handler(packet);
        }

        void HandleLoginPacket(byte[] packet)
        {
            if (TryHandleFlatBuffer(packet))
                return;

            var header = PacketHeader.Read(packet);
            switch (header.Type)
            {
                case Opcodes.SocketNetworkStatus:
                    HandleNetworkStatus(packet, loginSocket, true);
                    break;
                case Opcodes.LsvLoginReply:
                    ParseLoginReply(packet);
                    break;
                case Opcodes.LsvSelectServer:
                    ParseSelectServer(packet);
                    break;
                default:
                    RoseDebug.LogWarning($"Unhandled login packet 0x{header.Type:X4}");
                    break;
            }
        }

        void HandleWorldPacket(byte[] packet)
        {
            if (TryHandleFlatBuffer(packet))
                return;

            var header = PacketHeader.Read(packet);
            switch (header.Type)
            {
                case Opcodes.SocketNetworkStatus:
                    HandleNetworkStatus(packet, worldSocket, false);
                    break;
                case Opcodes.SrvJoinServerReply:
                    if (HandleJoinServerReply(packet, false))
                        RequestCharacterList();
                    break;
                case Opcodes.WsvCharList:
                    ParseCharacterList(packet);
                    break;
                case Opcodes.WsvCreateChar:
                    ParseCreateCharacter(packet);
                    break;
                case Opcodes.WsvMoveServer:
                    ParseMoveServer(packet);
                    break;
                case Opcodes.WsvMemo:
                    // Mail count notification sent after character select; safe to ignore.
                    break;
                default:
                    RoseDebug.LogWarning($"Unhandled world packet 0x{header.Type:X4}");
                    break;
            }
        }

        void HandleZonePacket(byte[] packet)
        {
            if (TryHandleFlatBuffer(packet))
                return;

            var header = PacketHeader.Read(packet);
            switch (header.Type)
            {
                case Opcodes.SocketNetworkStatus:
                    HandleNetworkStatus(packet, zoneSocket, false);
                    break;
                case Opcodes.SrvJoinServerReply:
                    HandleJoinServerReply(packet, true);
                    break;
                case Opcodes.GsvSelectChar:
                    ParseSelectCharacter(packet);
                    break;
                case Opcodes.GsvInventoryData:
                    break;
                case Opcodes.GsvQuestData:
                    zoneQuestReceived = true;
                    TryMarkZoneDataReady();
                    break;
                case Opcodes.GsvQuestOnly:
                    zoneQuestReceived = true;
                    TryMarkZoneDataReady();
                    break;
                case Opcodes.GsvWishList:
                    break;
                case Opcodes.GsvJoinZone:
                    ParseJoinZone(packet);
                    break;
                case Opcodes.GsvInitData:
                    break;
                case Opcodes.GsvAvtChar:
                    ParseSpawnCharacter(packet, true);
                    break;
                case Opcodes.GsvMobChar:
                case Opcodes.GsvNpcChar:
                    ParseSpawnCharacter(packet, false);
                    break;
                case Opcodes.GsvSubObject:
                    ParseSubObject(packet);
                    break;
                case Opcodes.GsvMove:
                    ParseLegacyMove(packet);
                    break;
                case Opcodes.GsvChat:
                    ParseChat(packet);
                    break;
                default:
                    RoseDebug.LogWarning($"Unhandled zone packet 0x{header.Type:X4}");
                    break;
            }
        }

        bool TryHandleFlatBuffer(byte[] packet)
        {
            if (packet.Length < 8)
                return false;

            if (FlatBufferPackets.TryParseCharacterMove(packet, out CharacterMovePacket move))
            {
                var pos = RoseCoordinates.ServerToUnity(move.TargetX, move.TargetY, move.TargetZ);
                EntityMoved?.Invoke((ushort)move.CharacterId, pos);
                return true;
            }

            return false;
        }

        void HandleNetworkStatus(byte[] packet, RoseTcpClient socket, bool isLoginFlow)
        {
            byte status = packet[6];
            switch (status)
            {
                case NetworkStatus.Connect:
                    if (socket == loginSocket)
                        loginSocket.Send(ClientPackets.AcceptRequest());
                    else if (socket == worldSocket)
                        worldSocket.Send(ClientPackets.JoinServerRequest(worldSessionId, passwordHash64));
                    else if (socket == zoneSocket)
                        zoneSocket.Send(ClientPackets.JoinServerRequest(zoneSessionIds[0], passwordHash64));
                    break;
                case NetworkStatus.Accepted:
                    if (isLoginFlow)
                        SetStatus("Login server accepted connection.");
                    break;
                case NetworkStatus.Disconnect:
                    SetStatus("Disconnected from server.");
                    break;
            }
        }

        void ParseLoginReply(byte[] packet)
        {
            int offset = PacketConstants.HeaderSize;
            byte result = PacketBuffer.ReadByte(packet, ref offset);
            result = (byte)(result & 0x7F);

            if (result != LoginResult.Ok)
            {
                LoginFailed?.Invoke(DescribeLoginError(result));
                return;
            }

            offset += 4; // right + pay type
            Servers.Clear();
            while (offset < packet.Length)
            {
                string serverName = PacketBuffer.ReadString(packet, ref offset);
                if (offset + 4 > packet.Length)
                    break;

                uint serverId = PacketBuffer.ReadUInt32(packet, ref offset);
                if (!string.IsNullOrEmpty(serverName))
                    Servers.Add(new ServerEntry { Name = serverName, Id = serverId });
            }

            ServerListUpdated?.Invoke();
            SetStatus($"Login OK. {Servers.Count} server(s) available.");
        }

        async void ParseSelectServer(byte[] packet)
        {
            int offset = PacketConstants.HeaderSize;
            byte result = PacketBuffer.ReadByte(packet, ref offset);
            if (result != SelectServerResult.Ok)
            {
                LoginFailed?.Invoke($"Server selection failed ({result}).");
                return;
            }

            worldSessionId = PacketBuffer.ReadUInt32(packet, ref offset);
            offset += 4; // random seed
            string worldHost = PacketBuffer.ReadString(packet, ref offset);
            ushort worldPort = PacketBuffer.ReadUInt16(packet, ref offset);

            pendingWorldHost = worldHost;
            pendingWorldPort = worldPort;

            loginSocket.Disconnect();
            SetPhase(RoseConnectionPhase.WorldServer, $"Connecting to world server {worldHost}:{worldPort}...");

            await worldSocket.ConnectAsync(worldHost, worldPort);
            activeWorldSocket = worldSocket;
        }

        bool HandleJoinServerReply(byte[] packet, bool isZoneServer)
        {
            int offset = PacketConstants.HeaderSize;
            byte result = PacketBuffer.ReadByte(packet, ref offset);

            if (result == JoinServerResult.Ok)
            {
                SetStatus(isZoneServer ? "Connected to game server." : "Connected to character server.");
                return true;
            }

            string target = isZoneServer ? "game" : "character";
            SetStatus($"{target} server join failed (code {result}).");
            RoseDebug.LogError($"{target} server join failed with result {result}.");
            return false;
        }

        void ParseCharacterList(byte[] packet)
        {
            int offset = PacketConstants.HeaderSize;
            byte count = PacketBuffer.ReadByte(packet, ref offset);

            Characters.Clear();
            for (int i = 0; i < count; i++)
            {
                var entry = new CharacterEntry { Slot = (byte)i };
                // rose-next-classic sends name before tagCHARINFO + part items.
                entry.Name = PacketBuffer.ReadString(packet, ref offset);
                entry.Race = PacketBuffer.ReadByte(packet, ref offset);
                entry.Level = PacketBuffer.ReadInt16(packet, ref offset);
                entry.Job = PacketBuffer.ReadInt16(packet, ref offset);
                offset += 4; // remain sec
                offset += 1; // platinum flag

                for (int p = 0; p < BodyPart.Max; p++)
                    entry.PartItems[p] = PacketBuffer.ReadPartItem(packet, ref offset);

                Characters.Add(entry);
            }

            CharacterListUpdated?.Invoke();
            SetStatus($"Received {Characters.Count} character(s).");
        }

        void ParseCreateCharacter(byte[] packet)
        {
            int offset = PacketConstants.HeaderSize;
            byte result = PacketBuffer.ReadByte(packet, ref offset);
            CharacterCreateResult?.Invoke();
            if (result == CreateCharResult.Ok)
                RequestCharacterList();
        }

        async void ParseMoveServer(byte[] packet)
        {
            int offset = PacketConstants.HeaderSize;
            ushort zonePort = PacketBuffer.ReadUInt16(packet, ref offset);
            zoneSessionIds[0] = PacketBuffer.ReadUInt32(packet, ref offset);
            zoneSessionIds[1] = PacketBuffer.ReadUInt32(packet, ref offset);
            string zoneHost = PacketBuffer.ReadString(packet, ref offset);

            if (string.IsNullOrWhiteSpace(zoneHost))
                zoneHost = "127.0.0.1";

            pendingZoneHost = zoneHost;
            pendingZonePort = zonePort;
            ResetZoneDataState();
            activeWorldSocket = null;

            // Keep the world-server TCP session alive; rose-next validates zone joins against it.
            SetPhase(RoseConnectionPhase.ZoneServer, $"Character accepted. Connecting to game server {zoneHost}:{zonePort}...");

            try
            {
                await zoneSocket.ConnectAsync(zoneHost, zonePort);
            }
            catch (Exception ex)
            {
                SetStatus($"Failed to connect to game server {zoneHost}:{zonePort}: {ex.Message}");
                RoseDebug.LogError($"Zone connect failed: {ex.Message}");
            }
        }

        void ParseSelectCharacter(byte[] packet)
        {
            int offset = PacketConstants.HeaderSize;
            var selected = new SelectedCharacterData();

            selected.Race = PacketBuffer.ReadByte(packet, ref offset);
            selected.ZoneId = PacketBuffer.ReadInt16(packet, ref offset);
            float x = PacketBuffer.ReadFloat(packet, ref offset);
            float y = PacketBuffer.ReadFloat(packet, ref offset);
            selected.ServerSpawnX = x;
            selected.ServerSpawnY = y;
            selected.ServerSpawnZ = 0;
            selected.SpawnPosition = Vector3.zero;
            offset += 2; // revive zone

            for (int i = 0; i < BodyPart.Max; i++)
                selected.PartItems[i] = PacketBuffer.ReadPartItem(packet, ref offset);

            offset += BodyPart.Max * 4; // costume

            int nameOffset = PacketConstants.HeaderSize + RoseStructSizes.GsvSelectCharFixedBody;
            if (nameOffset < packet.Length)
            {
                selected.Name = PacketBuffer.ReadString(packet, ref nameOffset);
            }
            else
            {
                selected.Name = pendingCharName;
            }

            selected.FaceId = (byte)PacketBuffer.GetPartItemNo(selected.PartItems[BodyPart.Face]);
            selected.HairId = (byte)PacketBuffer.GetPartItemNo(selected.PartItems[BodyPart.Hair]);
            SelectedCharacter = selected;
            SetStatus($"Character {selected.Name} selected for zone {selected.ZoneId}.");
        }

        void ResetZoneDataState()
        {
            zoneDataReady = false;
            zoneQuestReceived = false;
        }

        void TryMarkZoneDataReady()
        {
            if (zoneDataReady || !zoneQuestReceived)
                return;

            zoneDataReady = true;
            SetStatus("Zone data received. Ready to enter map.");
            CharacterReadyToEnter?.Invoke();
        }

        public void SendJoinZone(byte weightRate = 100)
        {
            if (!zoneDataReady)
                return;

            zoneSocket.Send(ClientPackets.JoinZone(weightRate));
            SetStatus("Join zone request sent...");
        }

        void ParseJoinZone(byte[] packet)
        {
            int offset = PacketConstants.HeaderSize;
            LocalObjectIndex = PacketBuffer.ReadUInt16(packet, ref offset);
            if (SelectedCharacter != null)
                SelectedCharacter.ServerObjectIndex = LocalObjectIndex;

            SetPhase(RoseConnectionPhase.InWorld, "Entered world.");
            EnteredWorld?.Invoke();
        }

        void ParseSpawnCharacter(byte[] packet, bool isPlayer)
        {
            int offset = PacketConstants.HeaderSize;
            var entity = new RemoteEntity { IsPlayer = isPlayer };

            entity.ObjectIndex = PacketBuffer.ReadUInt16(packet, ref offset);
            float curX = PacketBuffer.ReadFloat(packet, ref offset);
            float curY = PacketBuffer.ReadFloat(packet, ref offset);
            float toX = PacketBuffer.ReadFloat(packet, ref offset);
            float toY = PacketBuffer.ReadFloat(packet, ref offset);
            offset += 6; // command, target
            offset += 1; // move mode
            offset += 4; // hp
            offset += 4; // team
            offset += 4; // status flag

            entity.Position = RoseCoordinates.ServerToUnity(curX, curY);
            entity.TargetPosition = RoseCoordinates.ServerToUnity(toX, toY);

            if (isPlayer)
            {
                offset += 2; // run speed
                offset += 2; // atk speed
                offset += 1; // weight
                for (int i = 0; i < BodyPart.Max; i++)
                    entity.PartItems[i] = PacketBuffer.ReadPartItem(packet, ref offset);
                offset += BodyPart.Max * 4; // costume
                offset += 8; // shot items simplified
                entity.Job = PacketBuffer.ReadInt16(packet, ref offset);
                entity.Level = PacketBuffer.ReadByte(packet, ref offset);
                offset += 1; // pvp
                offset += 8; // riding items simplified
                entity.Name = PacketBuffer.ReadString(packet, ref offset);
                entity.Race = 0;
            }

            Entities[entity.ObjectIndex] = entity;
            EntitySpawned?.Invoke(entity);
        }

        void ParseSubObject(byte[] packet)
        {
            int offset = PacketConstants.HeaderSize;
            while (offset + 2 <= packet.Length)
            {
                ushort objectIndex = PacketBuffer.ReadUInt16(packet, ref offset);
                Entities.Remove(objectIndex);
                EntityRemoved?.Invoke(objectIndex);
            }
        }

        void ParseLegacyMove(byte[] packet)
        {
            int offset = PacketConstants.HeaderSize;
            ushort objectIndex = PacketBuffer.ReadUInt16(packet, ref offset);
            offset += 4; // dest object + srv dist
            float toX = PacketBuffer.ReadFloat(packet, ref offset);
            float toY = PacketBuffer.ReadFloat(packet, ref offset);
            offset += 2; // posZ
            offset += 1; // move mode

            EntityMoved?.Invoke(objectIndex, RoseCoordinates.ServerToUnity(toX, toY));
        }

        void ParseChat(byte[] packet)
        {
            int offset = PacketConstants.HeaderSize;
            ushort objectIndex = PacketBuffer.ReadUInt16(packet, ref offset);
            string message = PacketBuffer.ReadString(packet, ref offset);

            string author = Entities.TryGetValue(objectIndex, out RemoteEntity entity) ? entity.Name : $"#{objectIndex}";
            ChatReceived?.Invoke(objectIndex, $"{author}>{message}");
        }

        void SetPhase(RoseConnectionPhase newPhase, string status)
        {
            phase = newPhase;
            SetStatus(status);
        }

        void SetStatus(string message)
        {
            RoseDebug.Log(message);
            StatusChanged?.Invoke(message);
        }

        static string DescribeLoginError(byte result)
        {
            return result switch
            {
                LoginResult.NotFoundAccount => "Account not found.",
                LoginResult.InvalidPassword => "Invalid password.",
                LoginResult.AlreadyLoggedIn => "Account already logged in.",
                _ => $"Login failed (code {result}).",
            };
        }
    }
}
