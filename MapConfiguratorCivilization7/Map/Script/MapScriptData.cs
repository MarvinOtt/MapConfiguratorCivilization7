using System;

namespace MapConfiguratorCivilization7.Helper
{
    public enum MapTileType
    {
        Water,
        Continent,
        Island
    }

    public enum MapTileBiome
    {
        Ocean,
        Coastal,
        Lake,
        Plains,
        Tropical,
        Desert,
        Grassland,
        Tundra
    }

    public enum MapTileMorph
    {
        Flat,
        Rough,  // Does nothing yet
        Mountainous,
        NavRiver  // Does nothing yet
    }

    public enum MapTileMainFeature
    {
        None,
        Volcano,
        MinorRiver, // Does nothing yet
        Ice,
    }

    public struct MapTile
    {
        public uint data;

        public MapTile()
        {
            this.data = 0;
        }

        public void SetType(MapTileType type)
        {
            data = (data & 0xFFFFFFFC) | ((uint)type);
        }

        public new MapTileType GetType()
        {
            return (MapTileType)(data & 0x3);
        }

        public void SetBiome(MapTileBiome biome)
        {
            data = (data & 0xFFFFFFE3) | ((uint)biome << 2);
        }

        public MapTileBiome GetBiome()
        {
            return (MapTileBiome)((data >> 2) & 0x7);
        }

        public void SetFeature(MapTileMainFeature feature)
        {
            data = (data & 0xFFFFFF9F) | ((uint)feature << 5);
        }

        public MapTileMainFeature GetFeature()
        {
            return (MapTileMainFeature)((data >> 5) & 0x3);
        }

        public void SetMorph(MapTileMorph morph)
        {
            data = (data & 0xFFFFFE7F) | ((uint)morph << 7);
        }

        public MapTileMorph GetMorph()
        {
            return (MapTileMorph)((data >> 7) & 0x3);
        }

        // Does nothing yet
        public void SetFloodPlain(bool isFloodPlain)
        {
            data = (data & 0xFFFFFDFF) | (Convert.ToUInt32(isFloodPlain) << 9);
        }

        // Does nothing yet
        public void SetWet(bool isWet)
        {
            data = (data & 0xFFFFFBFF) | (Convert.ToUInt32(isWet) << 10);
        }

        public void SetVegetated(bool isVegetated)
        {
            data = (data & 0xFFFFF7FF) | (Convert.ToUInt32(isVegetated) << 11);
        }

        public bool IsVegetated()
        {
            return ((data >> 11) & 0x1) != 0;
        }

        public void SetSnow(bool isSnow)
        {
            data = (data & 0xFFFFEFFF) | (Convert.ToUInt32(isSnow) << 12);
        }

        public bool IsSnow()
        {
            return ((data >> 12) & 0x1) != 0;
        }

        // Does nothing yet
        public void SetReef(bool isReef)
        {
            data = (data & 0xFFFFDFFF) | (Convert.ToUInt32(isReef) << 13);
        }

        public void SetHomeOrDistant(bool isDistant)
        {
            data = (data & 0xFFFFBFFF) | (Convert.ToUInt32(isDistant) << 14);
        }

        public bool IsHomeOrDistant()
        {
            return ((data >> 14) & 0x1) != 0;
        }

        public float ToFloat()
        {
            return (float)(data & 0x1FFF);
        }
    }

    public class MapScriptData
    {
        public MapTile[] tiles;
        public float[] debug;
        public int width, height;
        public int seed;

        public int playerHome, playerDistant;

        public MapScriptData(int width, int height, int seed)
        {
            this.width = width;
            this.height = height;
            this.seed = seed;
            tiles = new MapTile[MapData.MAX_TILES];
            debug = new float[MapData.MAX_TILES];
        }

        public void SetSize(int width, int height)
        {
            this.width = width;
            this.height = height;
        }
    }
}
