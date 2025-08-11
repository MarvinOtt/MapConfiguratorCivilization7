using ImGuiNET;
using Microsoft.Xna.Framework;

namespace MapConfiguratorCivilization7.Gui
{
    public static class MapPreviewUi
    {
        public static bool isOpen = true;

        public static void Draw()
        {
            if (!isOpen)
                return;


            ImGui.SetNextWindowPos(new Vector2(App.ScreenWidth, 0), ImGuiCond.Always, new Vector2(1, 0));
            ImGui.SetNextWindowSize(Vector2.Zero);
            ImGui.SetNextWindowSizeConstraints(new Vector2(150, 0), new Vector2(400, 400));
            if (ImGui.Begin("MapPreviewSettings", ref isOpen, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoTitleBar))
            {
                ImGui.Text("Seed");
                ImGui.SameLine();
                if (ImGui.Button("Randomize"))
                {
                    App.map.seed = App.map.random.Next();
                    App.map.ApplyChange(true);
                }
                if (ImGui.InputInt("##seed", ref App.map.seed, 0))
                {
                    App.map.ApplyChange(true);
                }

                ImGui.Spacing();

                ImGui.Text("Map Size");
                float contentWidth = ImGui.GetWindowContentRegionWidth();
                ImGui.SetNextItemWidth(contentWidth - ImGuiStyleConfig.style.FramePadding.Y * 2);
                if (ImGui.BeginCombo("##MapSize", Map.mapSizes[App.map.selectedMapSizeIndex].name))
                {
                    for (int i = 0; i < Map.mapSizes.Count; i++)
                    {
                        bool isSelected = (i == App.map.selectedMapSizeIndex);
                        if (ImGui.Selectable(Map.mapSizes[i].name, isSelected))
                        {
                            App.map.selectedMapSizeIndex = i;
                            App.map.selectedPlayerCount = Map.mapSizes[App.map.selectedMapSizeIndex].defaultPlayers;
                            App.map.ApplyChange(true);
                        }

                        if (isSelected)
                            ImGui.SetItemDefaultFocus();
                    }

                    ImGui.EndCombo();
                }

                ImGui.Text("Players (Home - Dist)");
                ImGui.SetNextItemWidth(contentWidth / 2 - ImGuiStyleConfig.style.FramePadding.Y * 2);
                if (ImGui.BeginCombo("##PlayersHome", App.map.selectedPlayerCount.X.ToString()))
                {
                    for (int i = 0; i < 10; i++)
                    {
                        bool isSelected = (i == App.map.selectedPlayerCount.X);
                        string defaultAppendCur = i == Map.mapSizes[App.map.selectedMapSizeIndex].defaultPlayers.X ? "(Default)" : "";
                        if (ImGui.Selectable(i.ToString() + defaultAppendCur, isSelected))
                        {
                            App.map.selectedPlayerCount.X = i;
                            App.map.ApplyChange(true);
                        }

                        if (isSelected)
                            ImGui.SetItemDefaultFocus();
                    }

                    ImGui.EndCombo();
                }
                ImGui.SameLine();
                ImGui.SetNextItemWidth(contentWidth / 2 - ImGuiStyleConfig.style.FramePadding.Y * 2);
                if (ImGui.BeginCombo("##PlayersDist", App.map.selectedPlayerCount.Y.ToString()))
                {
                    for (int i = 0; i < 10; i++)
                    {
                        bool isSelected = (i == App.map.selectedPlayerCount.Y);
                        string defaultAppendCur = i == Map.mapSizes[App.map.selectedMapSizeIndex].defaultPlayers.Y ? "(Default)" : "";
                        if (ImGui.Selectable(i.ToString() + defaultAppendCur, isSelected))
                        {
                            App.map.selectedPlayerCount.Y = i;
                            App.map.ApplyChange(true);
                        }

                        if (isSelected)
                            ImGui.SetItemDefaultFocus();
                    }

                    ImGui.EndCombo();
                }

                ImGui.Spacing();
                if (ImGui.Button("Center map"))
                {
                    App.map.mapRender.Center(true);
                }
                if (Settings.data.showDebug)
                    ImGui.Checkbox("Render debug", ref App.map.mapRender.renderDebug);
                ImGui.Checkbox("Wrap map", ref App.map.mapRender.wrapMap);
                ImGui.Checkbox("Show map border", ref App.map.mapRender.showMapBorder);
                ImGui.Checkbox("Show tile border", ref App.map.mapRender.showHexBorder);

                ImGui.End();
            }
        }
    }
}
