using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapConfiguratorCivilization7
{
    public class MapScriptHandler
    {
        public List<MapScript> scripts;
        public int selectedScriptIndex = 0;

        public MapScriptHandler()
        {
            scripts = new List<MapScript>();
        }

        public void ApplyChange(bool skipDelay = false)
        {
            scripts[selectedScriptIndex].ApplyChanges(skipDelay);
        }

        public bool Initialize()
        {
            try
            {
                string workshopModsPath = Path.Combine(Settings.data.pathToSteamLibraryWithCiv7, @"steamapps\workshop\content\1295660"); // Civ 7 AppID = 1295660

                if (!Directory.Exists(workshopModsPath))
                {
                    Console.WriteLine($"Mod folder not found at: {workshopModsPath}");
                    scripts.Clear();
                    App.map.mapData.Clear();
                    return false;
                }

                Console.WriteLine("Searching for C# files in mapConfigurator/scripts...");

                foreach (var modFolder in Directory.GetDirectories(workshopModsPath))
                {
                    string scriptPath = Path.Combine(modFolder, "mapConfigurator", "scripts");

                    if (Directory.Exists(scriptPath))
                    {
                        foreach (var scriptFolder in Directory.GetDirectories(scriptPath))
                        {
                            string name = Path.GetFileName(scriptFolder);
                            string scriptFileName = Path.Combine(scriptFolder, "script.cs");
                            if (File.Exists(scriptFileName))
                            {
                                scripts.Add(new MapScript(name, scriptFolder));
                            }
                        }


                        var csFiles = Directory.GetFiles(scriptPath, "*.cs", SearchOption.AllDirectories);

                        if (csFiles.Length > 0)
                        {
                            Console.WriteLine($"\nFound C# files in mod: {modFolder}");
                            foreach (var file in csFiles)
                            {
                                Console.WriteLine($" - {file}");
                            }
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unexpected error: " + ex.Message);
                return false;
            }
        }
    }
}
