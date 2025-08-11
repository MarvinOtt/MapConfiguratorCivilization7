using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace MapConfiguratorCivilization7.Helper
{
    public static class MapScriptHelper
    {
        public static readonly float HEX_X_MUL = (2.0f / 3.0f) * (float)Math.Sqrt(3.0f);
        public static readonly uint BIT_LAND = 1;
        public static readonly uint BIT_OCEAN = 2;
        public static readonly uint BIT_COAST = 4;

        public static Point[] neighborOffsets = { new(-1, -1), new(0, -1), new(-1, 0), new(1, 0), new(-1, 1), new(0, 1), new(0, -1), new(1, -1), new(-1, 0), new(1, 0), new(0, 1), new(1, 1) };


        public static int Wrap(int x, int m)
        {
            return (x % m + m) % m;
        }

        public static float Wrap(float x, float m)
        {
            return (x % m + m) % m;
        }

        public static float Attenuation(float scale, float x)
        {
            return scale / (scale + x);
        }

        public static float InvAttenuation(float scale, float x)
        { 
            return 1.0f - scale / (scale + x);
        }

        // Returns the position of a hex tile with respect to the hex geometry
        public static Vector2 PosOfHex(Point hexPosIndex)
        {
            return new Vector2((hexPosIndex.X + (hexPosIndex.Y % 2) * 0.5f) * HEX_X_MUL, hexPosIndex.Y);
        }

        // Returns the distance between two points where the world is wrapped at the x-axis with the specified width
        public static float DistanceBetweenPointsWrapped(Vector2 pos1, Vector2 pos2, float width)
        {
            float yDif = Math.Abs(pos1.Y - pos2.Y);
            float xDif = Math.Abs(pos1.X - pos2.X);
            xDif = Math.Min(xDif, width - xDif);
            return (float)Math.Sqrt(xDif * xDif + yDif * yDif);
        }

        // Returns the direction from posStart to posEnd where the world is wrapped at the x-axis with the specified width
        public static Vector2 GetDirToPosWrapped(Vector2 posStart, Vector2 posEnd, float width)
        {
            Vector2 dir1 = posEnd - posStart;
            Vector2 dir2 = (posEnd + new Vector2(width, 0)) - posStart;
            Vector2 dir3 = (posEnd - new Vector2(width, 0)) - posStart;

            float dist1 = dir1.Length();
            float dist2 = dir2.Length();
            float dist3 = dir3.Length();
            if (dist1 < dist2 && dist1 < dist3)
                return dir1;
            else if (dist2 < dist1 && dist2 < dist3)
                return dir2;
            else
                return dir3;
        }

        // Maps a 2D position into a 3D cylinder to connect the start and end of the x-axis together.
        // Useful in order to create seamless transitions for example with noise functions.
        public static Vector3 Map2dPosTo3dCylinder(Vector2 pos, float width)
        {
            float pos0to2pi = (pos.X / width) * 2 * (float)Math.PI;
            float posX = (float)Math.Sin(pos0to2pi);
            float posZ = (float)Math.Cos(pos0to2pi);
            return new Vector3(posX, (pos.Y * 2 * (float)Math.PI - (float)Math.PI) / width, posZ);
        }

        // Warps a position at the cylinder with the specified noise function and minimizes unwanted distortions by enforcing the result to also be on the cylinder.
        public static Vector3 WarpCylinder(FastNoiseLite warp, Vector3 pos)
        {
            warp.DomainWarp(ref pos.X, ref pos.Y, ref pos.Z);
            Vector3 posCenter = new Vector3(0, pos.Y, 0);
            return posCenter + Vector3.Normalize(pos - posCenter);
        }

        // Shortcut to create noise function
        public static FastNoiseLite CreateNoise(int seed, FastNoiseLite.FractalType fractalType, float frequency, float lacunarity, float gain, int octaves = 4, FastNoiseLite.NoiseType noiseType = FastNoiseLite.NoiseType.OpenSimplex2S)
        {
            FastNoiseLite noise = new FastNoiseLite(seed + 3);
            noise.SetNoiseType(noiseType);
            noise.SetFractalType(fractalType);
            noise.SetFractalOctaves(octaves);
            noise.SetFrequency(frequency);
            noise.SetFractalLacunarity(lacunarity);
            noise.SetFractalGain(gain);
            return noise;
        }

        // Shortcut to create warp noise function
        public static FastNoiseLite CreateWarp(int seed, FastNoiseLite.FractalType fractalType, float warpAmp, float frequency, float lacunarity, float gain, int octaves = 4, FastNoiseLite.NoiseType noiseType = FastNoiseLite.NoiseType.OpenSimplex2)
        {
            FastNoiseLite noise = CreateNoise(seed, fractalType, frequency, lacunarity, gain, octaves, noiseType);
            noise.SetDomainWarpAmp(warpAmp);
            return noise;
        }

        // Returns a random position in a uniform circle
        public static Vector2 RandomPointInCircle(XoShiRo128plus random)
        {
            float r = (float)Math.Sqrt(random.NextFloat());
            float angle = random.NextFloat() * 2 * (float)Math.PI;
            return new Vector2(r * (float)Math.Cos(angle), r * (float)Math.Sin(angle));
        }

        // Returns the valid neighboring hex tile index positions including wrapping at the x-axis
        public static IEnumerable<Point> GetHexNeighbors(int x, int y, MapScriptData data)
        {
            int isOdd = y % 2;
            for (int n = 0; n < 6; ++n)
            {
                Point offset = neighborOffsets[n + 6 * isOdd];
                int newX = (x + offset.X + data.width) % data.width;
                int newY = y + offset.Y;

                if (newY < 0 || newY >= data.height)
                    continue;

                yield return new Point(newX, newY);
            }
        }

        // Computes distance fields to land where negative values are inland.
        // Useful to for example place islands at a safe distance from main continents or ensuring that coastal spread does not connect home with distant lands.
        // The result is stored in DF, where the second dimension is for the following:
        // Index 0: Distance fields to islands
        // Index 1: Distance fields to main home lands
        // Index 2: Distance fields to main distant lands
        public static void ComputeDistanceFields(MapScriptData data, int[,] DF, int maxIter)
        {
            for (int y = 0; y < data.height; y++)
            {
                for (int x = 0; x < data.width; x++)
                {
                    int index = x + y * data.width;
                    int dfIsland = 1, dfHome = 1, dfDist = 1;
                    int isWater = data.tiles[index].GetType() == MapTileType.Water ? 0 : 1;

                    foreach (var neighbor in GetHexNeighbors(x, y, data))
                    {
                        int newIndex = neighbor.X + neighbor.Y * data.width;
                        var nextTile = data.tiles[newIndex];
                        if (nextTile.GetType() == MapTileType.Continent)
                        {
                            if (nextTile.IsHomeOrDistant())
                                dfDist = isWater;
                            else
                                dfHome = isWater;
                        }
                        else if (nextTile.GetType() == MapTileType.Island)
                            dfIsland = isWater;
                    }
                    DF[index, 0] = dfIsland;
                    DF[index, 1] = dfHome;
                    DF[index, 2] = dfDist;
                }
            }
            for (int i = 0; i < maxIter; ++i)
            {
                for (int y = 0; y < data.height; y++)
                {
                    for (int x = 0; x < data.width; x++)
                    {
                        int index = x + y * data.width;
                        int minDfIsland = 9999, minDfHome = 9999, minDfDist = 9999;
                        for (int n = 0; n < 6; ++n)
                        {
                            int isOdd = y % 2;
                            Point offset = neighborOffsets[n + 6 * isOdd];
                            int newX = (x + offset.X + data.width) % data.width;
                            int newY = y + offset.Y;
                            if (newY < 0 || newY >= data.height)
                                continue;


                            int newIndex = newX + newY * data.width;
                            minDfIsland = Math.Min(minDfIsland, DF[newIndex, 0]);
                            minDfHome = Math.Min(minDfHome, DF[newIndex, 1]);
                            minDfDist = Math.Min(minDfDist, DF[newIndex, 2]);
                        }
                        if (DF[index, 0] > 0)
                            DF[index, 0] = minDfIsland + 1;
                        if (DF[index, 1] > 0)
                            DF[index, 1] = minDfHome + 1;
                        if (DF[index, 2] > 0)
                            DF[index, 2] = minDfDist + 1;
                    }
                }
            }
            for (int y = 0; y < data.height; y++)
            {
                for (int x = 0; x < data.width; x++)
                {
                    int index = x + y * data.width;
                    var tile = data.tiles[index];
                    if (tile.GetType() == MapTileType.Continent)
                    {
                        if (tile.IsHomeOrDistant())
                            DF[index, 2] *= -1;
                        else
                            DF[index, 1] *= -1;
                    }
                    else if (tile.GetType() == MapTileType.Island)
                        DF[index, 0] *= -1;
                }
            }
        }


        // Computes the amount of reachable tiles for every tile.
        // Useful to for example check how much land can be reached from every location or how large a water region is.
        // "typeMaskCount" is a bitmask to control which tiles should be counted.
        // "typeMaskExpand" is a bitmask to control which tiles can be used to expand regions. 
        // Use BIT_LAND, BIT_OCEAN and BIT_COAST to create a bitmask
        public static int[,] ComputeConnectedCounts(MapScriptData data, uint typeMaskCount, uint typeMaskExpand)
        {
            int[,] sizes = new int[data.width, data.height];
            bool[,] visited = new bool[data.width, data.height];

            for (int y = 0; y < data.height; y++)
            {
                for (int x = 0; x < data.width; x++)
                {
                    if (!visited[x, y])
                    {
                        int index = x + y * data.width;
                        uint type = data.tiles[index].GetType() != MapTileType.Water ? 1u : (data.tiles[index].GetBiome() == MapTileBiome.Ocean ? 2u : 4u);
                        if ((type & typeMaskCount) != 0)
                        {
                            DFS(data, sizes, visited, x, y, type, typeMaskCount, typeMaskExpand);
                        }

                    }
                }
            }

            return sizes;
        }

        private static void DFS(MapScriptData data, int[,] sizes, bool[,] visited, int x, int y, uint type, uint typeMaskCount, uint typeMaskExpand)
        {
            var stack = new Stack<(int, int)>();
            stack.Push((x, y));
            visited[x, y] = true;
            var component = new List<(int, int)>();
            component.Add((x, y));

            if (data.tiles[x + y * data.width].GetMorph() == MapTileMorph.Mountainous)
            {
                sizes[x, y] = 0;
                return;
            }

            int count = 1;
            while (stack.Count > 0)
            {
                var (curX, curY) = stack.Pop();

                foreach (var neighbor in GetHexNeighbors(curX, curY, data))
                {
                    if (visited[neighbor.X, neighbor.Y])
                        continue;

                    if (data.tiles[neighbor.X + neighbor.Y * data.width].GetMorph() == MapTileMorph.Mountainous)
                    {
                        visited[neighbor.X, neighbor.Y] = true;
                        continue;
                    }

                    int newIndex = neighbor.X + neighbor.Y * data.width;
                    uint newType = data.tiles[newIndex].GetType() != MapTileType.Water ? 1u : (data.tiles[newIndex].GetBiome() == MapTileBiome.Ocean ? 2u : 4u);

                    if ((newType & typeMaskExpand) != 0)
                    {
                        stack.Push((neighbor.X, neighbor.Y));
                        visited[neighbor.X, neighbor.Y] = true;
                    }
                    if ((newType & typeMaskCount) != 0)
                    {
                        component.Add((neighbor.X, neighbor.Y)); 
                        count++;
                    }
                }
            }

            foreach (var (curX, curY) in component)
            {
                sizes[curX, curY] = count;
            }
        }
    }
}
