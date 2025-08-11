using ImGuiNET;
using MapConfiguratorCivilization7.Helper;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapConfiguratorCivilization7.Gui
{
    public static class SettingsUi
    {
        public static void Draw()
        {
            ImGui.Text("Steam library path");
            ImGui.SameLine();
            if (ImGui.Button("Auto"))
            {
                if (Settings.FindSteamLibraryLocation())
                    App.map.scriptHandler.Initialize();
            }
            ImGuiCommon.HelperIcon("Path to the steam library where Civilization VII is installed.\nPressing \"Auto\" tries to find it automatically", 500);
            ImGui.SetNextItemWidth(ImGui.GetWindowContentRegionWidth());
            if (ImGui.InputText("##Civ7LibraryLocation", ref Settings.data.pathToSteamLibraryWithCiv7, 1000))
            {
                App.map.scriptHandler.Initialize();
            }
            if (ImGui.Checkbox("Show debug settings", ref Settings.data.showDebug))
            {
                if (!Settings.data.showDebug)
                    App.map.mapRender.renderDebug = false;
            }
            ImGui.Spacing();
            ImGui.Text("Super sampling");
            ImGuiCommon.HelperIcon("Determines the quality of the map render.\nEspecially noticeable when zooming out", 500);
            if (ImGui.BeginCombo("##SuperSampling", Settings.data.superSamplingCount.ToString()))
            {
                for (int i = 0; i < 4; i++)
                {
                    int superSampling = (int)Math.Pow(2, i); 
                    bool isSelected = superSampling == Settings.data.superSamplingCount;
                    if (ImGui.Selectable(superSampling.ToString(), isSelected))
                        Settings.data.superSamplingCount = superSampling;

                    if (isSelected)
                        ImGui.SetItemDefaultFocus();
                }

                ImGui.EndCombo();
            }
            ImGui.Spacing();
            if (ImGui.CollapsingHeader("Biome Colors", ImGuiTreeNodeFlags.DefaultOpen))
            {
                var biomeNames = Enum.GetNames(typeof(MapTileBiome));
                for (int i = 0; i < Settings.data.biomeColors.Length && i < biomeNames.Length; i++)
                {
                    Vector3 color = Settings.data.biomeColors[i];
                    Vector3 original = color; // For change detection
                    if (ImGui.ColorEdit3(biomeNames[i], ref color))
                    {
                        Settings.data.biomeColors[i] = color;
                        // Optionally: Trigger ApplyChanges() or mark dirty
                    }
                }
            }

            if (ImGui.Button("Save"))
                Settings.Save();

            ImGui.End();
        }
    }
}
