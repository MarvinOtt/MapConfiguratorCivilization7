using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace MapConfiguratorCivilization7
{
    public class App : Game
    {
        private GraphicsDeviceManager graphics;
        public static SpriteBatch spriteBatch;
        public static App app;
        public static event EventHandler GraphicsChanged;
        GuiHandler guiHandler;

        public static ContentManager contentManager;
        public static GraphicsDevice graphicsDevice;

        public static int ScreenWidth;
        public static int ScreenHeight;

        private int GraphicsNeedApplyChanges = 0;

        public static Map map;

        // Gets called every time the Windows Size gets changed
        void Window_ClientSizeChanged(object sender, EventArgs e)
        {
            if (Window.ClientBounds.Width != 0)
                graphics.PreferredBackBufferWidth = Window.ClientBounds.Width;
            if (Window.ClientBounds.Height != 0)
                graphics.PreferredBackBufferHeight = Window.ClientBounds.Height;
            // Not Applying Graphics here because when resizing happens, ApplyChanges would be called too often which could cause a crash
            // When resizing happens, the Update Method is not going to be called so long until resizing is finished, and therefore Apply Changes gets only called once
            GraphicsNeedApplyChanges = 10;
        }

        public void UpdateEverythingOfGraphics(object sender, EventArgs e)
        {
            ScreenWidth = graphics.PreferredBackBufferWidth;
            ScreenHeight = graphics.PreferredBackBufferHeight;
            GraphicsDevice.PresentationParameters.RenderTargetUsage = RenderTargetUsage.PreserveContents;
        }

        public App()
        {
            app = this;
            GraphicsChanged += UpdateEverythingOfGraphics;
            graphics = new GraphicsDeviceManager(this)
            {
                GraphicsProfile = GraphicsProfile.HiDef,
                PreferredBackBufferWidth = 1920,
                PreferredBackBufferHeight = 1080,
                PreferredBackBufferFormat = SurfaceFormat.Color,
                IsFullScreen = false,
                SynchronizeWithVerticalRetrace = true

            };
            IsFixedTimeStep = false;
            Content.RootDirectory = "Content";
            contentManager = Content;
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            Window.AllowUserResizing = true;
            Window.ClientSizeChanged += Window_ClientSizeChanged;
            spriteBatch = new SpriteBatch(GraphicsDevice);
            GraphicsChanged(null, EventArgs.Empty);
            graphicsDevice = GraphicsDevice;
            BlendState blendState = new BlendState();
            blendState.AlphaSourceBlend = Blend.One;
            blendState.AlphaDestinationBlend = Blend.Zero;
            blendState.ColorSourceBlend = Blend.One;
            blendState.ColorDestinationBlend = Blend.Zero;
            blendState.AlphaBlendFunction = BlendFunction.Add;
            graphicsDevice.BlendState = blendState;
            graphicsDevice.SamplerStates[0] = SamplerState.PointClamp;
            graphicsDevice.SamplerStates[1] = SamplerState.LinearClamp;
            GraphicsDevice.PresentationParameters.RenderTargetUsage = RenderTargetUsage.DiscardContents;
            guiHandler = new GuiHandler(this);
            map = new Map();
            IO.Setup();
            base.Initialize();
        }

        protected override void LoadContent()
        {
            guiHandler.SetupUi();
            Settings.Load();

            map.scriptHandler.Initialize();
            map.mapRender.Center(true);
        }

        protected override void Update(GameTime gameTime)
        {
            IO.Update(gameTime.ElapsedGameTime.TotalMilliseconds);

            if (GraphicsNeedApplyChanges == 1)
            {
                graphics.ApplyChanges();
                GraphicsChanged(null, EventArgs.Empty);
            }
            if (GraphicsNeedApplyChanges >= 1)
                GraphicsNeedApplyChanges--;

            if (IsActive && !GuiHandler.IsUiActive)
                map.Update((float)gameTime.ElapsedGameTime.TotalMilliseconds);

            GuiHandler.Update();

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            map.Render();

            guiHandler.BeginDraw(gameTime);
            guiHandler.Draw();
            guiHandler.EndDraw();
            IO.UpdateEnd();
        }
    }
}