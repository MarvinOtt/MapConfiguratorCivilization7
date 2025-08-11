using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace MapConfiguratorCivilization7
{
    public class Map
    {
        public MapData mapData;
        public MapRender mapRender;
        public MapScriptHandler scriptHandler;
        public Random random;

        public static List<MapSize> mapSizes = [new("Tiny", new(60, 38), new(3, 1)), new("Small", new(74, 46), new(4, 2)), new("Standard", new(84, 54), new(5, 3)), new("Large", new(96, 60), new(6, 4)), new("Huge", new(106, 66), new(6, 4)), new("Massive (YnAMP)", new(128, 80), new(7, 5))];
        public int selectedMapSizeIndex = 4;
        public Point selectedPlayerCount;
        public Point mapSize;
        public int seed;

        public Map()
        {
            random = new Random();
            mapSize = mapSizes[selectedMapSizeIndex].size;
            selectedPlayerCount = mapSizes[selectedMapSizeIndex].defaultPlayers;
            mapData = new MapData(this);
            mapRender = new MapRender(this);
            scriptHandler = new MapScriptHandler();
            seed = random.Next();
        }

        public void ApplyChange(bool skipDelay = false)
        {
            mapSize = mapSizes[selectedMapSizeIndex].size;
            mapData.ApplyChange();
            scriptHandler.ApplyChange(skipDelay);
        }

        public void Update(float gameTime)
        {
            mapRender.Update();
        }

        public void Render()
        {
            mapRender.Render();
        }
    }
}
