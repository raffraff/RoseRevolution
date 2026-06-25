using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityRose;
using UnityRose.Formats;
using UnityRose.Game;

/// <summary>
/// Loads ROSE zone maps at runtime using the same layout as RoseTerrainWindow.ImportMap.
/// </summary>
public static class RoseMapRuntimeLoader
{
    struct PatchNormalIndex
    {
        public int patchID;
        public int normalID;

        public PatchNormalIndex(int patchID, int normalID)
        {
            this.patchID = patchID;
            this.normalID = normalID;
        }
    }

    static GameObject currentMapRoot;
    static Transform spawnRoot;
    static bool usingSceneMap;

    public static bool HasLoadedMap => spawnRoot != null;
    public static bool IsUsingSceneMap => usingSceneMap;

    public static bool EnsureMapReady(int mapId, RoseClassicGameManager gameManager)
    {
        if (HasLoadedMap)
            return true;

        if (TryBindSceneMap(gameManager))
            return true;

        if (gameManager != null && !gameManager.loadMapAtRuntime)
        {
            RoseDebug.LogWarning("Scene terrain not found. Add a 'Spawn Points' object from map import, assign Scene Spawn Root, or enable Load Map At Runtime.");
            return false;
        }

        return LoadMap(mapId, gameManager);
    }

    public static bool TryBindSceneMap(RoseClassicGameManager gameManager)
    {
        Transform root = gameManager?.sceneSpawnRoot;
        if (root == null)
        {
            var spawnsObject = GameObject.Find("Spawn Points");
            if (spawnsObject != null)
                root = spawnsObject.transform;
        }

        GameObject mapRoot = null;
        if (root != null)
            mapRoot = root.parent != null ? root.parent.gameObject : root.gameObject;
        else
        {
            var roseMap = UnityEngine.Object.FindFirstObjectByType<RoseMap>();
            if (roseMap == null)
                return false;

            mapRoot = roseMap.gameObject;
            root = roseMap.transform.Find("Spawn Points");
            if (root == null)
            {
                var bind = new GameObject("Spawn Points");
                bind.transform.SetParent(roseMap.transform, false);
                bind.transform.position = new Vector3(5200f * 2f, 0f, 0f);
                bind.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
                root = bind.transform;
            }
        }

        spawnRoot = root;
        usingSceneMap = true;
        currentMapRoot = mapRoot;

        Transform startSpawn = root.Find("start");
        if (startSpawn != null && gameManager != null)
            gameManager.spawnPosition = startSpawn.position;

        if (gameManager?.worldManager != null)
        {
            if (gameManager.worldManager.mobSpawner == null)
            {
                var monsters = mapRoot.transform.Find("Monsters");
                if (monsters == null)
                    monsters = GameObject.Find("Monsters")?.transform;
                if (monsters != null)
                    gameManager.worldManager.mobSpawner = monsters.gameObject;
            }
        }

        RoseDebug.Log($"Using scene terrain '{currentMapRoot.name}' (spawn root: {spawnRoot.name}).");
        return true;
    }

    public static Vector3 ServerRawToMapWorld(float serverX, float serverY, float serverZ = 0f)
    {
        Vector3 world = RoseClassic.RoseCoordinates.ServerToUnity(serverX, serverY, serverZ);
        world.y = SampleGroundHeight(world);
        return world;
    }

    public static Vector3 MapWorldToServerRaw(Vector3 world)
    {
        return RoseClassic.RoseCoordinates.UnityToServer(world);
    }

    public static float SampleGroundHeight(Vector3 worldPos)
    {
        int floorMask = LayerMask.GetMask("Floor", "MapObjects");
        if (floorMask == 0)
            return worldPos.y;

        var origin = new Vector3(worldPos.x, worldPos.y + 500f, worldPos.z);
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 1000f, floorMask))
            return hit.point.y;

        return worldPos.y;
    }

    public static bool LoadMap(int mapId, RoseClassicGameManager gameManager)
    {
        if (mapId <= 0)
        {
            RoseDebug.LogWarning($"Invalid map id {mapId}.");
            return false;
        }

        if (usingSceneMap)
            return true;

        if (ResourceManager.Instance == null || ResourceManager.Instance.stb_zone == null)
        {
            RoseDebug.LogError("ResourceManager or stb_zone is not initialized.");
            return false;
        }

        if (currentMapRoot != null && !usingSceneMap)
        {
            spawnRoot = null;
            UnityEngine.Object.Destroy(currentMapRoot);
        }

        usingSceneMap = false;

        var stb = ResourceManager.Instance.stb_zone;
        string zonPath = Utils.FixPath(stb.Cells[mapId][2].ToString());
        string mapDirectoryRelative = Path.GetDirectoryName(zonPath);
        var mapDirectory = Path.Combine(ROSEImport.GetDataPath(), mapDirectoryRelative);

        if (!Directory.Exists(mapDirectory))
        {
            RoseDebug.LogError($"Map directory not found for zone {mapId}: {mapDirectory}");
            return false;
        }

        RoseDebug.Log($"Loading map {mapId} ({stb.Cells[mapId][1]}) from {mapDirectory}");

        var map = new GameObject(stb.Cells[mapId][1].ToString());
        currentMapRoot = map;
        map.AddComponent<RoseMap>();

        var terrain = new GameObject("Ground");
        terrain.transform.SetParent(map.transform, false);
        terrain.layer = LayerMask.NameToLayer("Floor");

        var terrainObjects = new GameObject("Objects");
        terrainObjects.transform.SetParent(map.transform, false);
        terrainObjects.layer = LayerMask.NameToLayer("MapObjects");

        var patches = new List<RosePatch>();
        var atlasRectHash = new Dictionary<string, Rect>();
        var atlasTexHash = new Dictionary<string, Texture2D>();
        var textures = new List<Texture2D>();

        foreach (var dir in new DirectoryInfo(mapDirectory).GetDirectories())
        {
            if (dir.Name.Contains("."))
                continue;

            var patch = new RosePatch(dir);
            if (!patch.Load(mapId))
                continue;

            patch.UpdateAtlas(ref atlasRectHash, ref atlasTexHash, ref textures);
            patches.Add(patch);
        }

        if (patches.Count == 0)
        {
            RoseDebug.LogError($"No terrain patches found for map {mapId}.");
            UnityEngine.Object.Destroy(map);
            currentMapRoot = null;
            spawnRoot = null;
            return false;
        }

        int width;
        int height;
        if (atlasRectHash.Count <= 16) width = height = 4 * 256;
        else if (atlasRectHash.Count <= 32) { width = 8 * 256; height = 4 * 256; }
        else if (atlasRectHash.Count <= 64) { width = 8 * 256; height = 8 * 256; }
        else if (atlasRectHash.Count <= 128) { width = 16 * 256; height = 8 * 256; }
        else if (atlasRectHash.Count <= 256) { width = 16 * 256; height = 16 * 256; }
        else
        {
            RoseDebug.LogError($"Map {mapId} has too many terrain textures for atlas packing.");
            UnityEngine.Object.Destroy(map);
            currentMapRoot = null;
            spawnRoot = null;
            return false;
        }

        var atlas = new Texture2D(width, height);
        Rect[] rects = atlas.PackTextures(textures.ToArray(), 0, Math.Max(width, height));
        atlas.anisoLevel = 11;

        int rectId = 0;
        foreach (string key in atlasTexHash.Keys)
            atlasRectHash[key] = rects[rectId++];

        foreach (RosePatch patch in patches)
            patch.Import(terrain.transform, terrainObjects.transform, atlas, atlas, atlasRectHash);

        var patchNormalLookup = new Dictionary<string, List<PatchNormalIndex>>();
        for (int patchId = 0; patchId < patches.Count; patchId++)
        {
            foreach (string vertex in patches[patchId].edgeVertexLookup.Keys)
            {
                var ids = new List<PatchNormalIndex>();
                foreach (int id in patches[patchId].edgeVertexLookup[vertex])
                    ids.Add(new PatchNormalIndex(patchId, id));

                if (!patchNormalLookup.ContainsKey(vertex))
                    patchNormalLookup.Add(vertex, ids);
                else
                    patchNormalLookup[vertex].AddRange(ids);
            }
        }

        foreach (var entry in patchNormalLookup)
        {
            Vector3 avg = Vector3.zero;
            foreach (PatchNormalIndex index in entry.Value)
                avg += patches[index.patchID].m_mesh.normals[index.normalID];

            avg.Normalize();

            foreach (PatchNormalIndex index in entry.Value)
                patches[index.patchID].m_mesh.normals[index.normalID] = avg;
        }

        terrainObjects.transform.localScale = new Vector3(1.0f, 1.0f, -1.0f);
        terrainObjects.transform.Rotate(0.0f, -90.0f, 0.0f);
        terrainObjects.transform.position = new Vector3(5200.0f, 0.0f, 5200.0f);

        ApplySpawnPoints(map, patches, gameManager);
        SetupMobSpawner(map, gameManager);

        var roseMap = map.GetComponent<RoseMap>();
        roseMap.patches.AddRange(patches);
        roseMap.mapName = map.name;

        RoseDebug.Log($"Map {mapId} loaded ({patches.Count} patches).");
        return true;
    }

    static void ApplySpawnPoints(GameObject map, List<RosePatch> patches, RoseClassicGameManager gameManager)
    {
        if (gameManager == null || patches[0].m_ZON?.SpawnPoints == null)
            return;

        var spawns = new GameObject("Spawn Points");
        spawns.transform.position = new Vector3(5200 * 2F, 0, 0);
        spawns.transform.Rotate(0, -90F, 0);
        spawns.transform.SetParent(map.transform, false);
        spawnRoot = spawns.transform;

        foreach (var spawnPoint in patches[0].m_ZON.SpawnPoints)
        {
            var spawn = new GameObject(spawnPoint.Name);
            spawn.transform.SetParent(spawns.transform, false);
            spawn.transform.localPosition = Utils.r2uScale(spawnPoint.Position);

            if (spawnPoint.Name == "start")
                gameManager.spawnPosition = spawn.transform.position;
        }
    }

    static void SetupMobSpawner(GameObject map, RoseClassicGameManager gameManager)
    {
        if (gameManager?.worldManager == null)
            return;

        var monsters = new GameObject("Monsters");
        monsters.transform.SetParent(map.transform, false);
        monsters.transform.localScale = new Vector3(1.0f, 1.0f, -1.0f);
        monsters.transform.Rotate(0, -90F, 0);
        monsters.transform.position = new Vector3(5200, 0, 5200);

        gameManager.worldManager.mobSpawner = monsters;
    }
}
