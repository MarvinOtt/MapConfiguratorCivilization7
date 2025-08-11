/**
 * Base game map script - Produces widely varied continents.
 * @packageDocumentation
 */
console.log("Generating using script randomWorlds.js");
import { addMountains, addHills, buildRainfallMap, generateLakes } from '/base-standard/maps/elevation-terrain-generator.js';
import { designateBiomes } from '/base-standard/maps/feature-biome-generator.js';
import * as globals from '/base-standard/maps/map-globals.js';
import * as utilities from '/base-standard/maps/map-utilities.js';
import { addNaturalWonders } from '/base-standard/maps/natural-wonder-generator.js';
import { generateResources } from '/base-standard/maps/resource-generator.js';
import { addTundraVolcanoes, addVolcanoes } from '/base-standard/maps/volcano-generator.js';
import { assignAdvancedStartRegions } from '/base-standard/maps/assign-advanced-start-region.js';
import { generateDiscoveries } from '/base-standard/maps/discovery-generator.js';
import { generateSnow, dumpPermanentSnow } from '/base-standard/maps/snow-generator.js';

import { dumpStartSectors, dumpContinents, dumpTerrain, dumpElevation, dumpRainfall, dumpBiomes, dumpFeatures, dumpResources, dumpNoisePredicate } from '/base-standard/maps/map-debug-helpers.js';

import { updateRegionsForStartBias, pickStartPlot } from '/randomWorlds/maps/starting.js';
import { addFeatures } from '/randomWorlds/maps/features.js';
import { MapData, MapTile, MapTileType, MapTileBiome, MapTileMorph, MapTileMainFeature }  from '/randomWorlds/maps/mapData.js';
import * as Helper from '/randomWorlds/maps/helper.js';
import { Point, Vector2, Vector3 } from '/randomWorlds/maps/helper.js';
import { XoShiRo128plus } from '/randomWorlds/maps/xoShiRo128plus.js';
import { SETTINGS } from '/randomWorlds/settings/randomWorlds.js';
import FastNoiseLite from '/randomWorlds/maps/FastNoiseLite.js';


function requestMapData(initParams) {
    console.log(initParams.width);
    console.log(initParams.height);
    console.log(initParams.topLatitude);
    console.log(initParams.bottomLatitude);
    console.log(initParams.wrapX);
    console.log(initParams.wrapY);
    console.log(initParams.mapSize);
    engine.call("SetMapInitData", initParams);
}

// Register listeners.
engine.on('RequestMapInitData', requestMapData);
engine.on('GenerateMap', generateMap);

export const NodeType = {
    PlayerHome: 0,
    PlayerDist: 1,
    Ocean: 2
};

export class NodeGroup {
    constructor(type) {
        this.size = 1;
        this.forceMul = 0;
        this.isAttractedToCenter = false;
        this.type = type;
        this.pos = new Vector2(-100, -100);
        this.posNew = new Vector2(0, 0);
        this.vel = new Vector2(0, 0);
    }

    addSize(sizeIncrease) {
        this.size += sizeIncrease;
    }

    initialize(random) {
        this.pos = new Vector2(-100, -100);
        this.vel = new Vector2(0, 0);
        this.forceMul = 1;
        this.isAttractedToCenter = random.nextBool();
    }
}

function generateMap() {
    // Check events
    let naturalWonderEvent = false;
    let requestedNaturalWonders = [];
    let liveEventDBRow = GameInfo.GlobalParameters.lookup("REGISTERED_RACE_TO_WONDERS_EVENT");
    if (liveEventDBRow && liveEventDBRow.Value != "0") {
        naturalWonderEvent = true;
        requestedNaturalWonders.push("FEATURE_BERMUDA_TRIANGLE");
    }
    liveEventDBRow = GameInfo.GlobalParameters.lookup("REGISTERED_MARVELOUS_MOUNTAINS_EVENT");
    if (liveEventDBRow && liveEventDBRow.Value != "0") {
        naturalWonderEvent = true;
        requestedNaturalWonders.push("FEATURE_MOUNT_EVEREST");
    }

    console.log("Generating a random world map!");
    console.log(`Age - ${GameInfo.Ages.lookup(Game.age).AgeType}`);

    // Prepare data
    let uiMapSize = GameplayMap.getMapSize();
    let mapInfo = GameInfo.Maps.lookup(uiMapSize);
    if (mapInfo == null)
        return;

    const playerIds = Players.getAliveIds();
    const playerHome = Math.min(mapInfo.PlayersLandmass1, playerIds.length);
    const playerDist = playerIds.length - playerHome;
    let data = new MapData(GameplayMap.getGridWidth(), GameplayMap.getGridHeight(), playerHome, playerDist, GameplayMap.getRandomSeed());
    let random = new XoShiRo128plus(data.seed);
    const worldAspectRatio = (data.width / data.height) * Helper.HEX_X_MUL;
    const worldSizeCompensation = data.height / 80.0;
    let landDF = new Array(data.tileCount * 3);

    console.log("Computing group positions");
    let groups = new Array(30);
    const oceanNodeCount = 1 + Math.trunc(data.totalPlayers / 9.0);
    let groupCount = 0;
    while (true) {
        groupCount = groupPlayers(data, random, groups, oceanNodeCount);
        if (computeGroupPositions(random, groups, groupCount, worldAspectRatio))
            break;
    }

    console.log("Generating the main continents");
    generateMainContinents(data, groups, groupCount, worldAspectRatio, worldSizeCompensation);

    // Compute distance fields for main continents
    Helper.computeDistanceFields(data, landDF, 15);

    console.log("Generating islands");
    generateIslands(data, landDF, worldAspectRatio, worldSizeCompensation);

    // Compute distance fields again to include islands
    Helper.computeDistanceFields(data, landDF, 25);

    console.log("Generating poles");
    generatePoles(data, landDF, worldAspectRatio, worldSizeCompensation);

    console.log("Generating mountains and volcanos");
    let mountainMap = generateMountainsAndVolcanos(data, random, worldAspectRatio, worldSizeCompensation);

    console.log("Generating coast");
    generateCoast(data, random, landDF, worldSizeCompensation);

    let landReachableDirectly = Helper.computeConnectedCounts(data, 1, 1);
    let landReachableWithCoasts = Helper.computeConnectedCounts(data, 1, 1 | 4);
    let waterReachable = Helper.computeConnectedCounts(data, 2 | 4, 2 | 4);

    console.log("Identify exisiting lakes");
    identifyLakes(data, waterReachable, worldSizeCompensation);

    console.log("Find spawn regions");
    let validLandHome = [], validLandDist = [];
    findValidLandForSpawns(data, landDF, validLandHome, validLandDist);
    let homeLandPositions = [], distLandPositions = [];
    computePlayerLocation(data, random, homeLandPositions, landReachableWithCoasts, landReachableDirectly, validLandHome, data.playerHome, NodeType.PlayerHome, worldAspectRatio, worldSizeCompensation);
    computePlayerLocation(data, random, distLandPositions, landReachableWithCoasts, landReachableDirectly, validLandDist, data.playerDistant, NodeType.PlayerDist, worldAspectRatio, worldSizeCompensation);

    // Convert MapData for game
    for (let y = 0; y < data.height; y++) {
        for (let x = 0; x < data.width; x++) {
            const index = x + y * data.width;
            let tile = data.tiles[index];
            if (tile.getType() !== MapTileType.Water) {
                TerrainBuilder.setPlotTag(x, y, PlotTags.PLOT_TAG_NONE);    
                TerrainBuilder.addPlotTag(x, y, PlotTags.PLOT_TAG_LANDMASS);
                TerrainBuilder.addPlotTag(x, y, tile.getType() == MapTileType.Island ? PlotTags.PLOT_TAG_ISLAND : (tile.isHomeOrDistant() ? PlotTags.PLOT_TAG_EAST_LANDMASS : PlotTags.PLOT_TAG_WEST_LANDMASS));
                TerrainBuilder.setTerrainType(x, y, tile.getMorph() === MapTileMorph.Mountainous ? globals.g_MountainTerrain : globals.g_FlatTerrain);
            } else {
                TerrainBuilder.setPlotTag(x, y, PlotTags.PLOT_TAG_WATER);
                TerrainBuilder.addPlotTag(x, y, tile.isHomeOrDistant() ? PlotTags.PLOT_TAG_EAST_WATER : PlotTags.PLOT_TAG_WEST_WATER);
                TerrainBuilder.setTerrainType(x, y, tile.getBiome() == MapTileBiome.Coastal || tile.getBiome() == MapTileBiome.Lake ? globals.g_CoastTerrain : globals.g_OceanTerrain);
            }
        }
    }
    TerrainBuilder.validateAndFixTerrain();
    AreaBuilder.recalculateAreas();
    TerrainBuilder.stampContinents();

    // Place volcanos from MapData
    for (let y = 0; y < data.height; y++) {
        for (let x = 0; x < data.width; x++) {
            let tile = data.tiles[x + y * data.width];
            if (tile.getFeature() === MapTileMainFeature.Volcano) {
                const featureParam = { Feature: globals.g_VolcanoFeature, Direction: -1, Elevation: 0 };
                TerrainBuilder.setFeatureType(x, y, featureParam);
            }
        }
    }

    generateLakes(data.width, data.height, mapInfo.LakeGenerationFrequency);
    AreaBuilder.recalculateAreas();
    TerrainBuilder.buildElevation();
    addHills(data.width, data.height);
    buildRainfallMap(data.width, data.height);
    TerrainBuilder.modelRivers(5, 15, globals.g_NavigableRiverTerrain);
    TerrainBuilder.validateAndFixTerrain();
    TerrainBuilder.defineNamedRivers();

    console.log("Compute Biomes");
    let windShadowMap = computeWindShadowFromMountains(data, worldAspectRatio);
    let rainfallMap = computeRainfall(data, landDF, windShadowMap);
    computeBiomes(data, random, mountainMap, rainfallMap, worldAspectRatio);

    for (let y = 0; y < data.height; y++) {
        for (let x = 0; x < data.width; x++) {
            const index = x + y * data.width;
            let tile = data.tiles[index];
            if (tile.getType() === MapTileType.Water) {
                TerrainBuilder.setBiomeType(x, y, globals.g_MarineBiome);
            } else if (tile.getBiome() === MapTileBiome.Plains) {
                TerrainBuilder.setBiomeType(x, y, globals.g_PlainsBiome);
            } else if (tile.getBiome() === MapTileBiome.Tropical) {
                TerrainBuilder.setBiomeType(x, y, globals.g_TropicalBiome);
            } else if (tile.getBiome() === MapTileBiome.Desert) {
                TerrainBuilder.setBiomeType(x, y, globals.g_DesertBiome);
            } else if (tile.getBiome() === MapTileBiome.Grassland) {
                TerrainBuilder.setBiomeType(x, y, globals.g_GrasslandBiome);
            } else if (tile.getBiome() === MapTileBiome.Tundra) {
                TerrainBuilder.setBiomeType(x, y, globals.g_TundraBiome);
            }
        }
    }

    addTundraVolcanoes(data.width, data.height);
    addNaturalWonders(data.width, data.height, mapInfo.NumNaturalWonders, naturalWonderEvent, requestedNaturalWonders);
    TerrainBuilder.addFloodplains(4, 10);

    addFeatures(data.width, data.height);

    // Place features from MapData
    const iceFeatureIndex = GameInfo.Features.findIndex(feature => feature.FeatureType === "FEATURE_ICE");
    for (let y = 0; y < data.height; y++) {
        for (let x = 0; x < data.width; x++) {
            let tile = data.tiles[x + y * data.width];
            if (tile.getFeature() === MapTileMainFeature.Ice) {
                const featureParam = { Feature: iceFeatureIndex, Direction: -1, Elevation: 0 };
                TerrainBuilder.setFeatureType(x, y, featureParam);
            }
        }
    }

    TerrainBuilder.validateAndFixTerrain();
    AreaBuilder.recalculateAreas();
    TerrainBuilder.storeWaterData();
    generateSnow(data.width, data.height);
    generateResources(data.width, data.height);

    console.log("Find actual start position for players");
    let startPositions = findActualStartLocation(data, homeLandPositions, distLandPositions);

    generateDiscoveries(data.width, data.height, startPositions);
    FertilityBuilder.recalculate();
    assignAdvancedStartRegions();

    console.log("Finished generating a random world map!");
}

function groupNodes(random, groups, groupCount, type, total, forceSingle = false) {
    let maxGroupCount;
    if (total >= 0 && total <= 4) maxGroupCount = 2;
    else if (total >= 5 && total <= 8) maxGroupCount = 3;
    else if (total >= 9 && total <= 13) maxGroupCount = 4;
    else maxGroupCount = 5;

    let minGroupCount;
    if (total >= 0 && total <= 2) minGroupCount = 1;
    else if (total >= 3 && total <= 7) minGroupCount = 2;
    else minGroupCount = 3;

    let extraGroupCount = minGroupCount + (random.nextUint() % ((maxGroupCount - minGroupCount) + 1));
    if (forceSingle)
        extraGroupCount = total;
    for (let i = 0; i < extraGroupCount; i++) {
        groups[groupCount++] = new NodeGroup(type);
    }

    return groupCount;
}

function groupPlayers(data, random, groups, oceanNodeCount) {
    let groupCount = 0;
    groupCount = groupNodes(random, groups, groupCount, NodeType.PlayerHome, data.playerHome);
    groupCount = groupNodes(random, groups, groupCount, NodeType.PlayerDist, data.playerDistant);
    groupCount = groupNodes(random, groups, groupCount, NodeType.Ocean, oceanNodeCount, true);

    for (let i = 0; i < groupCount; i++) {
        groups[i].initialize(random);
    }

    for (let i = 0; i < groupCount; i++) {
        while (true) {
            const newPos = new Vector2(data.width * random.nextFloat(), 0.05 + 0.9 * random.nextFloat());
            let isValid = true;
            for (let j = 0; j < groupCount; j++) {
                if (Helper.distanceBetweenPointsWrapped(newPos, groups[j].pos, data.width) < 0.03) {
                    isValid = false;
                    break;
                }
            }
            if (isValid) {
                groups[i].pos = newPos;
                break;
            }
        }
    }
    return groupCount;
}

function computeGroupPositions(random, groups, groupCount, worldAspectRatio) {
    const nodeCountCompensation = 1.0 / Math.sqrt(groupCount);
    let iter = 0;
    const maxIter = 150;

    while (true) {
        for (let i = 0; i < groupCount; i++) {
            // Repel nodes from each other
            let forceNodeRepulsion = new Vector2(0, 0);
            for (let j = 0; j < groupCount; j++) {
                if (i === j) continue;
                const dir = Helper.getDirToPosWrapped(groups[i].pos, groups[j].pos, worldAspectRatio);
                const distSqr = dir.lengthSquared();
                if (distSqr < 0.00001) continue;
                forceNodeRepulsion = forceNodeRepulsion.add(
                    dir.normalize().multiplySingle(-groups[j].forceMul * Helper.attenuation(0.05, distSqr))
                );
            }
            groups[i].vel = groups[i].vel.add(forceNodeRepulsion.multiplySingle(0.03));

            // Repel from poles
            let forcePoles = new Vector2(0, 0);
            forcePoles = forcePoles.add(new Vector2(0, 1).multiplySingle(Helper.attenuation(0.15, groups[i].pos.y)));
            forcePoles = forcePoles.add(new Vector2(0, -1).multiplySingle(Helper.attenuation(0.15, 1.0 - groups[i].pos.y)));
            if (groups[i].isAttractedToCenter) forcePoles = forcePoles.multiplySingle(2);
            groups[i].vel = groups[i].vel.add(forcePoles.multiplySingle(0.01 / nodeCountCompensation));

            // Velocity damping
            groups[i].vel = groups[i].vel.multiplySingle(0.85);
            groups[i].posNew = groups[i].pos.add(groups[i].vel);

            if ((groups[i].posNew.y < 0.15 && groups[i].vel.y < 0) || (groups[i].posNew.y < 1.0 - 0.15 && groups[i].vel.y > 0)) {
                groups[i].vel = new Vector2(groups[i].vel.x, 0);
            }

            groups[i].posNew = new Vector2(
                Helper.wrap(groups[i].posNew.x, worldAspectRatio),
                Math.min(Math.max(groups[i].posNew.y, 0.075), 1.0 - 0.075)
            );
        }

        // Swap positions
        for (let i = 0; i < groupCount; i++) {
            const tmp = groups[i].posNew;
            groups[i].posNew = groups[i].pos;
            groups[i].pos = tmp;
        }

        // Validity check
        iter++;
        if (iter % 10 === 0 && iter >= 30) {
            let isValid = true;
            for (let i = 0; i < groupCount; i++) {
                let minDist = Infinity;
                for (let j = 0; j < groupCount; j++) {
                    if (i === j) continue;
                    const dist = Helper.distanceBetweenPointsWrapped(groups[i].pos, groups[j].pos, worldAspectRatio);
                    minDist = Math.min(minDist, dist);
                }
                if (minDist < 0.3 * nodeCountCompensation) isValid = false;
                if (minDist < 0.0001) {
                    groups[i].vel = new Vector2(random.nextFloat() - 0.5, groups[i].vel.y);
                }
            }
            if (isValid) return true;
        }

        if (iter > maxIter) return false;
    }
}

function generateMainContinents(data, groups, groupCount, worldAspectRatio, worldSizeCompensation) {
    const worldSizeCompensationNoise = Helper.lerp(1, worldSizeCompensation, 0.4);
    const noiseHeight = Helper.createNoise(data.seed + 1, FastNoiseLite.FractalType.FBm, (worldSizeCompensationNoise / SETTINGS.detailSize), 1.4, 0.5, 8);
    const noiseHeightFreq = Helper.createNoise(data.seed + 2, FastNoiseLite.FractalType.Billow, 0.045 * 12, 1.4, 0.4, 3);
    const warpTerrain = Helper.createWarp(data.seed + 5, FastNoiseLite.FractalType.DomainWarpProgressive, SETTINGS.warpAmp, SETTINGS.warpFreq, 1.4, 0.3, 5);
    const nodeCountCompensation = 1.0 / Math.sqrt(groupCount);

    for (let y = 0; y < data.height; y++) {
        for (let x = 0; x < data.width; x++) {
            const index = x + y * data.width;
            const hexPos = Helper.posOfHex({ x, y }).divSingle(data.height);
            const hexPosWrapped = Helper.map2dPosTo3dCylinder(hexPos, worldAspectRatio);

            // Warp noise to create unique features
            const warpVariation = Math.pow(Math.min(Math.max(noiseHeightFreq.GetNoise(hexPosWrapped.x, hexPosWrapped.y, hexPosWrapped.z) + 0.95, 0), 1), 2);
            warpTerrain.SetFrequency((worldSizeCompensationNoise / SETTINGS.detailSize) * SETTINGS.warpFreq * Helper.lerp(1, warpVariation, 0.5));
            warpTerrain.SetDomainWarpAmp(SETTINGS.warpAmp * Helper.lerp(1, warpVariation, 0.6));
            const hexPosWrappedAndWarped = Helper.warpCylinder(warpTerrain, hexPosWrapped);

            // Compute node distance factors
            let distanceHome = 99999, distanceDist = 99999, minDist = 99999;
            let spacerFac = 0;

            for (let i = 0; i < groupCount; i++) {
                const dist = Math.max(0, Helper.distanceBetweenPointsWrapped(hexPos, groups[i].pos, worldAspectRatio) * 1 - 0.02 * nodeCountCompensation);

                if (groups[i].type === NodeType.Ocean) {
                    spacerFac += Helper.attenuation(0.075, dist);
                    continue;
                }

                minDist = Math.min(minDist, dist);

                if (groups[i].type === NodeType.PlayerHome) {
                    distanceHome = Math.min(distanceHome, dist);
                } else if (groups[i].type === NodeType.PlayerDist) {
                    distanceDist = Math.min(distanceDist, dist);
                }
            }

            // Compute land height of continents
            const distanceHomeDist = Math.abs(distanceHome - distanceDist);
            let heightOffset = 0.3;
            heightOffset += 5.0 * Helper.attenuation(0.05, minDist);
            heightOffset += 0.5 * Math.pow(Helper.invAttenuation(nodeCountCompensation * 0.1, distanceHomeDist), 2);
            heightOffset -= 5.0 * Helper.attenuation((nodeCountCompensation / worldSizeCompensation) * 0.015, Math.max(0, distanceHomeDist - (3.0 / data.height)));
            heightOffset -= 2 * spacerFac;
            heightOffset -= 5.0 * Helper.attenuation(0.03, hexPos.y);
            heightOffset -= 5.0 * Helper.attenuation(0.03, 1.0 - hexPos.y);

            const finalHeight = heightOffset + (noiseHeight.GetNoise(hexPosWrappedAndWarped.x, hexPosWrappedAndWarped.y, hexPosWrappedAndWarped.z)) * 3.5 * SETTINGS.detailAmount;

            // Apply height to tile
            const tile = new MapTile();
            tile.setHomeOrDistant(distanceDist < distanceHome);
            const isLand = finalHeight > SETTINGS.threshold * -1.2;
            tile.setType(isLand ? MapTileType.Continent : MapTileType.Water);
            tile.setBiome(isLand ? MapTileBiome.Grassland : MapTileBiome.Ocean);
            data.tiles[index] = tile;
        }
    }
}

function generateIslands(data, landDF, worldAspectRatio, worldSizeCompensation) {
    const noiseIslands = Helper.createNoise(data.seed, FastNoiseLite.FractalType.FBm, 0.20 * 6, 1.4, 0.7, 4);
    const warpIslands = Helper.createWarp(data.seed + 10, FastNoiseLite.FractalType.DomainWarpIndependent, 0.012, 0.20 * 4, 1.4, 0.5, 5);

    for (let y = 0; y < data.height; y++) {
        for (let x = 0; x < data.width; x++) {
            const index = x + y * data.width;
            const hexPos = Helper.posOfHex({ x, y }).divSingle(data.height);
            const hexPosWrapped = Helper.map2dPosTo3dCylinder(hexPos, worldAspectRatio);
            const hexPosWrappedAndWarped = Helper.warpCylinder(warpIslands, hexPosWrapped);

            const df = Math.min(landDF[index * 3 + 1], landDF[index * 3 + 2]);
            const distanceFieldFac = Helper.invAttenuation(1, (Math.max(0, df - 2) * 0.8) / worldSizeCompensation);
            const islandFacLarge = Helper.clamp(Math.pow(noiseIslands.GetNoise(hexPosWrapped.x * 0.3, hexPosWrapped.y * 0.3 + 15, hexPosWrapped.z * 0.3) * (8 * SETTINGS.islandAmount), 4), 0, 1);

            let heightIsland = (noiseIslands.GetNoise(hexPosWrappedAndWarped.x * 1.5, hexPosWrappedAndWarped.y * 1.5 - 15, hexPosWrappedAndWarped.z * 2) + 0.5) * distanceFieldFac * islandFacLarge;

            heightIsland -= 2.0 * Helper.attenuation(0.01, hexPos.y);
            heightIsland -= 2.0 * Helper.attenuation(0.01, 1.0 - hexPos.y);

            const tile = data.tiles[index];
            if (heightIsland > -SETTINGS.islandThreshold * 0.5 + 1.0) {
                tile.setType(MapTileType.Island);
            }
            data.tiles[index] = tile;
        }
    }
}

function generatePoles(data, landDF, worldAspectRatio, worldSizeCompensation) {
    const warpIce = Helper.createWarp(data.seed, FastNoiseLite.FractalType.DomainWarpIndependent, 2.0, 0.20 * 8, 1.4, 0.5, 5);
    const noiseIce = Helper.createNoise(data.seed + 1, FastNoiseLite.FractalType.Billow, 0.045 * 20, 1.4, 0.5, 8);

    for (let y = 0; y < data.height; y++) {
        for (let x = 0; x < data.width; x++) {
            const index = x + y * data.width;
            const hexPos = Helper.posOfHex({ x, y }).divSingle(data.height);
            const hexPosWrapped = Helper.map2dPosTo3dCylinder(hexPos, worldAspectRatio);
            const hexPosWrappedAndWarped = Helper.warpCylinder(warpIce, hexPosWrapped);

            const df = Math.min(landDF[index * 3 + 0], Math.min(landDF[index * 3 + 1], landDF[index * 3 + 2]));
            const poleFacIce = Helper.invAttenuation(1, (Math.max(0, df) * 0.2) / worldSizeCompensation);

            let noiseValueIce = noiseIce.GetNoise(hexPosWrappedAndWarped.x, hexPosWrappedAndWarped.y + 10, hexPosWrappedAndWarped.z) * poleFacIce * 1.4 - 0.85;

            noiseValueIce += 2.75 * poleFacIce * Helper.attenuation(0.04, hexPos.y);
            noiseValueIce += 2.75 * poleFacIce * Helper.attenuation(0.04, 1.0 - hexPos.y);

            const tile = data.tiles[index];
            if (noiseValueIce > -SETTINGS.iceCapAmount) {
                tile.setBiome(MapTileBiome.Ocean);
                tile.setFeature(MapTileMainFeature.Ice);

            }
            data.tiles[index] = tile;
        }
    }
}

function generateMountainsAndVolcanos(data, random, worldAspectRatio, worldSizeCompensation) {
    const noiseMountains = Helper.createNoise(data.seed + 3, FastNoiseLite.FractalType.Billow, 4.0 * worldSizeCompensation, 1.35, SETTINGS.mountainStrength, 5);
    const noiseMountainsSmooth = Helper.createNoise(data.seed + 3, FastNoiseLite.FractalType.Billow, 4.0 * worldSizeCompensation, 1.35, 0.5, 5);
    const warpMountains = Helper.createWarp(data.seed + 5, FastNoiseLite.FractalType.DomainWarpIndependent, 1.5, 0.4 * worldSizeCompensation, 1.4, 0.4, 5);
    let mountainMap = new Array(data.tileCount);

    for (let y = 0; y < data.height; y++) {
        for (let x = 0; x < data.width; x++) {
            const index = x + y * data.width;
            const tile = data.tiles[index];
            if (tile.getType() === MapTileType.Water) continue;

            const hexPos = Helper.posOfHex({ x, y }).divSingle(data.height);
            const hexPosWrapped = Helper.map2dPosTo3dCylinder(hexPos, worldAspectRatio);
            const hexPosWrappedAndWarped = Helper.warpCylinder(warpMountains, hexPosWrapped);

            const mountainValue = (noiseMountains.GetNoise(hexPosWrappedAndWarped.x, hexPosWrappedAndWarped.y, hexPosWrappedAndWarped.z) + 1) * 0.5 - 0.83;

            const mountainValue2 = (noiseMountainsSmooth.GetNoise(hexPosWrappedAndWarped.x, hexPosWrappedAndWarped.y, hexPosWrappedAndWarped.z) + 1) * 0.5 - 0.83;

            mountainMap[index] = Math.tanh((mountainValue2 - SETTINGS.mountainThreshold * -0.36) * 4) * 0.5 + 0.5;

            if (mountainValue > SETTINGS.mountainThreshold * -0.36) {
                tile.setMorph(MapTileMorph.Mountainous);

                const volcanoValue = (mountainValue - SETTINGS.mountainThreshold * -0.36) * Math.pow(SETTINGS.volcanoThreshold * 1.5, 2);

                if (random.nextFloat() < volcanoValue) {
                    tile.setFeature(MapTileMainFeature.Volcano);
                }
                data.tiles[index] = tile;
            }
        }
    }
    return mountainMap;
}


function generateCoast(data, random, landDF, worldSizeCompensation)
{
    let neighborCount = new Array(data.totalTiles);
    for (let i = 0; i < 10; ++i) {
        for (let y = 0; y < data.height; y++) {
            for (let x = 0; x < data.width; x++) {
                const index = x + y * data.width;
                let anyLand = false;
                let coastalNeighborCount = 0;
                let maxCoastalNeighborCount = 0;
                const tile = data.tiles[index];

                const neighbors = Helper.getHexNeighbors(x, y, data);
                for (const n of neighbors) {
                    const newIndex = n.x + n.y * data.width;
                    const newTile = data.tiles[newIndex];
                    if (newTile.getType() !== MapTileType.Water) {
                        anyLand = true;
                    }
                    if (newTile.getBiome() === MapTileBiome.Coastal) {
                        coastalNeighborCount++;
                    }
                    maxCoastalNeighborCount = Math.max(maxCoastalNeighborCount, neighborCount[newIndex]);
                }

                neighborCount[index] = coastalNeighborCount;
                if (tile.getType() == MapTileType.Water) {
                    if (anyLand) {
                        tile.setBiome(MapTileBiome.Coastal);
                    }

                    else if (i > 0) {
                        const dfCont = Math.min(landDF[index * 3 + 1], landDF[index * 3 + 2]);
                        const dfDifIslandCont = Math.abs(dfCont - landDF[index * 3 + 0]);
                        const dfDifHomeDist = Math.abs(landDF[index * 3 + 1] - landDF[index * 3 + 2]);

                        let isValid = dfDifIslandCont > 1;
                        if (landDF[index * 3 + 0] > dfCont)
                            isValid = isValid && dfDifHomeDist > 1;

                        if (isValid) {
                            let expansionProb;
                            if (maxCoastalNeighborCount == 1) expansionProb = 1.0;
                            else if (maxCoastalNeighborCount == 2) expansionProb = 0.75;
                            else if (maxCoastalNeighborCount == 3) expansionProb = 0.1;
                            else if (maxCoastalNeighborCount == 4) expansionProb = 0.2;
                            else if (maxCoastalNeighborCount == 5) expansionProb = 0.3;
                            else if (maxCoastalNeighborCount == 6) expansionProb = 0.4;
                            else expansionProb = 0;

                            if ((expansionProb * SETTINGS.coastalSpread) * worldSizeCompensation >= random.nextFloat() && coastalNeighborCount > 0) {
                                tile.setBiome(MapTileBiome.Coastal);
                            }

                        }
                    }

                }
                data.tiles[index] = tile;
            }
        }
    }
}

function identifyLakes(data, waterReachable, worldSizeCompensation)
{
    for (let y = 0; y < data.height; y++) {
        for (let x = 0; x < data.width; x++) {
            if (data.tiles[x + y * data.width].getType() === MapTileType.Water) {
                if (waterReachable[x, y] < 12 * worldSizeCompensation)
                    data.tiles[x + y * data.width].setBiome(MapTileBiome.Lake);
            }
        }
    }
}

function findValidLandForSpawns(data, landDF, validLandHome, validLandDist)
{
    for (let y = 0; y < data.height; y++) {
        for (let x = 0; x < data.width; x++) {
            const index = x + y * data.width;
            let df = Math.min(landDF[index * 3 + 1], landDF[index * 3 + 2]);
            if (data.tiles[index].getType() === MapTileType.Continent && df <= -2 && data.tiles[index].getMorph() !== MapTileMorph.Mountainous) {
                if (data.tiles[index].isHomeOrDistant()) {
                    validLandDist.push(index);
                } else {
                    validLandHome.push(index);
                }

            }
        }
    }
}

function computePlayerLocation(data, random, posPlayers, landReachableWithCoasts, landReachableDirectly, validLand, playerCount, nodeType, worldAspectRatio, worldSizeCompensation)
{
    let minDistThreshhold = 0.6, minLandRequiredFac = 1;
    let tries = 0;
    while (true) {
        if (tries > 40000) {
            console.log("timeout player spawn");
            break;
        }
        tries++;
        let isValid = true;
        for (let p = 0; p < playerCount; ++p)
        {
            let isPlayerValid = false;
            for (let i = 0; i < 5; ++i)
            {
                const randPosIndex = validLand[random.nextUint() % validLand.length];
                const randPos = new Point(randPosIndex % data.width, Math.trunc(randPosIndex / data.width));

                let isPosValid = landReachableWithCoasts[randPosIndex] > 70 * worldSizeCompensation * minLandRequiredFac;
                isPosValid = isPosValid && landReachableDirectly[randPosIndex] > 25 * worldSizeCompensation * minLandRequiredFac;
                if (isPosValid) {
                    isPlayerValid = true;
                    posPlayers[p] = randPos;
                    break;
                }
            }
            isValid = isValid && isPlayerValid;
        }

        if (isValid) {
            // Check for validity again
            let isValidFinal = true;
            for (let i = 0; i < playerCount; i++)
            {
                let contFac = 1.0 / Helper.lerp(1, Helper.attenuation(80, landReachableWithCoasts[posPlayers[i].x + posPlayers[i].y * data.width]), 0.5);
                let minDist = 99999;
                for (let j = 0; j < playerCount; j++)
                {
                    if (i == j)
                        continue;
                    const pos1 = new Vector2((posPlayers[i].x / data.height) * Helper.HEX_X_MUL, (posPlayers[i].y / data.height));
                    const pos2 = new Vector2((posPlayers[j].x / data.height) * Helper.HEX_X_MUL, (posPlayers[j].y / data.height));
                    const dist = Helper.distanceBetweenPointsWrapped(pos1, pos2, worldAspectRatio) * contFac;

                    minDist = Math.min(minDist, dist);
                }

                if (minDist < minDistThreshhold)
                    isValidFinal = false;
            }

            if (!isValidFinal) {
                minDistThreshhold *= tries < 1500 ? 0.99975: (tries < 3000 ? 0.999: 0.995);
                minDistThreshhold = Math.max(minDistThreshhold, 0.02);
                continue;
            }
            console.log("PlayerSpawn: " + tries + " | " + minDistThreshhold);
            break;
        }
        else if (tries > 3000) {
            minLandRequiredFac *= 0.995;
        }
    }
}

function findActualStartLocation(data, homeLandPositions, distLandPositions) {

    let tempPlayers = [];
    for (let playerIndex = 0; playerIndex < data.totalPlayers; playerIndex++) {
        tempPlayers.push(playerIndex);
    }
    utilities.shuffle(tempPlayers);

    let homePlayers = [], distPlayers = [];
    let homeStartRegions = [], distStartRegions = [];
    for (let i = 0; i < data.playerHome; i++) {
        homePlayers.push(tempPlayers[i]);
        let region = { west: 0, east: 0, south: 0, north: 0, continent: -1 };
        region.west = homeLandPositions[i].x - 3;
        region.east = homeLandPositions[i].x + 3;
        region.south = Math.max(0, homeLandPositions[i].y - 3);
        region.north = Math.min(data.height - 1, homeLandPositions[i].y + 3);
        homeStartRegions.push(region);
    }
    for (let i = 0; i < data.playerDistant; i++) {
        distPlayers.push(tempPlayers[data.playerHome + i]);
        let region = { west: 0, east: 0, south: 0, north: 0, continent: -1 };
        region.west = distLandPositions[i].x - 3;
        region.east = distLandPositions[i].x + 3;
        region.south = Math.max(0, distLandPositions[i].y - 3);
        region.north = Math.min(data.height - 1, distLandPositions[i].y + 3);
        distStartRegions.push(region);
    }

    updateRegionsForStartBias(data, homePlayers, homeStartRegions);
    updateRegionsForStartBias(data, distPlayers, distStartRegions);

    let startPositions = [];
    let aliveMajorIds = Players.getAliveMajorIds();
    for (let i = 0; i < data.playerHome; ++i) {
        let playerIndex = homePlayers[i];
        let playerId = aliveMajorIds[playerIndex];
        let plotIndex = pickStartPlot(data, homeStartRegions[i], i, playerId, false, startPositions);
        if (plotIndex >= 0) {
            startPositions.push(plotIndex);
            let location = GameplayMap.getLocationFromIndex(plotIndex);
            console.log("CHOICE FOR HOME PLAYER: " + playerId + " (" + location.x + ", " + location.y + ")");
            StartPositioner.setStartPosition(plotIndex, playerId);
        } else {
            console.log("FAILED TO PICK LOCATION FOR HOME: " + playerId);
        }
    }

    for (let i = 0; i < data.playerDistant; ++i) {
        let playerIndex = distPlayers[i];
        let playerId = aliveMajorIds[playerIndex];
        let plotIndex = pickStartPlot(data, distStartRegions[i], i, playerId, false, startPositions);
        if (plotIndex >= 0) {
            startPositions.push(plotIndex);
            let location = GameplayMap.getLocationFromIndex(plotIndex);
            console.log("CHOICE FOR DIST PLAYER: " + playerId + " (" + location.x + ", " + location.y + ")");
            StartPositioner.setStartPosition(plotIndex, playerId);
        } else {
            console.log("FAILED TO PICK LOCATION FOR DIST: " + playerId);
        }
    }
    return startPositions;
}

function computeWindShadowFromMountains(data, worldAspectRatio)
{
    let windShadow = new Array(data.totalTiles);
    let windShadowNew = new Array(data.totalTiles);
    let windDirDynamic = new Array(data.totalTiles);
    let noiseWindDir = Helper.createNoise(data.seed + 5, FastNoiseLite.FractalType.FBm, 0.2 * 2, 1.4, 0.4, 3);
    for (let i = 0; i < 15; ++i)
    {
        for (let y = 0; y < data.height; y++)
        {
            for (let x = 0; x < data.width; x++)
            {
                const index = x + y * data.width;
                const curPos = Helper.posOfHex(new Point(x, y)).divSingle(data.height);
                const curPosWrapped = Helper.map2dPosTo3dCylinder(curPos, worldAspectRatio);
                if (i === 0) {
                    windShadow[index] = 1.0;
                    windShadowNew[index] = 1.0;
                    const curNoise = noiseWindDir.GetNoise(curPosWrapped.x, curPosWrapped.y, curPosWrapped.z) + 0.7;
                    windDirDynamic[index] = new Vector2(Math.cos(curNoise * 2 * Math.PI), Math.sin(curNoise * 2 * Math.PI));
                }
                else {
                    let totalDotFac = 0, curWindShadow = 0;
                    const windDirectionDynamic = windDirDynamic[index];


                    for (let n = 0; n < 6; ++n)
                    {
                        const isOdd = y % 2;
                        const offset = Helper.neighborOffsets[n + 6 * isOdd];
                        let newX = (x + offset.x + data.width) % data.width;
                        let newY = y + offset.y;
                        if (newY < 0 || newY >= data.height)
                            continue;

                        const newIndex = newX + newY * data.width;
                        const nextPos = Helper.posOfHex(new Point(x + offset.x, newY)).divSingle(data.height);
                        const dir = curPos.subtract(nextPos).normalize();
                        const dotFac = Math.pow(Math.max(0, dir.dot(windDirectionDynamic)), 2);
                        let extraDot = 1;
                        if (data.tiles[newIndex].getMorph() === MapTileMorph.Mountainous)
                            extraDot *= 0.5;

                        curWindShadow += windShadow[newIndex] * dotFac * extraDot;
                        totalDotFac += dotFac;
                    }
                    curWindShadow /= totalDotFac;
                    curWindShadow = Math.min(curWindShadow + 0.025, 1.0);
                    windShadowNew[index] = Helper.lerp(windShadow[index], curWindShadow, 0.5);

                }
            }
        }
        [windShadowNew, windShadow] = [windShadow, windShadowNew];
    }
    return windShadow;
}

function computeRainfall(data, landDF, windShadow)
{
    let rainfallMap = new Array(data.totalTiles);
    for (let y = 0; y < data.height; y++)
    {
        for (let x = 0; x < data.width; x++)
        {
            const index = x + y * data.width;
            const waterDF = -Math.min(0, Math.min(landDF[index * 3 + 0], Math.min(landDF[index * 3 + 1], landDF[index * 3 + 2])) + 1);
            let rainfall = Helper.lerp(1, Helper.attenuation(10, waterDF), 0.5);
            let shadow = Math.pow(Math.min(1, windShadow[index] + 0.3), 3);
            if (data.tiles[index].getMorph() === MapTileMorph.Mountainous)
                shadow = Helper.lerp(1, shadow, 0.2);
            rainfall *= Helper.lerp(1, shadow, 0.85);
            if (data.tiles[index].getType() === MapTileType.Water)
                rainfall = 0;
            rainfallMap[index] = rainfall;

        }
    }
    return rainfallMap
}

function computeBiomes(data, random, mountainMap, rainfallMap, worldAspectRatio)
{
    const noiseDesert = Helper.createNoise(data.seed + 5, FastNoiseLite.FractalType.FBm, 0.2 * 4, 1.4, 0.6, 4);
    const noiseGrassland = Helper.createNoise(data.seed + 6, FastNoiseLite.FractalType.FBm, 0.2 * 10, 1.4, 0.5, 5);
    for (let y = 0; y < data.height; y++)
    {
        for (let x = 0; x < data.width; x++)
        {
            const index = x + y * data.width;
            const curPos = Helper.posOfHex(new Point(x, y)).divSingle(data.height);
            const curPosWrapped = Helper.map2dPosTo3dCylinder(curPos, worldAspectRatio);

            var tile = data.tiles[index];
            if (tile.getType() === MapTileType.Water)
                continue;

            const temp = Math.pow(Math.cos((Math.abs(0.5 - curPos.y)) * Math.PI), 4);
            const mountain = mountainMap[index];
            const desertNoiseValue = noiseDesert.GetNoise(curPosWrapped.x, curPosWrapped.y, curPosWrapped.z);
            const grasslandNoiseValue = noiseGrassland.GetNoise(curPosWrapped.x, curPosWrapped.y, curPosWrapped.z);
            const desertLatitudeFac = Math.pow(Math.max(0.4, (1.0 / 0.125) * ((0.15 - Math.abs(0.15 - Math.abs((0.5 + desertNoiseValue * 0.5) - curPos.y))) - 0.025)), 0.5);

            const plains = SETTINGS.plainsMul * 0.76 * Helper.lerp(1, 1.0 - mountain, 0.7) * (1.0 - Math.abs(0.5 - rainfallMap[index])) * Math.max(0, 1.0 - Math.abs(0.85 - temp) * 1.25);
            const desert = SETTINGS.desertMul * 1.2 * (1.0 - rainfallMap[index] * 0.8) * Helper.lerp(1, temp, 0.5) * Helper.lerp(1, desertLatitudeFac, 0.8);
            const grassland = SETTINGS.grasslandMul * 0.29 * Math.max(0.25, 1.0 + grasslandNoiseValue * 1.5) * Helper.lerp(1, 1.0 - mountain, 0.4) * Helper.lerp(1, temp, 0.5);
            const tundra = SETTINGS.tundraMul * 1.12 * Helper.lerp(1, mountain, 0.25) * Math.max(0.25, 1.0 - temp * 1.5) * Math.max(0.2, 1.0 - Math.abs(0.25 - rainfallMap[index]) * 1.5);
            const tropical = SETTINGS.tropicalMul * 0.77 * (rainfallMap[index]) * Math.pow(temp, 1.5) * (1 - desertLatitudeFac * 0.6);

            if (plains > desert && plains > grassland && plains > tundra && plains > tropical)
                tile.setBiome(MapTileBiome.Plains);
            else if (desert > plains && desert > grassland && desert > tundra && desert > tropical)
                tile.setBiome(MapTileBiome.Desert);
            else if (grassland > desert && grassland > plains && grassland > tundra && grassland > tropical)
                tile.setBiome(MapTileBiome.Grassland);
            else if (tundra > desert && tundra > plains && tundra > grassland && tundra > tropical)
                tile.setBiome(MapTileBiome.Tundra);
            else
                tile.setBiome(MapTileBiome.Tropical);

            data.tiles[index] = tile;
        }
    }
}