using ImGuiNET;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapConfiguratorCivilization7.Gui
{
    public static class MapSettingsUi
    {
        public static void Draw()
        {

            MapScriptHandler handler = App.map.scriptHandler;

            var scriptNames = handler.scripts.Select(s => s.name).ToArray();

            ImGui.Text("Script");
            if (handler.scripts.Count == 0)
            {
                ImGui.TextWrapped("No scripts have been found!\nVerify that the steam location is set properly and you are subscribed to a Civilization VII configurable map script");
                return;
            }
            if (ImGui.BeginCombo("##Script", scriptNames[App.map.scriptHandler.selectedScriptIndex]))
            {
                for (int i = 0; i < scriptNames.Length; i++)
                {
                    bool isSelected = (i == App.map.scriptHandler.selectedScriptIndex);
                    if (ImGui.Selectable(scriptNames[i], isSelected))
                    {
                        App.map.scriptHandler.selectedScriptIndex = i;
                        App.map.ApplyChange();
                    }

                    if (isSelected)
                        ImGui.SetItemDefaultFocus();
                }

                ImGui.EndCombo();
            }

            // Render selected script settings
            if (App.map.scriptHandler.selectedScriptIndex >= 0 && App.map.scriptHandler.selectedScriptIndex < handler.scripts.Count)
            {
                var script = handler.scripts[App.map.scriptHandler.selectedScriptIndex];

                if (!script.InitializationTask.IsCompleted)
                {
                    ImGui.TextWrapped("Script is currently loading...");
                }
                else if (!script.isValid)
                {
                    ImGui.TextWrapped("Error during script initialization:\n" + script.InitializationTask.Result);
                    if (ImGui.Button("Load again"))
                        script.StartInitialize();
                }
                else
                {
                    script.RenderSettings();
                }
            }

        }
    }
}
