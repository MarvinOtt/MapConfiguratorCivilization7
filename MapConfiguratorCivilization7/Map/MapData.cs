using MapConfiguratorCivilization7.Helper;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MapConfiguratorCivilization7
{

    public struct MapSize
    {
        public string name;
        public Point size;
        public Point defaultPlayers;
        public MapSize(string name, Point size, Point defaultPlayers)
        {
            this.name = name;
            this.size = size;
            this.defaultPlayers = defaultPlayers;
        }
    }

    public class MapData
    {
        public static readonly int MAX_SIZE_X = 512;
        public static readonly int MAX_SIZE_Y = 320;
        public static readonly int MAX_TILES = MAX_SIZE_X * MAX_SIZE_Y;
        public int tileCount;

        public MapTile[] tiles;
        public float[] debugData;
        public float[] tilesGpuTemp;
        public Texture2D tilesGpu, debugGpu;

        public Color[] tileTypeColors = new Color[] { Color.DarkBlue, Color.LightBlue, Color.GreenYellow, Color.Green, Color.WhiteSmoke };

        private Map map;

        public MapData(Map map)
        {
            this.map = map;
            tileCount = map.mapSize.X * map.mapSize.Y;
            tiles = new MapTile[MAX_TILES];
            debugData = new float[MAX_TILES];
            tilesGpuTemp = new float[MAX_TILES];
            tilesGpu = new Texture2D(App.graphicsDevice, MAX_SIZE_X, MAX_SIZE_Y, false, SurfaceFormat.Single);
            debugGpu = new Texture2D(App.graphicsDevice, MAX_SIZE_X, MAX_SIZE_Y, false, SurfaceFormat.Single);

            UpdateForGpu();
        }

        public void ApplyChange()
        {
            tileCount = map.mapSize.X * map.mapSize.Y;
            Clear();
        }

        public void Clear()
        {
            for (int i = 0; i < tileCount; ++i)
            {
                tiles[i] = new MapTile();
                if (Settings.data.showDebug)
                    debugData[i] = 0;

            }
            UpdateForGpu();
        }

        public void Set(MapScriptData data)
        {
            for (int i = 0; i < tileCount; ++i)
            {
                tiles[i] = data.tiles[i];
                if (Settings.data.showDebug)
                    debugData[i] = data.debug[i];
            }
            UpdateForGpu();
        }

        public void UpdateForGpu()
        {
            for(int i = 0; i < tileCount; ++i)
            {
                tilesGpuTemp[i] = tiles[i].ToFloat();
            }
            if (Settings.data.showDebug)
                debugGpu.SetData(0, 0, new Rectangle(0, 0, map.mapSize.X, map.mapSize.Y), debugData, 0, tileCount);
            tilesGpu.SetData(0, 0, new Rectangle(0, 0, map.mapSize.X, map.mapSize.Y), tilesGpuTemp, 0, tileCount);
        }
    }
}
