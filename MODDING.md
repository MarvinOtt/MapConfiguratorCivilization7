# Making a custom script

A mod can have multiple scripts, where each one needs two versions: One written in C# for this tool and the other one in JavaScript for the game.
When the settings from the tool are applied the parameters are written into a JavaScript settings file.
This can then simply be imported from the game script.  
The C# script does not have to match the JavaScript version in all features. For example the C# version could only compute the main continents and allow changing their size. 

If a Civilization VII mod has the script and setting files as below, the tool recognises all present scripts.  
Required file structure (Example with mod "mapScript" together with the two map scripts "randomWorlds" and "chaoticContinents"):

<pre>
mapScript/
├── mapConfigurator/
│   └── scripts/
│       ├── randomWorlds\
│       │   ├── script.cs
│       │   └── extraConfig1.json
│       └── chaoticContinents\
│           ├── script.cs
│           └── extraConfig2.json
└── modules/
    ├── maps\
    │   ├── randomWorlds.js
    │   └── chaoticContinents.js
    └── settings/
        ├── randomWorlds.js
        └── chaoticContinents.js
</pre>

The scripts for the game under "modules/maps" can also be located differently as long as the scripts can load the corresponding setting files.  
The files under "modules/settings" correspond to the settings for each script. This tool overwrite it automatically, but for the initial version, the strucure is as follows:  

```js
export const SETTINGS = {
  threshold: 0,
  detailAmount: 1,
  detailSize: 0.7,
  islandAmount: 1,
  islandThreshold: 1.2,
  debugView: "Height"
};
```
## Script structure
Example of a basic script:
```cs
using MapConfiguratorCivilization7;
using MapConfiguratorCivilization7.Helper;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using static MapConfiguratorCivilization7.Helper.FastNoiseLite;


public class ScriptSettings
{
    [SliderFloat("Amount of land", 0, 1.0f)]
    public float probability = 0.5f;
}

public class Script
{
    CancellationToken token;
    MapScript script;
    MapScriptData data;
    ScriptSettings settings;
    XoShiRo128plus random;

    public Script(MapScript script, CancellationToken token)
    {
        this.script = script;
        this.token = token;
        data = script.data;
        settings = (ScriptSettings)script.settings.workingInstance;
        random = new XoShiRo128plus(data.seed);
    }

    public void Run()
    {
        for (int y = 0; y < data.height; y++)
        {
            for (int x = 0; x < data.width; x++)
            {
                bool isLand = random.NextFloat() < settings.probability;
                data.tiles[x + y * data.width].SetType(isLand ? MapTileType.Continent : MapTileType.Water);
                data.tiles[x + y * data.width].SetBiome(isLand ? MapTileBiome.Grassland : MapTileBiome.Ocean);
            }
            token.ThrowIfCancellationRequested();
        }

        // Syncs the tile data with the preview
        script.UpdateMapCallback();
    }
}
```
A more extensive script of the mod "Random Worlds" from steam workshop: [C#](Examples/randomWorlds.cs), [JavaScript](Examples/randomWorlds.js).
## Helper classes and functions

### C#
The file [MapScriptData.cs](MapConfiguratorCivilization7/Map/Script/MapScriptData.cs) contains the tile data for a script. Needed as an interface to the tool.  
The file [MapScriptHelper.cs](MapConfiguratorCivilization7/Map/Script/MapScriptHelper.cs) contains some helpful functions for creating map scripts.  
The file [MapScriptAttribute.cs](MapConfiguratorCivilization7/Map/Script/MapScriptAttribute.cs) contains the possible attributes that can be applied to setting paramaters.  
The file [FastNoiseLite.cs](MapConfiguratorCivilization7/Common/FastNoiseLite.cs) contains the noise library.  
The file [XoShiRo128plus.cs](MapConfiguratorCivilization7/Common/XoShiRo128plus.cs) contains the random number generator.

### JavaScript
The file [mapData.js](Examples/mapData.js) contains the tile data as with the C# version. Recommended to use in order to keep the code similar to the C# version.  
The file [helper.js](Examples/helper.js) contains some helpful functions for creating map scripts. It is the same as the C# version.  
The file [FastNoiseLite.js](Examples/FastNoiseLite.js) contains the noise library which is the same as the C# version.  
The file [xoShiRo128plus.js](Examples/xoShiRo128plus.js) contains the random number generator which is the same as the C# version.

## Setup and Debugging

Create the required file strucuture and place the files in a temporary mod folder under "steamapps/workshop/1295660/<anyID>". This path is under the steam library where Civilization VII is installed.
This has to be done as the tool looks for scripts in steam workshop mods. For starting only a C# script is needed.  
In order to easily find errors it is recommended to setup this project locally and add the wanted C# script files to the project as a link to catch compilation errors.
The JavaScript version should match the C# version as close as possible. Enable map script logging for the game and use console.log() for easier debugging.
