using System;
using System.Collections.Generic;
using UnityEngine;
using UnityRose;

namespace RoseClassic
{
    public class ServerEntry
    {
        public string Name;
        public uint Id;
    }

    public class CharacterEntry
    {
        public byte Slot;
        public string Name;
        public byte Race;
        public short Level;
        public short Job;
        public uint[] PartItems = new uint[BodyPart.Max];
    }

    public class SelectedCharacterData
    {
        public string Name;
        public short ZoneId;
        public Vector3 SpawnPosition;
        public float ServerSpawnX;
        public float ServerSpawnY;
        public float ServerSpawnZ;
        public byte Race;
        public byte FaceId;
        public byte HairId;
        public uint[] PartItems = new uint[BodyPart.Max];
        public ushort ServerObjectIndex;
    }

    public class RemoteEntity
    {
        public ushort ObjectIndex;
        public string Name;
        public Vector3 Position;
        public Vector3 TargetPosition;
        public byte Race;
        public byte Level;
        public short Job;
        public uint[] PartItems = new uint[BodyPart.Max];
        public bool IsPlayer;
    }

    public static class RosePartMapper
    {
        public static void ApplyPartsToModel(CharModel model, uint[] parts, byte race)
        {
            model.gender = race == 0 ? GenderType.MALE : GenderType.FEMALE;
            model.changeID(BodyPartType.FACE, (int)PacketIO.PacketBuffer.GetPartItemNo(parts[BodyPart.Face]));
            model.changeID(BodyPartType.HAIR, (int)PacketIO.PacketBuffer.GetPartItemNo(parts[BodyPart.Hair]));
            model.changeID(BodyPartType.CAP, (int)PacketIO.PacketBuffer.GetPartItemNo(parts[BodyPart.Helmet]));
            model.changeID(BodyPartType.BODY, (int)PacketIO.PacketBuffer.GetPartItemNo(parts[BodyPart.Armor]));
            model.changeID(BodyPartType.ARMS, (int)PacketIO.PacketBuffer.GetPartItemNo(parts[BodyPart.Gauntlet]));
            model.changeID(BodyPartType.FOOT, (int)PacketIO.PacketBuffer.GetPartItemNo(parts[BodyPart.Boots]));
            model.changeID(BodyPartType.FACEITEM, (int)PacketIO.PacketBuffer.GetPartItemNo(parts[BodyPart.Goggle]));
            model.changeID(BodyPartType.BACK, (int)PacketIO.PacketBuffer.GetPartItemNo(parts[BodyPart.Knapsack]));
            model.changeID(BodyPartType.WEAPON, (int)PacketIO.PacketBuffer.GetPartItemNo(parts[BodyPart.WeaponR]));
        }

        public static GenderType RaceToGender(byte race) => race == 0 ? GenderType.MALE : GenderType.FEMALE;
    }

    public static class RoseCoordinates
    {
        public static Vector3 ServerToUnity(float serverX, float serverY, float serverZ = 0)
        {
            return new Vector3(serverX / 100f - 5200f, serverZ / 100f, serverY / 100f - 5200f);
        }

        public static Vector3 UnityToServer(Vector3 unityPosition)
        {
            float serverX = (unityPosition.x + 5200f) * 100f;
            float serverY = (unityPosition.z + 5200f) * 100f;
            return new Vector3(serverX, serverY, unityPosition.y * 100f);
        }
    }
}
