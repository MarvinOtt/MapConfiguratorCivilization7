using ImGuiNET;
using MapConfiguratorCivilization7.Common;
using MapConfiguratorCivilization7.Gui;
using MapConfiguratorCivilization7.Helper;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MapConfiguratorCivilization7
{
    public class MapScriptSettings
    {
        public object defaultInstance, workingInstance;
        public HashSet<string> configs;
        public string selectedConfig = "default";
        public string newConfigName = "newConfig";

        private MapScript script;
        private Texture2DImGui iconsUiImGui;

        public MapScriptSettings(MapScript script)
        {
            this.script = script;
            configs = new HashSet<string>();
            configs.Add("default");
            iconsUiImGui = new Texture2DImGui(App.contentManager.Load<Texture2D>("iconUI"));

        }

        public void SetDefaultInstance(object defaultInstance)
        {
            this.defaultInstance = defaultInstance;
            workingInstance = JsonSerializer.Deserialize(JsonSerializer.Serialize(defaultInstance),defaultInstance.GetType());
        }

        public void SaveAsConfig(string name)
        {
            var json = JsonSerializer.Serialize(workingInstance, new JsonSerializerOptions
            {
                WriteIndented = true,
                IncludeFields = true
            });
            File.WriteAllText(Path.Combine(script.scriptPath, name + ".json"), json);
            if (name != "ConfigForGame")
            {
                configs.Add(name);
                selectedConfig = name;
            }
        }

        public bool LoadConfig(string name)
        {
            try
            {
                string json = File.ReadAllText(Path.Combine(script.scriptPath, name + ".json"));
                var a = workingInstance.GetType();
                var deserialized = JsonSerializer.Deserialize(json, workingInstance.GetType(), new JsonSerializerOptions{ IncludeFields = true });
                if (deserialized == null)
                    return false;

                workingInstance = deserialized;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void ApplyToGame()
        {
            var json = JsonSerializer.Serialize(workingInstance, new JsonSerializerOptions
            {
                WriteIndented = true,
                IncludeFields = true
            });

            var jsLike = Regex.Replace(json, "\"(\\w+)\":", "$1:");
            var jsCode = $"export const SETTINGS = {jsLike};";
            var pathSettingsRoot = Path.GetFullPath(Path.Combine(script.scriptPath, @"..\..\..\"));
            if (!Directory.Exists(pathSettingsRoot))
                return;
            var pathSettingsSettings = Path.Combine(pathSettingsRoot, "settings");
            if (!Directory.Exists(pathSettingsSettings))
                Directory.CreateDirectory(pathSettingsSettings);
            File.WriteAllText(Path.Combine(pathSettingsSettings, script.name + ".js"), jsCode);
        }

        private void CopyToWorkingInstance(object obj)
        {
            // Copy only matching value fields
            foreach (var field in workingInstance.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var newValue = field.GetValue(obj);

                if (newValue != null)
                {
                    field.SetValue(workingInstance, newValue);
                }
            }
        }

        public bool RenderImGui()
        {
            bool anyChanged = false;
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            ImGui.Text("Config");
            if (ImGui.BeginCombo("##Config", selectedConfig != null ? selectedConfig : "Select..."))
            {
                foreach (var config in configs)
                {
                    bool isSelected = (config == selectedConfig);
                    if (ImGui.Selectable(config, isSelected))
                    {
                        selectedConfig = config;
                        if (config == "default")
                        {
                            CopyToWorkingInstance(defaultInstance);
                            newConfigName = "newConfig";
                            anyChanged = true;
                        }
                        else
                        {
                            bool isLoaded = LoadConfig(config);
                            if (!isLoaded)
                                selectedConfig = null;
                            else
                            {
                                newConfigName = config;
                                anyChanged = true;
                            }
                        }

                    }
                }
                ImGui.EndCombo();
            }

            if (ImGui.Button("Delete Config"))
            {
                if (selectedConfig != "default")
                {
                    try
                    {
                        File.Delete(Path.Combine(script.scriptPath, selectedConfig + ".json"));
                        configs.Remove(selectedConfig);
                        selectedConfig = "default";
                        newConfigName = "newConfig";
                        anyChanged = true;
                    }
                    catch { }
                }
            }

            if (ImGui.Button("Save"))
            {
                if (!string.IsNullOrEmpty(newConfigName) && newConfigName != "default" && newConfigName != "ConfigForGame")
                    SaveAsConfig(newConfigName);
            }
            ImGui.SameLine();
            ImGui.InputText("Name", ref newConfigName, 100);
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            ImGui.Text("Settings:");
            var type = workingInstance.GetType();

            bool headerOpen = false, isHeader = false, setHeaderClosed = false;
            float itemHeightImGui = ImGui.GetFrameHeight() - ImGuiStyleConfig.style.FramePadding.Y * 2;
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var field in fields)
            {
                object value = field.GetValue(workingInstance);
                object defaultValue = field.GetValue(defaultInstance);

                if (setHeaderClosed)
                {
                    setHeaderClosed = false;
                    isHeader = false;
                }

                // Handle [ShowIf("fieldName")]
                var showIf = field.GetCustomAttribute<ShowIfAttribute>();
                if (showIf != null)
                {
                    var conditionField = type.GetField(showIf.FieldName);
                    if (conditionField == null || !(conditionField.GetValue(workingInstance) is bool show) || !show)
                        continue;
                }

                // Handle [HeaderBegin]
                if (field.GetCustomAttribute<HeaderBeginAttribute>() is HeaderBeginAttribute headerAttr)
                {
                    isHeader = true;
                    ImGui.Spacing();
                    headerOpen = ImGui.CollapsingHeader(headerAttr.HeaderName, headerAttr.IsDefaultOpen ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None);
                }

                // Handle [HeaderEnd]
                if (field.GetCustomAttribute<HeaderEndAttribute>() != null)
                    setHeaderClosed = true;

                if (isHeader && !headerOpen)
                    continue;

                // Handle [Separator]
                if (field.GetCustomAttribute<SeparatorAttribute>() != null)
                {
                    ImGui.Separator();
                    continue;
                }

                // Handle [Debug]
                var isDebug = field.GetCustomAttribute<DebugAttribute>();
                if (isDebug != null && !Settings.data.showDebug)
                    continue;

                if (isHeader && headerOpen)
                    ImGui.Indent(10);

                // Handle [Description]
                string description = null;
                if (field.GetCustomAttribute<DescriptionAttribute>() is DescriptionAttribute descriptionAttr)
                    description = descriptionAttr.Description;

                // Render [Checkbox]
                if (field.GetCustomAttribute<CheckboxAttribute>() is CheckboxAttribute checkboxAttr && field.FieldType == typeof(bool))
                {
                    bool val = (bool)value;
                    ImGui.Spacing();
                    ImGui.Text(checkboxAttr.Name);
                    if (description != null) ImGuiCommon.HelperIcon(description, 500);
                    if (ImGui.ImageButton(iconsUiImGui.ID, new Vector2(itemHeightImGui)))
                    {
                        field.SetValue(workingInstance, defaultValue);
                        anyChanged = true;
                    }
                    ImGui.SameLine();
                    if (ImGui.Checkbox("##" + field.Name, ref val))
                    {
                        field.SetValue(workingInstance, val);
                        anyChanged = true;
                    }
                }
                // Render [SliderInt]
                else if (field.GetCustomAttribute<SliderIntAttribute>() is SliderIntAttribute sliderIntAttr && field.FieldType == typeof(int))
                {
                    int val = (int)value;
                    ImGui.Spacing();
                    ImGui.Text(sliderIntAttr.Name);
                    if (description != null) ImGuiCommon.HelperIcon(description, 500);
                    if (ImGui.ImageButton(iconsUiImGui.ID, new Vector2(itemHeightImGui)))
                    {
                        field.SetValue(workingInstance, defaultValue);
                        anyChanged = true;
                    }
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                    if (ImGui.SliderInt("##" + field.Name, ref val, sliderIntAttr.Min, sliderIntAttr.Max))
                    {
                        field.SetValue(workingInstance, val);
                        anyChanged = true;
                    }
                }
                // Render [SliderFloat]
                else if (field.GetCustomAttribute<SliderFloatAttribute>() is SliderFloatAttribute sliderFloatAttr && field.FieldType == typeof(float))
                {
                    float val = (float)value;
                    ImGui.Spacing();
                    ImGui.Text(sliderFloatAttr.Name);
                    if (description != null) ImGuiCommon.HelperIcon(description, 500);
                    ImGui.PushID("##reset" + field.Name);
                    if (ImGui.ImageButton(iconsUiImGui.ID, new Vector2(itemHeightImGui)))
                    {
                        field.SetValue(workingInstance, defaultValue);
                        anyChanged = true;
                    }
                    ImGui.PopID();
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                    if (ImGui.SliderFloat("##" + field.Name, ref val, sliderFloatAttr.Min, sliderFloatAttr.Max, "%.2f", sliderFloatAttr.IsLog ? ImGuiSliderFlags.Logarithmic : ImGuiSliderFlags.None))
                    {
                        field.SetValue(workingInstance, val);
                        anyChanged = true;
                    }
                }
                // Render [Dropdown]
                else if (field.GetCustomAttribute<DropdownAttribute>() is DropdownAttribute dropdown)
                {
                    string val = (string)value;
                    int currentIndex = Array.IndexOf(dropdown.Options, val);
                    if (currentIndex < 0) currentIndex = 0;

                    ImGui.Spacing();
                    ImGui.Text(dropdown.Name);
                    if (description != null) ImGuiCommon.HelperIcon(description, 500);
                    if (ImGui.ImageButton(iconsUiImGui.ID, new Vector2(itemHeightImGui)))
                    {
                        field.SetValue(workingInstance, defaultValue);
                        anyChanged = true;
                    }
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                    if (ImGui.Combo("##" + field.Name, ref currentIndex, dropdown.Options, dropdown.Options.Length))
                    {
                        field.SetValue(workingInstance, dropdown.Options[currentIndex]);
                        anyChanged = true;
                    }
                }

                if (isHeader && headerOpen)
                    ImGui.Unindent(10);
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            if (ImGui.Button("Apply to Game"))
                ApplyToGame();
            ImGuiCommon.HelperIcon("Applies the current settings to the game and its corresponding script", 500);

            return anyChanged;
        }
    }
}
