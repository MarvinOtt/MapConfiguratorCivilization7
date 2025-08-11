import { MapData, MapTileType, MapTileBiome, MapTileMorph, MapTileMainFeature } from '/randomWorlds/maps/mapData.js';
import FastNoiseLite from '/randomWorlds/maps/FastNoiseLite.js';

// Constants
export const HEX_X_MUL = (2.0 / 3.0) * Math.sqrt(3.0);

export const lerp = (x, y, a) => x * (1 - a) + y * a;
export const clamp = (a, min = 0, max = 1) => Math.min(max, Math.max(min, a));

// Helper Classes
export class Point {
    constructor(x, y) {
        this.x = x;
        this.y = y;
    }
}

export class Vector2 {
    constructor(x, y) {
        this.x = x;
        this.y = y;
    }

    dot(v) {
        return this.x * v.x + this.y * v.y;
    }

    subtract(v) {
        return new Vector2(this.x - v.x, this.y - v.y);
    }

    add(v) {
        return new Vector2(this.x + v.x, this.y + v.y);
    }

    multiply(v) {
        return new Vector2(this.x * v.x, this.y * v.y);
    }

    multiplySingle(v) {
        return new Vector2(this.x * v, this.y * v);
    }

    divSingle(v) {
        return new Vector2(this.x / v, this.y / v);
    }

    normalize() {
        const lengthCur = this.length();
        return new Vector2(this.x / lengthCur, this.y / lengthCur);
    }

    length() {
        return Math.hypot(this.x, this.y);
    }

    lengthSquared() {
        return Math.pow(Math.hypot(this.x, this.y), 2);
    }

    static fromPolar(r, angle) {
        return new Vector2(r * Math.cos(angle), r * Math.sin(angle));
    }
}

export class Vector3 {
    constructor(x, y, z) {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    subtract(v) {
        return new Vector3(this.x - v.x, this.y - v.y, this.z - v.z);
    }

    add(v) {
        return new Vector3(this.x + v.x, this.y + v.y, this.z + v.z);
    }

    length() {
        return Math.sqrt(this.x ** 2 + this.y ** 2 + this.z ** 2);
    }

    normalize() {
        const len = this.length();
        return len === 0 ? new Vector3(0, 0, 0) : new Vector3(this.x / len, this.y / len, this.z / len);
    }
}

export const neighborOffsets = [
    new Point(-1, -1), new Point(0, -1), new Point(-1, 0),
    new Point(1, 0), new Point(-1, 1), new Point(0, 1),
    new Point(0, -1), new Point(1, -1), new Point(-1, 0),
    new Point(1, 0), new Point(0, 1), new Point(1, 1)
];

// Helper functions
export function wrap(x, m) {
    return ((x % m) + m) % m;
}

export function attenuation(scale, x) {
    return scale / (scale + x);
}

export function invAttenuation(scale, x) {
    return 1.0 - scale / (scale + x);
}

export function posOfHex(hexPos) {
    return new Vector2(
        (hexPos.x + (hexPos.y % 2) * 0.5) * HEX_X_MUL,
        hexPos.y
    );
}

export function distanceBetweenPointsWrapped(pos1, pos2, width) {
    let yDif = Math.abs(pos1.y - pos2.y);
    let xDif = Math.abs(pos1.x - pos2.x);
    xDif = Math.min(xDif, width - xDif);
    return Math.hypot(xDif, yDif);
}

export function getDirToPosWrapped(pos, posOther, width) {
    const dir1 = posOther.subtract(pos);
    const dir2 = posOther.add(new Vector2(width, 0)).subtract(pos);
    const dir3 = posOther.add(new Vector2(-width, 0)).subtract(pos);

    const dist1 = dir1.length();
    const dist2 = dir2.length();
    const dist3 = dir3.length();

    if (dist1 < dist2 && dist1 < dist3) return dir1;
    if (dist2 < dist1 && dist2 < dist3) return dir2;
    return dir3;
}

export function map2dPosTo3dCylinder(pos, width) {
    const angle = (pos.x / width) * 2 * Math.PI;
    const posX = Math.sin(angle);
    const posZ = Math.cos(angle);
    const posY = (pos.y * 2 * Math.PI - Math.PI) / width;
    return new Vector3(posX, posY, posZ);
}

export function warpCylinder(warp, pos) {
    warp.DomainWarp(pos);  // Assume warp modifies pos in-place
    const posCenter = new Vector3(0, pos.y, 0);
    return posCenter.add(pos.subtract(posCenter).normalize());
}

export function createNoise(seed, fractalType, frequency, lacunarity, gain, octaves = 4, noiseType = FastNoiseLite.NoiseType.OpenSimplex2S)
{
    let noise = new FastNoiseLite(seed + 3);
    noise.SetNoiseType(noiseType);
    noise.SetFractalType(fractalType);
    noise.SetFractalOctaves(octaves);
    noise.SetFrequency(frequency);
    noise.SetFractalLacunarity(lacunarity);
    noise.SetFractalGain(gain);
    return noise;
}

export function createWarp(seed, fractalType, warpAmp, frequency, lacunarity, gain, octaves = 4, noiseType = FastNoiseLite.NoiseType.OpenSimplex2)
{
    let noise = createNoise(seed, fractalType, frequency, lacunarity, gain, octaves, noiseType);
    noise.SetDomainWarpAmp(warpAmp);
    return noise;
}

export function randomPointInCircle(random) {
    const r = Math.sqrt(random.nextFloat());
    const angle = random.nextFloat() * 2 * Math.PI;
    return Vector2.fromPolar(r, angle);
}

export function getHexNeighbors(x, y, data) {
    const isOdd = y % 2;
    const neighbors = [];

    for (let n = 0; n < 6; ++n) {
        const offset = neighborOffsets[n + 6 * isOdd];
        const newX = (x + offset.x + data.width) % data.width;
        const newY = y + offset.y;

        if (newY < 0 || newY >= data.height) continue;

        neighbors.push(new Point(newX, newY));
    }

    return neighbors;
}

export function computeDistanceFields(data, DF, maxIter) {
    const W = data.width;
    const H = data.height;

    for (let y = 0; y < H; y++) {
        for (let x = 0; x < W; x++) {
            const index = x + y * W;
            let dfIsland = 1, dfHome = 1, dfDist = 1;
            const tile = data.tiles[index];
            const isWater = tile.getType() === MapTileType.Water ? 0 : 1;

            const neighbors = getHexNeighbors(x, y, data);
            for (const n of neighbors) {
                const newIndex = n.x + n.y * W;
                const neighborTile = data.tiles[newIndex];
                const type = neighborTile.getType();

                if (type === MapTileType.Continent) {
                    if (neighborTile.isHomeOrDistant()) dfDist = isWater;
                    else dfHome = isWater;
                } else if (type === MapTileType.Island) {
                    dfIsland = isWater;
                }
            }

            DF[index * 3 + 0] = dfIsland;
            DF[index * 3 + 1] = dfHome;
            DF[index * 3 + 2] = dfDist;
        }
    }

    for (let iter = 0; iter < maxIter; iter++) {
        for (let y = 0; y < H; y++) {
            for (let x = 0; x < W; x++) {
                const index = x + y * W;
                let minIsland = 9999, minHome = 9999, minDist = 9999;

                const isOdd = y % 2;
                for (let n = 0; n < 6; ++n) {
                    const offset = neighborOffsets[n + 6 * isOdd];
                    const nx = (x + offset.x + W) % W;
                    const ny = y + offset.y;
                    if (ny < 0 || ny >= H) continue;

                    const ni = nx + ny * W;
                    minIsland = Math.min(minIsland, DF[ni * 3 + 0]);
                    minHome = Math.min(minHome, DF[ni * 3 + 1]);
                    minDist = Math.min(minDist, DF[ni * 3 + 2]);
                }

                if (DF[index * 3 + 0] > 0) DF[index * 3 + 0] = minIsland + 1;
                if (DF[index * 3 + 1] > 0) DF[index * 3 + 1] = minHome + 1;
                if (DF[index * 3 + 2] > 0) DF[index * 3 + 2] = minDist + 1;
            }
        }
    }

    for (let y = 0; y < H; y++) {
        for (let x = 0; x < W; x++) {
            const index = x + y * W;
            const tile = data.tiles[index];

            if (tile.getType() === MapTileType.Continent) {
                if (tile.isHomeOrDistant()) DF[index * 3 + 2] *= -1;
                else DF[index * 3 + 1] *= -1;
            } else if (tile.getType() === MapTileType.Island) {
                DF[index * 3 + 0] *= -1;
            }
        }
    }
}

export function computeConnectedCounts(data, typeMaskCount, typeMaskExpand) {
    const W = data.width;
    const H = data.height;
    let sizes = new Array(W * H);
    const visited = new Array(W * H);

    for (let y = 0; y < H; y++) {
        for (let x = 0; x < W; x++) {
            const index = x + y * W;
            visited[index] = false;
            sizes[index] = 0;
        }
    }

    for (let y = 0; y < H; y++) {
        for (let x = 0; x < W; x++) {
            const index = x + y * W;
            if (!visited[index]) {
                const tile = data.tiles[index];
                const type = tile.getType() !== MapTileType.Water ? 1 : (tile.getBiome() === MapTileBiome.Ocean ? 2 : 4);

                if ((type & typeMaskCount) !== 0) {
                    const count = _dfs(data, sizes, visited, x, y, type, typeMaskCount, typeMaskExpand);
                }
            }
        }
    }

    return sizes;
}

function _dfs(data, sizes, visited, x, y, type, typeMaskCount, typeMaskExpand) {
    const W = data.width;
    const H = data.height;
    const stack = [[x, y]];
    const component = [[x, y]];
    const index = x + y * W;
    visited[index] = true;
    let count = 1;

    if (data.tiles[index].getMorph() === MapTileMorph.Mountainous) {
        sizes[index] = 0;
        return -1;
    }

    while (stack.length > 0) {
        const [curX, curY] = stack.pop();

        const neighbors = getHexNeighbors(curX, curY, data);
        for (const n of neighbors) {
            const newIndex = n.x + n.y * W;
            if (visited[newIndex]) continue;

            const neighborTile = data.tiles[newIndex];

            if (neighborTile.getMorph() === MapTileMorph.Mountainous) {
                visited[newIndex] = true;
                continue;
            }

            const newType = neighborTile.getType() !== MapTileType.Water ? 1 : (neighborTile.getBiome() === MapTileBiome.Ocean ? 2 : 4);

            if ((newType & typeMaskExpand) !== 0) {
                stack.push([n.x, n.y]);
                visited[newIndex] = true;
            }
            if ((newType & typeMaskCount) !== 0) {
                component.push([n.x, n.y]);
                count++;
            }
        }
    }

    while (component.length > 0) {
        const [curX, curY] = component.pop();
        sizes[curX + curY * W] = count;
    }
    return count;
}