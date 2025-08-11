using MapConfiguratorCivilization7;
using MapConfiguratorCivilization7.Helper;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;


public class ScriptSettings
{

    // Terrain
    [HeaderBegin("Terrain", true)]
    [Description("Decreasing the amount of land may require less players")]
    [SliderFloat("Amount of land", -1.0f, 1.0f)]
    public float threshold = 0.0f;

    [Description("Determines how varied the continents are")]
    [SliderFloat("Amount of details", 0, 2.0f)]
    public float detailAmount = 1.0f;

    [Description("The size of terrain variations")]
    [SliderFloat("Detail size", 0.1f, 2.0f)]
    public float detailSize = 0.7f;

    [SliderFloat("Amount of islands", 0, 2.0f)]
    public float islandAmount = 1.0f;

    [SliderFloat("Island size", 0, 2)]
    public float islandThreshold = 1.2f;

    [Debug]
    [SliderFloat("Debug: Warp amplitude", 0.0f, 50.0f, true)]
    public float warpAmp = 3.25f;

    [HeaderEnd]
    [Debug]
    [SliderFloat("Debug: Warp frequency", 0.1f, 10.0f, true)]
    public float warpFreq = 0.4f;

    // Features
    [HeaderBegin("Features", true)]
    [SliderFloat("Ice cap amount", -0.5f, 0.5f)]
    public float iceCapAmount = 0;

    [Debug]
    [SliderFloat("Debug: Mountain fractal", 0.01f, 3.0f, true)]
    public float mountainStrength = 0.9f;

    [SliderFloat("Amount of mountains", 0.0f, 1.5f)]
    public float mountainThreshold = 1.0f;

    [SliderFloat("Amount of volcanos", 0, 4.0f)]
    public float volcanoThreshold = 1;

    [HeaderEnd]
    [SliderFloat("Coastal spread", 0.0f, 1.0f)]
    public float coastalSpread = 0.28f;

    // Biomes
    [HeaderBegin("Biomes", true)]
    [SliderFloat("Plains size", 0.0f, 2.0f)]
    public float plainsMul = 1.0f;

    [SliderFloat("Grassland size", 0.0f, 2.0f)]
    public float grasslandMul = 1.0f;

    [SliderFloat("Tropical size", 0.0f, 2.0f)]
    public float tropicalMul = 1.0f;

    [SliderFloat("Desert size", 0.0f, 2.0f)]
    public float desertMul = 1.0f;

    [SliderFloat("Tundra size", 0.0f, 2.0f)]
    public float tundraMul = 1.0f;

    [HeaderEnd]
    [SliderFloat("Snow size", 0.0f, 2.0f)]
    public float snowAmount = 1.0f;

    [Dropdown("Debug View", "Height", "Groups", "Players", "WindShadow", "Rainfall", "Island")]
    [Debug]
    public string debugView = "Height";
}

public class Script
{
    CancellationToken token;
    MapScript script;
    MapScriptData data;
    ScriptSettings settings;
    XoShiRo128plus random;

    int totalTiles, totalPlayers, groupCount;
    float worldAspectRatio, worldSizeCompensation;

    NodeGroup[] groups;
    float[] mountainMap;
    int[,] landDF;
    int[] neighborCount;
    int[,] landReachableDirectly, landReachableWithCoasts, waterReachable;
    Point[] posPlayers;
    float[] windShadow;
    float[] windShadowNew;
    Vector2[] windDirDynamic;
    float[] rainfallMap;
    List<int> validLandHome = new(), validLandDist = new();

    public Script(MapScript script, CancellationToken token)
    {
        this.script = script;
        this.token = token;
        data = script.data;
        settings = (ScriptSettings)script.settings.workingInstance;
        random = new XoShiRo128plus(data.seed);

        totalTiles = data.width * data.height;
        totalPlayers = data.playerHome + data.playerDistant;
        worldAspectRatio = (data.width / (float)data.height) * MapScriptHelper.HEX_X_MUL;
        worldSizeCompensation = data.height / 80.0f;

        groups = new NodeGroup[30];
        mountainMap = new float[data.width * data.height];
        landDF = new int[data.width * data.height, 3];
        neighborCount = new int[data.width * data.height];
        posPlayers = new Point[totalPlayers];
        windShadow = new float[data.width * data.height];
        windShadowNew = new float[data.width * data.height];
        windDirDynamic = new Vector2[data.width * data.height];
        rainfallMap = new float[data.width * data.height];
    }

    public struct Air
    {
        public float humidity;
        public float temperature;
        public float rainfall;

        public Air(float humidity, float temperature, float rainfall)
        {
            this.humidity = humidity;
            this.temperature = temperature;
            this.rainfall = rainfall;
        }
    }

    public struct Ground
    {
        public float water;
        public float temperature;

    }

    public enum NodeType
    {
        PlayerHome,
        PlayerDist,
        Ocean
    }

    public struct NodeGroup
    {
        public float forceMul;
        public bool isAttractedToCenter;
        public NodeType type;
        public Vector2 pos;
        public Vector2 posNew;
        public Vector2 vel;

        public NodeGroup(NodeType type)
        {
            this.type = type;
        }

        public void Initialize(XoShiRo128plus random)
        {
            pos = new Vector2(-100, -100);
            vel = new Vector2(0, 0);
            forceMul = 1;
            isAttractedToCenter = random.NextBool();
        }
    }

    public void Run()
    {
        int oceanNodeCount = 1 + (int)(totalPlayers / 9.0f);
        while (true)
        {
            groupCount = GroupPlayers(oceanNodeCount);
            if (ComputeGroupPositions())
                break;
        }

        // Debug Group positions
        if (settings.debugView == "Groups")
        {
            for (int i = 0; i < groupCount; ++i)
            {
                int x = (int)((groups[i].pos.X / MapScriptHelper.HEX_X_MUL) * data.height);
                int y = (int)(groups[i].pos.Y * data.height);
                data.debug[x + y * data.width] = groups[i].type == NodeType.PlayerHome ? 1 : (groups[i].type == NodeType.Ocean ? -0.2f : -1);
            }
            script.UpdateMapCallback();
        }

        GenerateMainContinents();

        // Compute distance fields for main continents
        MapScriptHelper.ComputeDistanceFields(data, landDF, 15);

        GenerateIslands();

        // Compute distance fields again to include islands
        MapScriptHelper.ComputeDistanceFields(data, landDF, 25);

        GeneratePoles();
        GenerateMountainsAndVolcanos();
        GenerateCoast();

        landReachableDirectly = MapScriptHelper.ComputeConnectedCounts(data, MapScriptHelper.BIT_LAND, MapScriptHelper.BIT_LAND);
        landReachableWithCoasts = MapScriptHelper.ComputeConnectedCounts(data, MapScriptHelper.BIT_LAND, MapScriptHelper.BIT_LAND | MapScriptHelper.BIT_COAST);
        waterReachable = MapScriptHelper.ComputeConnectedCounts(data, MapScriptHelper.BIT_OCEAN | MapScriptHelper.BIT_COAST, MapScriptHelper.BIT_OCEAN | MapScriptHelper.BIT_COAST);

        IdentifyLakes();
        FindValidLandForSpawns();

        // Determine spawn regions
        PlacePlayers(validLandHome, data.playerHome, 0, NodeType.PlayerHome);
        PlacePlayers(validLandDist, data.playerDistant, data.playerHome, NodeType.PlayerDist);

        // Debug Spawn positions
        if (settings.debugView == "Players")
        {
            for (int i = 0; i < totalPlayers; ++i)
            {
                int x = posPlayers[i].X;
                int y = posPlayers[i].Y;
                data.debug[x + y * data.width] = i < data.playerHome ? 1 : -1;
            }
            script.UpdateMapCallback();
        }

        ComputeWindShadowFromMountains();
        ComputeRainfall();
        ComputeBiomes();

        script.UpdateMapCallback();
    }

    public void GroupNodes(XoShiRo128plus random, NodeGroup[] groups, ref int groupCount, NodeType type, int total, bool forceSingle = false)
    {
        int maxGroupCount = total switch
        {
            >= 0 and <= 4 => 2,
            >= 5 and <= 8 => 3,
            >= 9 and <= 13 => 4,
            _ => 5
        };

        int minGroupCount = total switch
        {
            >= 0 and <= 2 => 1,
            >= 3 and <= 7 => 2,
            _ => 3
        };

        int extraGroupCount = (int)(minGroupCount + (random.NextUint() % ((maxGroupCount - minGroupCount) + 1)));
        if (forceSingle)
            extraGroupCount = total;
        for (int i = 0; i < extraGroupCount; i++)
        {
            groups[groupCount++] = new NodeGroup(type);
        }
    }

    public int GroupPlayers(int oceanNodeCount)
    {
        int totalPlayers = data.playerHome + data.playerDistant;
        int groupCount = 0;
        GroupNodes(random, groups, ref groupCount, NodeType.PlayerHome, data.playerHome);
        GroupNodes(random, groups, ref groupCount, NodeType.PlayerDist, data.playerDistant);
        GroupNodes(random, groups, ref groupCount, NodeType.Ocean, oceanNodeCount, true);

        for (int i = 0; i < groupCount; ++i)
        {
            groups[i].Initialize(random);
        }

        for (int i = 0; i < groupCount; ++i)
        {
            while (true)
            {
                Vector2 newPos = new Vector2(data.width * random.NextFloat(), 0.05f + 0.9f * random.NextFloat());
                bool isValid = true;
                for (int j = 0; j < groupCount; ++j)
                {
                    if (MapScriptHelper.DistanceBetweenPointsWrapped(newPos, groups[j].pos, data.width) < 0.03f)
                        isValid = false;
                }
                if (isValid)
                {
                    groups[i].pos = newPos;
                    break;
                }
            }
        }
        return groupCount;
    }

    public bool ComputeGroupPositions()
    {
        float nodeCountCompensation = 1.0f / (float)Math.Sqrt(groupCount);
        int iter = 0, maxIter = 150;
        while (true)
        {
            for (int i = 0; i < groupCount; ++i)
            {
                // Repel nodes based on how close they are to each other
                Vector2 forceNodeRepulsion = Vector2.Zero;
                for (int j = 0; j < groupCount; ++j)
                {
                    if (i == j) continue;
                    Vector2 dir = MapScriptHelper.GetDirToPosWrapped(groups[i].pos, groups[j].pos, worldAspectRatio);
                    float distSqr = dir.LengthSquared();
                    if (distSqr < 0.00001f)
                        continue;
                    forceNodeRepulsion += -Vector2.Normalize(dir) * groups[j].forceMul * MapScriptHelper.Attenuation(0.05f, distSqr);
                }
                groups[i].vel += forceNodeRepulsion * 0.03f;

                // Repel nodes from poles
                Vector2 forcePoles = Vector2.Zero;
                forcePoles += new Vector2(0, 1) * MapScriptHelper.Attenuation(0.15f, groups[i].pos.Y);
                forcePoles += new Vector2(0, -1) * MapScriptHelper.Attenuation(0.15f, 1.0f - groups[i].pos.Y);
                if (groups[i].isAttractedToCenter)
                    forcePoles *= 2;
                groups[i].vel += (forcePoles / nodeCountCompensation) * 0.01f;

                // Damp velocity and compute new node position
                groups[i].vel *= 0.85f;
                groups[i].posNew = groups[i].pos + groups[i].vel;
                if ((groups[i].posNew.Y < 0.15f && groups[i].vel.Y < 0) || (groups[i].posNew.Y < 1.0f - 0.15f && groups[i].vel.Y > 0))
                    groups[i].vel.Y = 0;
                groups[i].posNew.X = MapScriptHelper.Wrap(groups[i].posNew.X, worldAspectRatio);
                groups[i].posNew.Y = Math.Clamp(groups[i].posNew.Y, 0.075f, 1.0f - 0.075f);
            }

            // Swap node position for next iteration
            for (int i = 0; i < groupCount; ++i)
            {
                (groups[i].posNew, groups[i].pos) = (groups[i].pos, groups[i].posNew);
            }

            // Check if all nodes have a valid position
            iter++;
            if (iter % 10 == 0 && iter >= 30)
            {
                bool isValid = true;
                for (int i = 0; i < groupCount; ++i)
                {
                    float minDist = 99999;
                    for (int j = 0; j < groupCount; ++j)
                    {
                        if (i == j)
                            continue;

                        float dist = MapScriptHelper.DistanceBetweenPointsWrapped(groups[i].pos, groups[j].pos, worldAspectRatio);
                        minDist = Math.Min(minDist, dist);
                    }
                    if (minDist < 0.3f * nodeCountCompensation)
                        isValid = false;
                    if (minDist < 0.0001f)
                        groups[i].vel.X = random.NextFloat() - 0.5f;
                }
                if (isValid) return true;
            }

            // Reset simulation if no valid state found for too long
            if (iter > maxIter)
                return false;
            token.ThrowIfCancellationRequested();
        }

    }

    public void GenerateMainContinents()
    {
        // Compute main continents
        float worldSizeCompensationNoise = MathHelper.Lerp(1, worldSizeCompensation, 0.4f);
        FastNoiseLite noiseHeight = MapScriptHelper.CreateNoise(data.seed + 1, FastNoiseLite.FractalType.FBm, (worldSizeCompensationNoise / settings.detailSize), 1.4f, 0.5f, 8);
        FastNoiseLite noiseHeightFreq = MapScriptHelper.CreateNoise(data.seed + 2, FastNoiseLite.FractalType.Billow, 0.045f * 12, 1.4f, 0.4f, 3);
        FastNoiseLite warpTerrain = MapScriptHelper.CreateWarp(data.seed + 5, FastNoiseLite.FractalType.DomainWarpProgressive, settings.warpAmp, settings.warpFreq, 1.4f, 0.3f, 5);
        float nodeCountCompensation = 1.0f / (float)Math.Sqrt(groupCount);
        //-1688249423
        for (int y = 0; y < data.height; y++)
        {
            for (int x = 0; x < data.width; x++)
            {
                int index = x + y * data.width;
                Vector2 hexPos = MapScriptHelper.PosOfHex(new(x, y)) / data.height;
                Vector3 hexPosWrapped = MapScriptHelper.Map2dPosTo3dCylinder(hexPos, worldAspectRatio);

                // Warp noise to create unique features
                float warpVariation = (float)Math.Pow(Math.Clamp((noiseHeightFreq.GetNoise(hexPosWrapped.X, hexPosWrapped.Y, hexPosWrapped.Z) + 0.95f), 0, 1), 2);
                warpTerrain.SetFrequency((worldSizeCompensationNoise / settings.detailSize) * settings.warpFreq * MathHelper.Lerp(1, warpVariation, 0.5f));
                warpTerrain.SetDomainWarpAmp(settings.warpAmp * MathHelper.Lerp(1, warpVariation, 0.6f));
                Vector3 hexPosWrappedAndWarped = MapScriptHelper.WarpCylinder(warpTerrain, hexPosWrapped);

                // Compute node distance factors
                float distanceHome = 99999, distanceDist = 99999, minDist = 99999;
                float spacerFac = 0;
                for (int i = 0; i < groupCount; i++)
                {
                    float dist = Math.Max(0, MapScriptHelper.DistanceBetweenPointsWrapped(hexPos, groups[i].pos, worldAspectRatio) * 1 - 0.02f * nodeCountCompensation);
                    if (groups[i].type == NodeType.Ocean)
                    {
                        spacerFac += MapScriptHelper.Attenuation(0.075f, dist);
                        continue;
                    }
                    minDist = Math.Min(minDist, dist);
                    if (groups[i].type == NodeType.PlayerHome)
                        distanceHome = Math.Min(distanceHome, dist);
                    else if (groups[i].type == NodeType.PlayerDist)
                        distanceDist = Math.Min(distanceDist, dist);
                }

                // Compute land height of continents
                float distanceHomeDist = Math.Abs(distanceHome - distanceDist);
                float heightOffset = 0.3f;
                heightOffset += 5.0f * MapScriptHelper.Attenuation(0.05f, minDist);
                heightOffset += 0.5f * (float)Math.Pow(MapScriptHelper.InvAttenuation(nodeCountCompensation * 0.1f, distanceHomeDist), 2);
                heightOffset -= 5.0f * MapScriptHelper.Attenuation((nodeCountCompensation / worldSizeCompensation) * 0.015f, Math.Max(0, distanceHomeDist - (3.0f / data.height)));
                heightOffset -= 2 * spacerFac;
                heightOffset -= 5.0f * MapScriptHelper.Attenuation(0.03f, hexPos.Y);
                heightOffset -= 5.0f * MapScriptHelper.Attenuation(0.03f, 1.0f - hexPos.Y);

                float finalHeight = heightOffset + (noiseHeight.GetNoise(hexPosWrappedAndWarped.X, hexPosWrappedAndWarped.Y, hexPosWrappedAndWarped.Z)) * 3.5f * settings.detailAmount;
                if (settings.debugView.Equals("Height"))
                    data.debug[index] = finalHeight - settings.threshold * -1.2f;

                // Apply height to tile
                var tile = new MapTile();
                tile.SetHomeOrDistant(distanceDist < distanceHome);
                bool isLand = finalHeight > settings.threshold * -1.2f;
                tile.SetType(isLand ? MapTileType.Continent : MapTileType.Water);
                tile.SetBiome(isLand ? MapTileBiome.Grassland : MapTileBiome.Ocean);
                data.tiles[index] = tile;
            }
            token.ThrowIfCancellationRequested();
        }
    }

    public void GenerateIslands()
    {
        FastNoiseLite noiseIslands = MapScriptHelper.CreateNoise(data.seed, FastNoiseLite.FractalType.FBm, 0.20f * 6, 1.4f, 0.7f, 4);
        FastNoiseLite warpIslands = MapScriptHelper.CreateWarp(data.seed + 10, FastNoiseLite.FractalType.DomainWarpIndependent, 0.012f, 0.20f * 4, 1.4f, 0.5f, 5);
        for (int y = 0; y < data.height; y++)
        {
            for (int x = 0; x < data.width; x++)
            {
                int index = x + y * data.width;
                Vector2 hexPos = MapScriptHelper.PosOfHex(new(x, y)) / data.height;
                Vector3 hexPosWrapped = MapScriptHelper.Map2dPosTo3dCylinder(hexPos, worldAspectRatio);
                Vector3 hexPosWrappedAndWarped = MapScriptHelper.WarpCylinder(warpIslands, hexPosWrapped);

                int df = Math.Min(landDF[index, 1], landDF[index, 2]);
                float distanceFieldFac = MapScriptHelper.InvAttenuation(1, (Math.Max(0, df - 2) * 0.8f) / worldSizeCompensation);
                float islandFacLarge = MathHelper.Clamp((float)Math.Pow(noiseIslands.GetNoise(hexPosWrapped.X * 0.3f, hexPosWrapped.Y * 0.3f + 15, hexPosWrapped.Z * 0.3f) * (8 * settings.islandAmount), 4), 0, 1);

                float heightIsland = (noiseIslands.GetNoise(hexPosWrappedAndWarped.X * 1.5f, hexPosWrappedAndWarped.Y * 1.5f - 15, hexPosWrappedAndWarped.Z * 2) + 0.5f) * distanceFieldFac * islandFacLarge;
                heightIsland -= 2.0f * MapScriptHelper.Attenuation(0.01f, hexPos.Y);
                heightIsland -= 2.0f * MapScriptHelper.Attenuation(0.01f, 1.0f - hexPos.Y);

                var tile = data.tiles[index];
                if (heightIsland > -settings.islandThreshold * 0.5f + 1.0f)
                    tile.SetType(MapTileType.Island);
                data.tiles[index] = tile;

                if (settings.debugView == "Island")
                    data.debug[index] = heightIsland - (-settings.islandThreshold * 0.5f + 1.0f);
            }
            token.ThrowIfCancellationRequested();
        }

    }

    public void GeneratePoles()
    {
        FastNoiseLite warpIce = MapScriptHelper.CreateWarp(data.seed, FastNoiseLite.FractalType.DomainWarpIndependent, 2.0f, 0.20f * 8, 1.4f, 0.5f, 5);
        FastNoiseLite noiseIce = MapScriptHelper.CreateNoise(data.seed + 1, FastNoiseLite.FractalType.Billow, 0.045f * 20, 1.4f, 0.5f, 8);
        for (int y = 0; y < data.height; y++)
        {
            for (int x = 0; x < data.width; x++)
            {
                int index = x + y * data.width;
                Vector2 hexPos = MapScriptHelper.PosOfHex(new(x, y)) / data.height;
                Vector3 hexPosWrapped = MapScriptHelper.Map2dPosTo3dCylinder(hexPos, worldAspectRatio);
                Vector3 hexPosWrappedAndWarped = MapScriptHelper.WarpCylinder(warpIce, hexPosWrapped);

                int df = Math.Min(landDF[index, 0], Math.Min(landDF[index, 1], landDF[index, 2]));
                float poleFacIce = MapScriptHelper.InvAttenuation(1, (Math.Max(0, df) * 0.2f) / worldSizeCompensation);
                float noiseValueIce = noiseIce.GetNoise(hexPosWrappedAndWarped.X, hexPosWrappedAndWarped.Y + 10, hexPosWrappedAndWarped.Z) * poleFacIce * 1.4f - 0.85f;
                noiseValueIce += 2.75f * poleFacIce * MapScriptHelper.Attenuation(0.04f, hexPos.Y);
                noiseValueIce += 2.75f * poleFacIce * MapScriptHelper.Attenuation(0.04f, 1.0f - hexPos.Y);

                var tile = data.tiles[index];
                if (noiseValueIce > -settings.iceCapAmount)
                {
                    tile.SetBiome(MapTileBiome.Ocean);
                    tile.SetFeature(MapTileMainFeature.Ice);
                }
                data.tiles[index] = tile;

            }
            token.ThrowIfCancellationRequested();
        }
    }

    public void GenerateMountainsAndVolcanos()
    {
        FastNoiseLite noiseMountains = MapScriptHelper.CreateNoise(data.seed + 3, FastNoiseLite.FractalType.Billow, 4.0f * worldSizeCompensation, 1.35f, settings.mountainStrength, 5);
        FastNoiseLite noiseMountainsSmooth = MapScriptHelper.CreateNoise(data.seed + 3, FastNoiseLite.FractalType.Billow, 4.0f * worldSizeCompensation, 1.35f, 0.5f, 5);
        FastNoiseLite warpMountains = MapScriptHelper.CreateWarp(data.seed + 5, FastNoiseLite.FractalType.DomainWarpIndependent, 1.5f, 0.4f * worldSizeCompensation, 1.4f, 0.4f, 5);
        for (int y = 0; y < data.height; y++)
        {
            for (int x = 0; x < data.width; x++)
            {
                int index = x + y * data.width;

                var tile = data.tiles[index];
                if (tile.GetType() == MapTileType.Water)
                    continue;

                Vector2 hexPos = MapScriptHelper.PosOfHex(new(x, y)) / data.height;
                Vector3 hexPosWrapped = MapScriptHelper.Map2dPosTo3dCylinder(hexPos, worldAspectRatio);
                Vector3 hexPosWrappedAndWarped = MapScriptHelper.WarpCylinder(warpMountains, hexPosWrapped);

                float mountainValue = (noiseMountains.GetNoise(hexPosWrappedAndWarped.X, hexPosWrappedAndWarped.Y, hexPosWrappedAndWarped.Z) + 1) * 0.5f - 0.83f;
                float mountainValue2 = (noiseMountainsSmooth.GetNoise(hexPosWrappedAndWarped.X, hexPosWrappedAndWarped.Y, hexPosWrappedAndWarped.Z) + 1) * 0.5f - 0.83f;
                mountainMap[index] = (float)Math.Tanh((mountainValue2 - settings.mountainThreshold * -0.36f) * 4) * 0.5f + 0.5f;

                if (mountainValue > settings.mountainThreshold * -0.36f)
                {
                    tile.SetMorph(MapTileMorph.Mountainous);

                    // Volcanoes
                    float volcanoValue = (mountainValue - settings.mountainThreshold * -0.36f) * (float)Math.Pow(settings.volcanoThreshold * 1.5f, 2);
                    if (random.NextFloat() < volcanoValue)
                        tile.SetFeature(MapTileMainFeature.Volcano);
                    data.tiles[index] = tile;
                }
            }
            token.ThrowIfCancellationRequested();
        }
    }

    public void GenerateCoast()
    {
        for (int i = 0; i < 10; ++i)
        {
            for (int y = 0; y < data.height; y++)
            {
                for (int x = 0; x < data.width; x++)
                {
                    int index = x + y * data.width;
                    bool anyLand = false;
                    int coastalNeighborCount = 0;
                    int maxCoastalNeighborCount = 0;
                    var tile = data.tiles[index];

                    foreach (var neighbor in MapScriptHelper.GetHexNeighbors(x, y, data))
                    {
                        int newIndex = neighbor.X + neighbor.Y * data.width;
                        var newTile = data.tiles[newIndex];
                        if (newTile.GetType() != MapTileType.Water)
                            anyLand = true;
                        if (newTile.GetBiome() == MapTileBiome.Coastal)
                            coastalNeighborCount++;
                        maxCoastalNeighborCount = Math.Max(maxCoastalNeighborCount, neighborCount[newIndex]);
                    }

                    neighborCount[index] = coastalNeighborCount;
                    if (tile.GetType() == MapTileType.Water)
                    {
                        if (anyLand)
                            tile.SetBiome(MapTileBiome.Coastal);
                        else if (i > 0)
                        {
                            int dfCont = Math.Min(landDF[index, 1], landDF[index, 2]);
                            int dfDifIslandCont = Math.Abs(dfCont - landDF[index, 0]);
                            int dfDifHomeDist = Math.Abs(landDF[index, 1] - landDF[index, 2]);

                            bool isValid = dfDifIslandCont > 1;
                            if (landDF[index, 0] > dfCont)
                                isValid = isValid && dfDifHomeDist > 1;

                            if (isValid)
                            {
                                float expansionProb = maxCoastalNeighborCount switch
                                {
                                    1 => 1.0f,
                                    2 => 0.75f,
                                    3 => 0.1f,
                                    4 => 0.2f,
                                    5 => 0.3f,
                                    6 => 0.4f,
                                    _ => 0
                                };
                                if ((expansionProb * settings.coastalSpread) * worldSizeCompensation >= random.NextFloat() && coastalNeighborCount > 0)
                                    tile.SetBiome(MapTileBiome.Coastal);
                            }
                        }

                    }
                    data.tiles[index] = tile;
                }
                token.ThrowIfCancellationRequested();
            }
        }
    }

    public void IdentifyLakes()
    {
        for (int y = 0; y < data.height; y++)
        {
            for (int x = 0; x < data.width; x++)
            {
                if (data.tiles[x + y * data.width].GetType() == MapTileType.Water)
                {
                    if (waterReachable[x, y] < 12 * worldSizeCompensation)
                        data.tiles[x + y * data.width].SetBiome(MapTileBiome.Lake);
                }
            }
        }
    }

    public void FindValidLandForSpawns()
    {
        for (int y = 0; y < data.height; y++)
        {
            for (int x = 0; x < data.width; x++)
            {
                int df = Math.Min(landDF[x + y * data.width, 1], landDF[x + y * data.width, 2]);
                if (data.tiles[x + y * data.width].GetType() == MapTileType.Continent && df <= -2 && data.tiles[x + y * data.width].GetMorph() != MapTileMorph.Mountainous)
                {
                    if (data.tiles[x + y * data.width].IsHomeOrDistant())
                        validLandDist.Add(x + y * data.width);
                    else
                        validLandHome.Add(x + y * data.width);
                }
            }
            token.ThrowIfCancellationRequested();
        }
    }

    public void PlacePlayers(List<int> validLand, int playerCount, int startOffset, NodeType nodeType)
    {
        float minDistThreshhold = 0.6f, minLandRequiredFac = 1;
        float worldAspectRatio = (data.width / (float)data.height) * MapScriptHelper.HEX_X_MUL;
        float worldSizeCompensation = data.height / 80.0f;
        int tries = 0;
        bool isPlayerDistant = nodeType == NodeType.PlayerDist;
        while (true)
        {
            tries++;
            bool isValid = true;
            for (int p = 0; p < playerCount; ++p)
            {
                bool isPlayerValid = false;
                for (int i = 0; i < 5; ++i)
                {
                    int randPosIndex = validLand[(int)(random.NextUint() % validLand.Count)];
                    Point randPos = new Point(randPosIndex % data.width, randPosIndex / data.width);

                    bool isPosValid = landReachableWithCoasts[randPos.X, randPos.Y] > 70 * worldSizeCompensation * minLandRequiredFac;
                    isPosValid = isPosValid && landReachableDirectly[randPos.X, randPos.Y] > 25 * worldSizeCompensation * minLandRequiredFac;
                    if (isPosValid)
                    {
                        isPlayerValid = true;
                        posPlayers[startOffset + p] = randPos;
                        break;
                    }
                }
                isValid = isValid && isPlayerValid;
            }
            
            if (isValid)
            {
                // Check for validity again
                bool isValidFinal = true;
                for (int i = 0; i < playerCount; i++)
                {
                    float contFac = 1.0f / MathHelper.Lerp(1, MapScriptHelper.Attenuation(80, landReachableWithCoasts[posPlayers[startOffset + i].X, posPlayers[startOffset + i].Y]), 0.5f);
                    float minDist = 99999;
                    for (int j = 0; j < playerCount; j++)
                    {
                        if (i == j)
                            continue;
                        Vector2 pos1 = new Vector2((posPlayers[startOffset + i].X / (float)data.height) * MapScriptHelper.HEX_X_MUL, (posPlayers[startOffset + i].Y / (float)data.height));
                        Vector2 pos2 = new Vector2((posPlayers[startOffset + j].X / (float)data.height) * MapScriptHelper.HEX_X_MUL, (posPlayers[startOffset + j].Y / (float)data.height));
                        float dist = MapScriptHelper.DistanceBetweenPointsWrapped(pos1, pos2, worldAspectRatio) * contFac;

                        minDist = Math.Min(minDist, dist);
                    }

                    if (minDist < minDistThreshhold)
                        isValidFinal = false;
                }

                if (!isValidFinal)
                {
                    minDistThreshhold *= tries < 1500 ? 0.99975f : (tries < 3000 ? 0.999f : 0.995f);
                    minDistThreshhold = Math.Max(minDistThreshhold, 0.02f);
                    continue;
                }
                Console.WriteLine(tries + " | " + minDistThreshhold);
                break;
            }
            else if (tries > 3000)
            {
                minLandRequiredFac *= 0.995f;
            }

                token.ThrowIfCancellationRequested();
        }
    }

    public void ComputeWindShadowFromMountains()
    {
        FastNoiseLite noiseWindDir = MapScriptHelper.CreateNoise(data.seed + 5, FastNoiseLite.FractalType.FBm, 0.2f * 2, 1.4f, 0.4f, 3);
        for (int i = 0; i < 15; ++i)
        {
            for (int y = 0; y < data.height; y++)
            {
                for (int x = 0; x < data.width; x++)
                {
                    int index = x + y * data.width;
                    Vector2 curPos = MapScriptHelper.PosOfHex(new Point(x, y)) / data.height;
                    Vector3 curPosWrapped = MapScriptHelper.Map2dPosTo3dCylinder(curPos, worldAspectRatio);
                    if (i == 0)
                    {
                        windShadow[index] = 1.0f;
                        windShadowNew[index] = 1.0f;
                        float curNoise = noiseWindDir.GetNoise(curPosWrapped.X, curPosWrapped.Y, curPosWrapped.Z) + 0.7f;
                        windDirDynamic[index] = new Vector2((float)Math.Cos(curNoise * 2 * Math.PI), (float)Math.Sin(curNoise * 2 * Math.PI));
                    }
                    else
                    {
                        float totalDotFac = 0, curWindShadow = 0;
                        Vector2 windDirectionDynamic = windDirDynamic[index];

                        for (int n = 0; n < 6; ++n)
                        {
                            int isOdd = y % 2;
                            Point offset = MapScriptHelper.neighborOffsets[n + 6 * isOdd];
                            int newX = (x + offset.X + data.width) % data.width;
                            int newY = y + offset.Y;
                            if (newY < 0 || newY >= data.height)
                                continue;

                            int newIndex = newX + newY * data.width;
                            Vector2 nextPos = MapScriptHelper.PosOfHex(new Point(x + offset.X, newY)) / data.height;
                            Vector2 dir = Vector2.Normalize(curPos - nextPos);
                            float dotFac = (float)Math.Pow(Math.Max(0, Vector2.Dot(dir, windDirectionDynamic)), 2);
                            float extraDot = 1;
                            if (data.tiles[newIndex].GetMorph() == MapTileMorph.Mountainous)
                                extraDot *= 0.5f;

                            curWindShadow += windShadow[newIndex] * dotFac * extraDot;
                            totalDotFac += dotFac;
                        }
                        curWindShadow /= totalDotFac;
                        curWindShadow = Math.Min(curWindShadow + 0.025f, 1.0f);
                        windShadowNew[index] = MathHelper.Lerp(windShadow[index], curWindShadow, 0.5f);

                        if (settings.debugView == "WindShadow")
                            data.debug[index] = (float)Math.Pow(Math.Min(1, curWindShadow + 0.4f), 3);
                    }
                }
                token.ThrowIfCancellationRequested();
            }
            (windShadowNew, windShadow) = (windShadow, windShadowNew);
            token.ThrowIfCancellationRequested();
        }
    }

    public void ComputeRainfall()
    {
        for (int y = 0; y < data.height; y++)
        {
            for (int x = 0; x < data.width; x++)
            {
                int index = x + y * data.width;
                Vector2 curPos = MapScriptHelper.PosOfHex(new Point(x, y)) / data.height;
                Vector3 curPosWrapped = MapScriptHelper.Map2dPosTo3dCylinder(curPos, worldAspectRatio);
                float waterDF = -Math.Min(0, Math.Min(landDF[index, 0], Math.Min(landDF[index, 1], landDF[index, 2])) + 1);
                float rainfall = MathHelper.Lerp(1, MapScriptHelper.Attenuation(10, waterDF), 0.5f);
                float shadow = (float)Math.Pow(Math.Min(1, windShadow[index] + 0.3f), 3);
                if (data.tiles[index].GetMorph() == MapTileMorph.Mountainous)
                    shadow = MathHelper.Lerp(1, shadow, 0.2f);
                rainfall *= MathHelper.Lerp(1, shadow, 0.85f);
                if (data.tiles[index].GetType() == MapTileType.Water)
                    rainfall = 0;
                rainfallMap[index] = rainfall;

                if (settings.debugView == "Rainfall")
                    data.debug[index] = rainfall;
            }
            token.ThrowIfCancellationRequested();
        }
    }

    public void ComputeBiomes()
    {
        FastNoiseLite noiseDesert = MapScriptHelper.CreateNoise(data.seed + 5, FastNoiseLite.FractalType.FBm, 0.2f * 4, 1.4f, 0.6f, 4);
        FastNoiseLite noiseGrassland = MapScriptHelper.CreateNoise(data.seed + 6, FastNoiseLite.FractalType.FBm, 0.2f * 10, 1.4f, 0.5f, 5);
        for (int y = 0; y < data.height; y++)
        {
            for (int x = 0; x < data.width; x++)
            {
                int index = x + y * data.width;
                Vector2 curPos = MapScriptHelper.PosOfHex(new Point(x, y)) / data.height;
                Vector3 curPosWrapped = MapScriptHelper.Map2dPosTo3dCylinder(curPos, worldAspectRatio);

                var tile = data.tiles[index];
                if (tile.GetType() == MapTileType.Water)
                    continue;

                float temp = (float)Math.Pow(Math.Cos((Math.Abs(0.5f - curPos.Y)) * Math.PI), 4);
                float mountain = mountainMap[index];
                float desertNoiseValue = noiseDesert.GetNoise(curPosWrapped.X, curPosWrapped.Y, curPosWrapped.Z);
                float grasslandNoiseValue = noiseGrassland.GetNoise(curPosWrapped.X, curPosWrapped.Y, curPosWrapped.Z);
                float desertLatitudeFac = (float)Math.Pow(Math.Max(0.4f, (1.0f / 0.125f) * ((0.15f - Math.Abs(0.15f - Math.Abs((0.5f + desertNoiseValue * 0.5f) - curPos.Y))) - 0.025f)), 0.5f);

                float plains = settings.plainsMul * 0.76f * MathHelper.Lerp(1, 1.0f - mountain, 0.7f) * (1.0f - Math.Abs(0.5f - rainfallMap[index])) * Math.Max(0, 1.0f - Math.Abs(0.85f - temp) * 1.25f);
                float desert = settings.desertMul * 1.2f * (1.0f - rainfallMap[index] * 0.8f) * MathHelper.Lerp(1, temp, 0.5f) * MathHelper.Lerp(1, desertLatitudeFac, 0.8f);
                float grassland = settings.grasslandMul * 0.29f * Math.Max(0.25f, 1.0f + grasslandNoiseValue * 1.5f) * MathHelper.Lerp(1, 1.0f - mountain, 0.4f) * MathHelper.Lerp(1, temp, 0.5f);
                float tundra = settings.tundraMul * 1.12f * MathHelper.Lerp(1, mountain, 0.25f) * Math.Max(0.25f, 1.0f - temp * 1.5f) * Math.Max(0.2f, 1.0f - Math.Abs(0.25f - rainfallMap[index]) * 1.5f);
                float tropical = settings.tropicalMul * 0.77f * (rainfallMap[index]) * (float)Math.Pow(temp, 1.5) * (1 - desertLatitudeFac * 0.6f);
                
                if (plains > desert && plains > grassland && plains > tundra && plains > tropical)
                    tile.SetBiome(MapTileBiome.Plains);
                else if (desert > plains && desert > grassland && desert > tundra && desert > tropical)
                    tile.SetBiome(MapTileBiome.Desert);
                else if (grassland > desert && grassland > plains && grassland > tundra && grassland > tropical)
                    tile.SetBiome(MapTileBiome.Grassland);
                else if (tundra > desert && tundra > plains && tundra > grassland && tundra > tropical)
                    tile.SetBiome(MapTileBiome.Tundra);
                else
                    tile.SetBiome(MapTileBiome.Tropical);

                data.tiles[index] = tile;
            }
            token.ThrowIfCancellationRequested();
        }
    }
}
