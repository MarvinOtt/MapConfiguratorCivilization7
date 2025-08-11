using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapConfiguratorCivilization7.Gui
{
    public static class ImGuiCommon
    {
        public static void HelperIcon(string text, float maxWidth, bool SameLine = true)
        {
            if (SameLine)
                ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            Tooltip(text, maxWidth, 100);
        }

        public static void Tooltip(string text, float maxWidth, double minTime = -1)
        {
            if (ImGui.IsItemHovered() && IO.mouseNoMoveTime > minTime)
            {
                ImGui.BeginTooltip();
                ImGui.PushTextWrapPos(maxWidth);
                ImGui.TextUnformatted(text);
                ImGui.PopTextWrapPos();
                ImGui.EndTooltip();
            }
        }

    }
}
