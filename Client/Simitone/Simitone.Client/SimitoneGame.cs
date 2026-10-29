/*
This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
If a copy of the MPL was not distributed with this file, You can obtain one at
http://mozilla.org/MPL/2.0/.
*/

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Threading;
using FSO.Common.Rendering.Framework;
using FSO.LotView;
using FSO.HIT;
using FSO.Client.UI;
using FSO.Client.GameContent;
using FSO.Common.Utils;
using FSO.Common;
using Microsoft.Xna.Framework.Audio;
using FSO.HIT.Model;
using FSO.Client;
using FSO.Files;
using FSO.SimAntics;
using MSDFData;
using FSO.LotView.Model;
using Simitone.Client.UI.Panels;

namespace Simitone.Client
{
    /// <summary>
    /// This is the main type for your game
    /// </summary>
    public class SimitoneGame : FSO.Common.Rendering.Framework.Game
    {
        public UILayer uiLayer;
        public _3DLayer SceneMgr;
        private bool HasUpdated;

        /// <summary>
        /// Set by a platform launcher when the window's drawable is larger than its size in points (macOS Retina).
        /// Returns the drawable size in pixels. MonoGame sizes the back buffer in points, so ApplyPixelBackBuffer
        /// corrects it and sets DPIScaleFactor to keep the UI the same size on screen.
        /// </summary>
        public static Func<Point> PixelSizeProvider;

        public SimitoneGame() : base()
        {
            GameFacade.Game = this;
            GameThread.Game = Thread.CurrentThread;
            if (GameFacade.DirectX) TimedReferenceController.SetMode(CacheType.PERMANENT);
            Content.RootDirectory = FSOEnvironment.GFXContentDir;

            TargetElapsedTime = new TimeSpan(10000000 / GlobalSettings.Default.TargetRefreshRate);
            FSOEnvironment.RefreshRate = GlobalSettings.Default.TargetRefreshRate;
            FSOEnvironment.TexCompress = false;
            UILotControl.ShowSimanticsExceptions = !FSOEnvironment.Args.Contains("nosimantics-exc");

            if (!FSOEnvironment.SoftwareKeyboard)
            {
                Graphics.SynchronizeWithVerticalRetrace = true;
                Graphics.PreferredBackBufferWidth = GlobalSettings.Default.GraphicsWidth;
                Graphics.PreferredBackBufferHeight = GlobalSettings.Default.GraphicsHeight;
                Graphics.HardwareModeSwitch = false;
                Graphics.ApplyChanges();
            }

            this.Window.AllowUserResizing = true;
            this.Window.ClientSizeChanged += new EventHandler<EventArgs>(Window_ClientSizeChanged);

            Thread.CurrentThread.Name = "Game";

        }

        /// <summary>
        /// Makes the back buffer and viewport match the drawable's pixel size when PixelSizeProvider is set.
        /// UI sizes (GraphicsWidth/Height) stay in points and DPIScaleFactor covers the difference.
        /// Returns true if it changed anything.
        /// </summary>
        public bool ApplyPixelBackBuffer()
        {
            if (PixelSizeProvider == null || GraphicsDevice == null) return false;
            var pixels = PixelSizeProvider();
            var points = Window.ClientBounds;
            if (pixels.X <= 0 || pixels.Y <= 0 || points.Width <= 0 || points.Height <= 0) return false;

            var pp = GraphicsDevice.PresentationParameters;
            if (pp.BackBufferWidth == pixels.X && pp.BackBufferHeight == pixels.Y) return false;

            pp.BackBufferWidth = pixels.X;
            pp.BackBufferHeight = pixels.Y;
            GraphicsDevice.Viewport = new Viewport(0, 0, pixels.X, pixels.Y);
            FSOEnvironment.DPIScaleFactor = pixels.X / (float)points.Width;
            GlobalSettings.Default.GraphicsWidth = points.Width;
            GlobalSettings.Default.GraphicsHeight = points.Height;

            if (uiLayer?.CurrentUIScreen != null)
            {
                uiLayer.SpriteBatch.ResizeBuffer(pixels.X, pixels.Y);
                uiLayer.CurrentUIScreen.GameResized();
            }
            return true;
        }

        bool newChange = false;
        void Window_ClientSizeChanged(object sender, EventArgs e)
        {
            //MonoGame has just reset the back buffer to the window's point size
            if (!newChange && ApplyPixelBackBuffer() && !GlobalSettings.Default.Windowed) return;
            if (newChange || !GlobalSettings.Default.Windowed || FSOEnvironment.SoftwareKeyboard) return;
            if (Window.ClientBounds.Width == 0 || Window.ClientBounds.Height == 0) return;
            newChange = true;
            var width = Math.Max(1, Window.ClientBounds.Width);
            var height = Math.Max(1, Window.ClientBounds.Height);
            Graphics.PreferredBackBufferWidth = width;
            Graphics.PreferredBackBufferHeight = height;
            Graphics.ApplyChanges();

            GlobalSettings.Default.GraphicsWidth = width;
            GlobalSettings.Default.GraphicsHeight = height;

            newChange = false;
            //ApplyChanges set the back buffer back to points; this also resizes the UI if it changes anything
            if (ApplyPixelBackBuffer()) return;
            if (uiLayer?.CurrentUIScreen == null) return;

            uiLayer.SpriteBatch.ResizeBuffer(GlobalSettings.Default.GraphicsWidth, GlobalSettings.Default.GraphicsHeight);
            uiLayer.CurrentUIScreen.GameResized();
        }

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {

            var settings = GlobalSettings.Default;
            if (FSOEnvironment.DPIScaleFactor != 1 || FSOEnvironment.SoftwareDepth)
            {
                settings.GraphicsWidth = (int)(GraphicsDevice.Viewport.Width / FSOEnvironment.DPIScaleFactor);
                settings.GraphicsHeight = (int)(GraphicsDevice.Viewport.Height / FSOEnvironment.DPIScaleFactor);
            }

            var initialMode = (GlobalGraphicsMode)settings.GlobalGraphicsMode;
            if (FSOEnvironment.Enable3D)
            {
                if (initialMode == GlobalGraphicsMode.Full2D) initialMode = GlobalGraphicsMode.Full3D;
            }
            else
            {
                initialMode = GlobalGraphicsMode.Full2D;
            }
            GraphicsModeControl.ChangeMode(initialMode);
            GraphicsModeControl.ModeChanged += SaveGraphicsModePreference;

            FSO.LotView.WorldConfig.Current = new FSO.LotView.WorldConfig()
            {
                LightingMode = settings.LightingMode,
                SmoothZoom = settings.SmoothZoom,
                SurroundingLots = settings.SurroundingLotMode,
                AA = settings.AntiAlias,
                Directional = settings.DirectionalLight3D,
                Complex = settings.ComplexShaders,
                EnableTransitions = settings.EnableTransitions
            };

            OperatingSystem os = Environment.OSVersion;
            PlatformID pid = os.Platform;
            GameFacade.Linux = (pid == PlatformID.MacOSX || pid == PlatformID.Unix);

            FSO.Content.Content.Target = FSO.Content.FSOEngineMode.TS1;
            FSO.Content.Content.TS1HybridBasePath = GlobalSettings.Default.TS1HybridPath;
            if (FSOEnvironment.Enable3D) FSO.Files.RC.DGRP3DMesh.InitRCWorkers();
            //FSO.Content.Content.Init(GlobalSettings.Default.StartupPath, GraphicsDevice);
            FSO.SimAntics.VMAvatar.MissingIconProvider = Simitone.Client.UI.Model.UIIconCache.GetObject;
            base.Initialize();

            GameFacade.GameThread = Thread.CurrentThread;

            SceneMgr = new _3DLayer();
            SceneMgr.Initialize(GraphicsDevice);

            GameFacade.Scenes = SceneMgr;
            GameFacade.GraphicsDevice = GraphicsDevice;
            GameFacade.GraphicsDeviceManager = Graphics;
            GameFacade.Cursor = new CursorManager(GraphicsDevice);
            if (!GameFacade.Linux)
            {
                CurLoader.BmpLoaderFunc = ImageLoader.BaseFunction;
                GameFacade.Cursor.Init(GlobalSettings.Default.TS1HybridPath, true);
            }

            /** Init any computed values **/
            GameFacade.Init();

            //init audio now
            HITVM.Init();
            var hit = HITVM.Get();
            hit.SetMasterVolume(HITVolumeGroup.FX, GlobalSettings.Default.FXVolume / 10f);
            hit.SetMasterVolume(HITVolumeGroup.MUSIC, GlobalSettings.Default.MusicVolume / 10f);
            hit.SetMasterVolume(HITVolumeGroup.VOX, GlobalSettings.Default.VoxVolume / 10f);
            hit.SetMasterVolume(HITVolumeGroup.AMBIENCE, GlobalSettings.Default.AmbienceVolume / 10f);

            ContentStrings.TS1 = true;
            GameFacade.Strings = new ContentStrings();

            GraphicsDevice.RasterizerState = new RasterizerState() { CullMode = CullMode.None };

            try
            {
                var audioTest = new SoundEffect(new byte[2], 44100, AudioChannels.Mono); //initialises XAudio.
                audioTest.CreateInstance().Play();
            }
            catch (Exception e)
            {
                //MessageBox.Show("Failed to initialize audio: \r\n\r\n" + e.StackTrace);
            }

            this.IsFixedTimeStep = true;

            WorldContent.Init(this.Services, Content.RootDirectory);
            base.Screen.Layers.Add(SceneMgr);
            base.Screen.Layers.Add(uiLayer);
            GameFacade.LastUpdateState = base.Screen.State;

            if (!GlobalSettings.Default.Windowed && !GameFacade.GraphicsDeviceManager.IsFullScreen)
            {
                GameFacade.GraphicsDeviceManager.ToggleFullScreen();
            }
        }

        private void SaveGraphicsModePreference(GlobalGraphicsMode obj)
        {
            GlobalSettings.Default.GlobalGraphicsMode = (int)obj;
            GlobalSettings.Default.Save();
        }

        /// <summary>
        /// Run this instance with GameRunBehavior forced as Synchronous.
        /// </summary>
        public new void Run()
        {
            Run(GameRunBehavior.Synchronous);
        }

        /// <summary>
        /// Only used on desktop targets. Use extensive reflection to AVOID linking on iOS!
        /// </summary>
        void AddTextInput()
        {
            this.Window.GetType().GetEvent("TextInput").AddEventHandler(this.Window, (EventHandler<TextInputEventArgs>)GameScreen.TextInput);
        }

        void RegainFocus(object sender, EventArgs e)
        {
            GameFacade.Focus = true;
        }

        void LostFocus(object sender, EventArgs e)
        {
            GameFacade.Focus = false;
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            Effect vitaboyEffect = null;
            try
            {
                GameFacade.MainFont = new FSO.Client.UI.Framework.Font();
                //GameFacade.MainFont.AddSize(12, Content.Load<SpriteFont>("Fonts/Mobile_15px"));
                //GameFacade.MainFont.AddSize(15, Content.Load<SpriteFont>("Fonts/Mobile_20px"));
                //GameFacade.MainFont.AddSize(19, Content.Load<SpriteFont>("Fonts/Mobile_25px"));
                //GameFacade.MainFont.AddSize(37, Content.Load<SpriteFont>("Fonts/Mobile_50px"));

                GameFacade.EdithFont = new FSO.Client.UI.Framework.Font();
                //GameFacade.EdithFont.AddSize(12, Content.Load<SpriteFont>("Fonts/Trebuchet_12px"));
                //GameFacade.EdithFont.AddSize(14, Content.Load<SpriteFont>("Fonts/Trebuchet_14px"));

                GameFacade.VectorFont = new FSO.UI.Framework.MSDFFont(Content.Load<FieldFont>("../Fonts/mobile"));
                GameFacade.EdithVectorFont = new FSO.UI.Framework.MSDFFont(Content.Load<FieldFont>("../Fonts/trebuchet"));
                GameFacade.EdithVectorFont.VectorScale = 0.366f;
                GameFacade.EdithVectorFont.Height = 15;
                GameFacade.EdithVectorFont.YOff = 11;

                FSO.UI.Framework.MSDFFont.MSDFEffect = Content.Load<Effect>("Effects/MSDFFont");

                vitaboyEffect = Content.Load<Effect>("Effects/Vitaboy"+((FSOEnvironment.SoftwareDepth)?"iOS":""));
                uiLayer = new UILayer(this);
            }
            catch (Exception e)
            {
                //MessageBox.Windows.Forms.MessageBox.Show("Content could not be loaded. Make sure that the FreeSO content has been compiled! (ContentSrc/TSOClientContent.mgcb)");
                Console.WriteLine(e.ToString());
                Exit();
            }

            FSO.Vitaboy.Avatar.setVitaboyEffect(vitaboyEffect);
        }

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// all content.
        /// </summary>
        protected override void UnloadContent()
        {
            // TODO: Unload any non ContentManager content here
        }


        protected override void OnExiting(object sender, ExitingEventArgs args)
        {
            base.OnExiting(sender, args);
            GameThread.SetKilled();
            args.Cancel = !(GameFacade.Screens.CurrentUIScreen?.CloseAttempt() ?? true);
        }

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {
            if (!HasUpdated)
            {
                this.IsMouseVisible = true;
                if (!FSOEnvironment.SoftwareKeyboard) AddTextInput();
                this.Window.Title = "Simitone";
                HasUpdated = true;
                GameFacade.Screens = uiLayer;
                GameController.EnterLoading();
            }
            GameThread.UpdateExecuting = true;

            //catches back buffer resets that don't raise ClientSizeChanged (fullscreen toggles, moving between displays)
            ApplyPixelBackBuffer();

            if (HITVM.Get() != null) HITVM.Get().Tick();

            base.Update(gameTime);
            GameThread.UpdateExecuting = false;
        }
    }
}
