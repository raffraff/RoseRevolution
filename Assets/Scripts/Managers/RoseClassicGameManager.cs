using System.Collections.Generic;
using UnityEngine;
using UnityRose;

/// <summary>
/// Handles world spawning and entity sync using the original ROSE protocol.
/// </summary>
public class RoseClassicGameManager : MonoBehaviour
{
    [Header("References")]
    public WorldManager worldManager;
    public GUIController guiController;
    public Vector3 spawnPosition = Vector3.zero;

    [Header("Map")]
    [Tooltip("Import terrain from 3DDATA when entering the world. Leave off if the map is already in the scene.")]
    public bool loadMapAtRuntime = false;
    [Tooltip("Optional. The 'Spawn Points' object from ROSE terrain import. Auto-detected if left empty.")]
    public Transform sceneSpawnRoot;
    public int fallbackMapId = 2;
    public float spawnFacingY = 0f;

    readonly Dictionary<ushort, RosePlayer> playersByObjectIndex = new Dictionary<ushort, RosePlayer>();

    RosePlayer mainPlayer;

    void Awake()
    {
        RoseMapRuntimeLoader.TryBindSceneMap(this);
    }

    void OnEnable()
    {
        if (RoseClassic.RoseNetworkManager.Instance == null)
            return;

        var net = RoseClassic.RoseNetworkManager.Instance;
        net.EnteredWorld += OnEnteredWorld;
        net.CharacterReadyToEnter += OnCharacterReadyToEnter;
        net.EntitySpawned += OnEntitySpawned;
        net.EntityRemoved += OnEntityRemoved;
        net.EntityMoved += OnEntityMoved;
        net.ChatReceived += OnChatReceived;
    }

    void OnDisable()
    {
        if (RoseClassic.RoseNetworkManager.Instance == null)
            return;

        var net = RoseClassic.RoseNetworkManager.Instance;
        net.EnteredWorld -= OnEnteredWorld;
        net.CharacterReadyToEnter -= OnCharacterReadyToEnter;
        net.EntitySpawned -= OnEntitySpawned;
        net.EntityRemoved -= OnEntityRemoved;
        net.EntityMoved -= OnEntityMoved;
        net.ChatReceived -= OnChatReceived;
    }

    void OnCharacterReadyToEnter()
    {
        var net = RoseClassic.RoseNetworkManager.Instance;
        var selected = net.SelectedCharacter;
        if (selected == null)
            return;

        int mapId = selected.ZoneId > 0 ? selected.ZoneId : fallbackMapId;
        try
        {
            if (!RoseMapRuntimeLoader.EnsureMapReady(mapId, this) && loadMapAtRuntime && mapId != fallbackMapId)
                RoseMapRuntimeLoader.EnsureMapReady(fallbackMapId, this);
        }
        catch (System.Exception ex)
        {
            RoseDebug.LogError($"Map setup failed: {ex.Message}");
        }

        Vector3 position = RoseMapRuntimeLoader.ServerRawToMapWorld(
            selected.ServerSpawnX,
            selected.ServerSpawnY,
            selected.ServerSpawnZ);

        if (position.sqrMagnitude < 1f || Vector3.Distance(position, spawnPosition) > 500f)
        {
            position = spawnPosition;
            position.y = RoseMapRuntimeLoader.SampleGroundHeight(position);
        }

        mainPlayer = SpawnFromParts(true, 0, selected.Name, selected.Race, selected.PartItems, position);
        if (mainPlayer?.player != null)
            mainPlayer.player.transform.rotation = Quaternion.Euler(0f, spawnFacingY, 0f);

        if (guiController != null && guiController.characterPreview != null)
            guiController.characterPreview.SetCharacterInformations(selected.Name, 1200, 1200, 960, 960, 1, "Visitor");

        net.SendJoinZone();
    }

    void OnEnteredWorld()
    {
        var net = RoseClassic.RoseNetworkManager.Instance;
        if (mainPlayer != null)
            playersByObjectIndex[net.LocalObjectIndex] = mainPlayer;
    }

    void OnEntitySpawned(RoseClassic.RemoteEntity entity)
    {
        var net = RoseClassic.RoseNetworkManager.Instance;
        if (net != null && entity.ObjectIndex == net.LocalObjectIndex)
            return;

        if (playersByObjectIndex.ContainsKey(entity.ObjectIndex))
            return;

        if (entity.IsPlayer && !string.IsNullOrEmpty(entity.Name))
        {
            var player = SpawnFromParts(false, entity.ObjectIndex, entity.Name, entity.Race, entity.PartItems, entity.Position);
            playersByObjectIndex[entity.ObjectIndex] = player;
        }
    }

    void OnEntityRemoved(ushort objectIndex)
    {
        if (!playersByObjectIndex.TryGetValue(objectIndex, out RosePlayer player))
            return;

        playersByObjectIndex.Remove(objectIndex);
        if (player?.player != null)
            Destroy(player.player);
    }

    void OnEntityMoved(ushort objectIndex, Vector3 destination)
    {
        if (!playersByObjectIndex.TryGetValue(objectIndex, out RosePlayer player))
            return;

        var controller = player.player.GetComponent<PlayerController>();
        if (controller != null)
            controller.destinationPosition = destination;
    }

    void OnChatReceived(ushort objectIndex, string message)
    {
        if (guiController != null && guiController.chatController != null)
            guiController.chatController.AddSystemMessage(message);

        if (playersByObjectIndex.TryGetValue(objectIndex, out RosePlayer player))
        {
            var entityGui = player.player.GetComponentInChildren<EntityGUIController>();
            entityGui?.bubble?.ShowMessage(message.Split('>').Length > 1 ? message.Split('>')[1] : message);
        }
    }

    RosePlayer SpawnFromParts(bool isMain, ushort objectIndex, string name, byte race, uint[] parts, Vector3 position)
    {
        int weaponRight = (int)RoseClassic.PacketIO.PacketBuffer.GetPartItemNo(parts[RoseClassic.BodyPart.WeaponR]);
        int weaponLeft = (int)RoseClassic.PacketIO.PacketBuffer.GetPartItemNo(parts[RoseClassic.BodyPart.WeaponL]);
        int mainWeapon = weaponRight;
        int offHandWeapon = weaponLeft;

        return worldManager.SpawnPlayer(
            isMain,
            RoseClassic.RosePartMapper.RaceToGender(race),
            name,
            (byte)RoseClassic.PacketIO.PacketBuffer.GetPartItemNo(parts[RoseClassic.BodyPart.Hair]),
            (byte)RoseClassic.PacketIO.PacketBuffer.GetPartItemNo(parts[RoseClassic.BodyPart.Face]),
            (int)RoseClassic.PacketIO.PacketBuffer.GetPartItemNo(parts[RoseClassic.BodyPart.Knapsack]),
            (int)RoseClassic.PacketIO.PacketBuffer.GetPartItemNo(parts[RoseClassic.BodyPart.Armor]),
            (int)RoseClassic.PacketIO.PacketBuffer.GetPartItemNo(parts[RoseClassic.BodyPart.Gauntlet]),
            (int)RoseClassic.PacketIO.PacketBuffer.GetPartItemNo(parts[RoseClassic.BodyPart.Boots]),
            (int)RoseClassic.PacketIO.PacketBuffer.GetPartItemNo(parts[RoseClassic.BodyPart.Goggle]),
            (int)RoseClassic.PacketIO.PacketBuffer.GetPartItemNo(parts[RoseClassic.BodyPart.Helmet]),
            mainWeapon,
            offHandWeapon,
            position);
    }
}
