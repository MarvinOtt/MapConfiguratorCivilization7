using ImGuiNET;
using MapConfiguratorCivilization7.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MapConfiguratorCivilization7
{
    public class GuiHandler
    {
        public static bool IsUiActive = false;
        public static bool ShowUi = true;

        public static float panelWidth = 200;

        public static ImGuiRenderer GuiRenderer;
        private App app;

        private string[] tabs = ["Scripts", "Settings"];

        public GuiHandler(App app)
        {
            this.app = app;
            GuiRenderer = new ImGuiRenderer(app);
            GuiRenderer.RebuildFontAtlas();

            ImGuiStyleConfig.SetStyle();
        }

        public void SetupUi()
        {

        }

        public static void Update()
        {
            if (IO.statesKeyboard.IsKeyToggleDown(Keys.F1))
                ShowUi ^= true;
        }


        public void BeginDraw(GameTime gameTime)
        {
            GuiRenderer.BeforeLayout(gameTime, app.IsActive);
        }

        public void Draw()
        {
            if (ShowUi)
            {
                ImGui.SetNextWindowPos(new Vector2(0, 0), ImGuiCond.Always);
                ImGui.SetNextWindowSize(new Vector2(280, App.ScreenHeight), ImGuiCond.Appearing);
                ImGui.SetNextWindowSizeConstraints(new Vector2(200, App.ScreenHeight), new Vector2(10000, App.ScreenHeight));
                if (ImGui.Begin("Map Settings", ImGuiWindowFlags.NoTitleBar))
                {
                    if (ImGui.BeginTabBar("MapSettingBar"))
                    {
                        foreach (var tab in tabs)
                        {
                            if (ImGui.BeginTabItem(tab))
                            {
                                ImGui.Spacing();
                                if (tab == tabs[0])
                                    MapSettingsUi.Draw();
                                else
                                    SettingsUi.Draw();

                                ImGui.EndTabItem();
                            }
                        }

                        ImGui.EndTabBar();
                    }
                    panelWidth = ImGui.GetWindowWidth();
                    ImGui.End();
                }
                MapPreviewUi.Draw();
            }
        }

        public void EndDraw()
        {
            GuiRenderer.AfterLayout();

            bool isModalOpen = false;
            IsUiActive = isModalOpen || ImGui.IsAnyItemActive() || ImGui.IsAnyItemHovered() || ImGui.IsWindowHovered(ImGuiHoveredFlags.AnyWindow | ImGuiHoveredFlags.AllowWhenBlockedByPopup);
        }
    }
}
