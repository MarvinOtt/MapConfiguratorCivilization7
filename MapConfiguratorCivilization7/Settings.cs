using MapConfiguratorCivilization7.Helper;
using Microsoft.Win32;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MapConfiguratorCivilization7
{
    public class SettingData
    {
        public string pathToSteamLibraryWithCiv7 = "";
        public bool showDebug = false;
        public Vector3[] biomeColors = new Vector3[]
        {
            Color.DarkBlue.ToVector3(),
            new Color(21, 73, 168).ToVector3(),
            new Color(30, 30, 186).ToVector3(),
            new Color(190, 189, 102).ToVector3(),
            new Color(100, 149, 80).ToVector3(),
            new Color(255, 213, 144).ToVector3(),
            new Color(126, 135, 94).ToVector3(),
            new Color(190, 149, 105).ToVector3(),
        };
        public int superSamplingCount = 4;

        public string getJson()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions {IncludeFields = true});
        }

        public static SettingData fromJson(string json)
        {
            return JsonSerializer.Deserialize<SettingData>(json, new JsonSerializerOptions { IncludeFields = true });
        }
    }

    public static class Settings
    {
        const string AppId = "1295660";

        public static SettingData data = new SettingData();

        public static void Load()
        {
            if (File.Exists("settings.json"))
                data = SettingData.fromJson(File.ReadAllText("settings.json"));

            if (string.IsNullOrEmpty(data.pathToSteamLibraryWithCiv7))
                FindSteamLibraryLocation();
        }

        public static void Save()
        {
            File.WriteAllText("settings.json", data.getJson());
        }

        public static bool FindSteamLibraryLocation()
        {
            string steamInstallPath = GetSteamInstallPath();
            if (steamInstallPath == null)
                return false;

            string libraryVdfPath = Path.Combine(steamInstallPath, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(libraryVdfPath))
                return false;

            string libraryVdf = File.ReadAllText(libraryVdfPath);

            var pathRegex = new Regex(@"""(\d+)""\s*{[^{}]*(""path""\s*""([^""]+)"")[^{}]*(""apps""\s*{([^}]*)})", RegexOptions.Singleline);
            var appsRegex = new Regex(@"""(\d+)""\s*""[^""]+""");

            string libraryPath = null;
            foreach (Match match in pathRegex.Matches(libraryVdf))
            {
                string path = match.Groups[3].Value.Replace(@"\\", @"\");
                string appsBlock = match.Groups[5].Value;

                foreach (Match appMatch in appsRegex.Matches(appsBlock))
                {
                    if (appMatch.Groups[1].Value == AppId)
                    {
                        libraryPath = path;
                    }
                }
            }

            if (libraryPath == null)
                return false;

            data.pathToSteamLibraryWithCiv7 = libraryPath;

            return true;
        }

        private static string GetSteamInstallPath()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Windows: registry
                try
                {
                    using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"Software\Wow6432Node\Valve\Steam"))
                    {
                        return key?.GetValue("InstallPath") as string;
                    }
                }
                catch
                {
                    return null;
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Linux: usually ~/.steam/steam or ~/.local/share/Steam
                string home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                string[] candidates = [
                    Path.Combine(home, ".steam/steam"),
                    Path.Combine(home, ".local/share/Steam")
                ];

                foreach (var c in candidates)
                {
                    if (Directory.Exists(c))
                        return c;
                }

            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // macOS: ~/Library/Application Support/Steam
                string home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                string path = Path.Combine(home, "Library/Application Support/Steam");
                if (Directory.Exists(path))
                    return path;
            }

            return null;
        }

    }
}
