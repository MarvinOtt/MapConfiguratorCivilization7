using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;

namespace MapConfiguratorCivilization7
{
    public class MapRender
    {
        Cube mesh;
        Effect mapPreview;
        Texture2D icons;

        private Map map;
        Vector2 mapPos = Vector2.Zero;
        float mapZoom = 8;

        readonly float SQRT3HALF = (float)Math.Sqrt(3.0f) / 2.0f;

        public bool renderDebug = false, wrapMap = false, showMapBorder = false, showHexBorder = false;

        public MapRender(Map map)
        {
            this.map = map;
            mesh = new Cube(App.graphicsDevice);

            mapPreview = App.contentManager.Load<Effect>("mapPreview");
            icons = App.contentManager.Load<Texture2D>("iconFinal");
        }

        public void Center(bool rescale = false)
        {
            if (rescale)
            {
                float mapZoomMaxX = ((App.ScreenWidth - GuiHandler.panelWidth) * 0.95f) / (App.map.mapSize.X * SQRT3HALF);
                float mapZoomMaxY = (App.ScreenHeight * 0.95f) / (App.map.mapSize.Y * 0.75f);
                mapZoom = Math.Min(mapZoomMaxX, mapZoomMaxY);
            }
            float mapPosX = (GuiHandler.panelWidth + (App.ScreenWidth - GuiHandler.panelWidth) * 0.5f) / mapZoom - (App.map.mapSize.X * SQRT3HALF * 0.5f);
            float mapPosY = (App.ScreenHeight * 0.5f) / mapZoom - (App.map.mapSize.Y * 0.75f * 0.5f);
            mapPos = new Vector2(mapPosX, mapPosY);
        }

        public void Update()
        {
            if (IO.statesMouse.New.RightButton == ButtonState.Pressed)
            {
                Vector2 mouseDif = (IO.statesMouse.New.Position - IO.statesMouse.Old.Position).ToVector2();
                mapPos += (mouseDif * new Vector2(1, -1)) / mapZoom;
            }
            int scrollWheelDif = IO.statesMouse.New.ScrollWheelValue - IO.statesMouse.Old.ScrollWheelValue;
            if (scrollWheelDif != 0)
            {
                float originalZoom = mapZoom;
                mapZoom *= 1 + Math.Sign(scrollWheelDif) * 0.15f;
                mapZoom = MathHelper.Clamp(mapZoom, 1.0f, 2000.0f);
                float appliedZoom = mapZoom / originalZoom;

                Vector2 mousePosFromBotLeft = new Vector2(IO.statesMouse.New.X, App.ScreenHeight - IO.statesMouse.New.Y);
                Vector2 mapPosDifToMouse = mousePosFromBotLeft / originalZoom;
                mapPos -= mapPosDifToMouse * (1 - 1.0f / appliedZoom);
            }
        }

        public void Render()
        {
            mapPreview.Parameters["screenWidth"]?.SetValue(App.ScreenWidth);
            mapPreview.Parameters["screenHeight"]?.SetValue(App.ScreenHeight);
            mapPreview.Parameters["WorldProjection"].SetValue(Matrix.CreateTranslation(new Vector3(-0.5f)) * (Matrix.CreateLookAt(Vector3.Zero, Vector3.Backward, Vector3.Up) * Matrix.CreatePerspectiveFieldOfView(1, 1, 0.001f, 1000)));
            mapPreview.Parameters["mapSizeX"]?.SetValue(map.mapSize.X);
            mapPreview.Parameters["mapSizeY"]?.SetValue(map.mapSize.Y);
            mapPreview.Parameters["zoom"]?.SetValue(mapZoom);
            mapPreview.Parameters["pos"]?.SetValue(mapPos);
            mapPreview.Parameters["mapSampler+mapTiles"]?.SetValue(map.mapData.tilesGpu);
            mapPreview.Parameters["mapSampler+mapDebug"]?.SetValue(map.mapData.debugGpu);
            mapPreview.Parameters["biomeColors"]?.SetValue(Settings.data.biomeColors);
            mapPreview.Parameters["iconSampler+icons"]?.SetValue(icons);
            mapPreview.Parameters["wrapMap"]?.SetValue(wrapMap);
            mapPreview.Parameters["renderDebug"]?.SetValue(renderDebug);
            mapPreview.Parameters["showMapBorder"]?.SetValue(showMapBorder);
            mapPreview.Parameters["showHexBorder"]?.SetValue(showHexBorder);
            mapPreview.Parameters["hexBorderSize"]?.SetValue(0.08f);
            mapPreview.Parameters["superSamplingCount"]?.SetValue(Settings.data.superSamplingCount);


            mapPreview.CurrentTechnique.Passes[0].Apply();

            App.graphicsDevice.SetVertexBuffer(mesh);
            App.graphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, mesh.VertexCount / 3);
        }
    }
}
