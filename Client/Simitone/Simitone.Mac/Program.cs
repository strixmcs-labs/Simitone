using FSO.Client;
using FSO.Common;
using FSO.LotView;
using Simitone.Client;
using System;
using System.IO;
using System.Threading;

namespace Simitone.Mac
{
    /// <summary>
    /// macOS entry point. Mirrors Simitone.Windows, minus WinForms, GDI+ and the Volcanic editor.
    /// Always uses the OpenGL (DesktopGL) backend.
    /// </summary>
    public static class Program
    {
        static int Main(string[] args)
        {
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

            FSOEnvironment.Enable3D = false;
            bool aa = false;
            bool jit = false;
            bool retina = true;
            string path = null;

            FSOEnvironment.Args = string.Join(" ", args);
            foreach (var arg in args)
            {
                if (arg.Length < 2 || arg[0] != '-') continue;
                var cmd = arg.Substring(1);
                if (cmd.StartsWith("lang")) GlobalSettings.Default.LanguageCode = byte.Parse(cmd.Substring(4));
                else if (cmd.StartsWith("hz")) GlobalSettings.Default.TargetRefreshRate = int.Parse(cmd.Substring(2));
                else if (cmd.StartsWith("path")) path = cmd.Substring(4).Trim('"');
                else
                {
                    switch (cmd)
                    {
                        case "3d": FSOEnvironment.Enable3D = true; break;
                        case "aa": aa = true; break;
                        case "jit": jit = true; break;
                        case "touch": FSOEnvironment.SoftwareKeyboard = true; break;
                        case "nosound": FSOEnvironment.NoSound = true; break;
                        //saved in config.ini, so it sticks for later launches
                        case "fullscreen": GlobalSettings.Default.Windowed = false; break;
                        case "windowed": GlobalSettings.Default.Windowed = true; break;
                        case "noretina": retina = false; break;
                    }
                }
            }

            var userDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Simitone/");
            Directory.CreateDirectory(userDir);

            path = MacGameLocator.FindTheSims1(path, userDir);
            if (path == null)
            {
                Console.Error.WriteLine("Couldn't find The Sims. Run with -path<folder>, set SIMITONE_TS1_PATH, " +
                    "or put the folder path in " + Path.Combine(userDir, "ts1path.txt") + ".");
                return 1;
            }
            Console.WriteLine("Using The Sims at " + path);

            FSO.Files.ImageLoaderHelpers.BitmapFunction = MacImageReader.Read;
            var latched = MacMouseLatch.Install();
            if (!latched) Console.Error.WriteLine("Couldn't hook SDL mouse events; quick clicks may be missed.");
            FSO.Common.Rendering.Framework.GameScreen.MouseStateFilter = state =>
                MacRetina.ScaleMouse(latched ? MacMouseLatch.Filter(state) : state);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => WriteCrashLog(userDir, e.ExceptionObject);

            FSOEnvironment.SoftwareDepth = false;
            FSOEnvironment.UseMRT = true;
            FSOEnvironment.ContentDir = "Content/";
            FSOEnvironment.GFXContentDir = "Content/OGL/";
            FSOEnvironment.UserDir = userDir;
            FSOEnvironment.Linux = false;
            FSOEnvironment.DirectX = false;
            FSOEnvironment.GameThread = Thread.CurrentThread;
            if (GlobalSettings.Default.LanguageCode == 0) GlobalSettings.Default.LanguageCode = 1;
            FSO.Files.Formats.IFF.Chunks.STR.DefaultLangCode = (FSO.Files.Formats.IFF.Chunks.STRLangCode)GlobalSettings.Default.LanguageCode;

            GlobalSettings.Default.StartupPath = path;
            GlobalSettings.Default.TS1HybridEnable = true;
            GlobalSettings.Default.TS1HybridPath = path;
            GlobalSettings.Default.ClientVersion = "0";
            GlobalSettings.Default.LightingMode = 3;
            GlobalSettings.Default.AntiAlias = aa ? 1 : 0;
            GlobalSettings.Default.ComplexShaders = true;
            GlobalSettings.Default.EnableTransitions = true;

            var assemblies = new FSO.SimAntics.JIT.Runtime.AssemblyStore();
            if (jit) assemblies.InitAOT();
            FSO.SimAntics.Engine.VMTranslator.INSTANCE = new FSO.SimAntics.JIT.Runtime.VMAOTTranslator(assemblies);

            GameFacade.DirectX = false;
            World.DirectX = false;
            using (var game = new SimitoneGame())
            {
                //the window and GL context exist once SimitoneGame is constructed; nothing has been sized from them yet
                if (retina && MacRetina.Enable(game.Window.Handle))
                {
                    SimitoneGame.PixelSizeProvider = MacRetina.GetPixelSize;
                    game.ApplyPixelBackBuffer();
                }
                if (Environment.GetEnvironmentVariable("SIMITONE_DISPLAY_DIAG") == "1") AddDisplayProbe(game);
                if (Environment.GetEnvironmentVariable("SIMITONE_DISPLAY_DIAG") == "2") AddResizeProbe(game);
                if (Environment.GetEnvironmentVariable("SIMITONE_DISPLAY_DIAG") == "3") AddInputLogger(game, userDir);
                game.Run();
            }
            return 0;
        }

        /// <summary>
        /// Prints window, drawable and display sizes on the first frame, then quits. For working out Retina handling.
        /// </summary>
        private static void AddDisplayProbe(SimitoneGame game)
        {
            var inner = FSO.Common.Rendering.Framework.GameScreen.MouseStateFilter;
            var frames = 0;
            FSO.Common.Rendering.Framework.GameScreen.MouseStateFilter = state =>
            {
                if (++frames == 30)
                {
                    var handle = game.Window.Handle;
                    var pp = game.GraphicsDevice.PresentationParameters;
                    Console.WriteLine("DIAG window(points)=" + MacSdl.GetWindowSize(handle)
                        + " drawable(pixels)=" + MacSdl.GetDrawableSize(handle)
                        + " flags=0x" + MacSdl.GetWindowFlags(handle).ToString("x")
                        + " clientBounds=" + game.Window.ClientBounds
                        + " backbuffer=" + pp.BackBufferWidth + "x" + pp.BackBufferHeight
                        + " viewport=" + game.GraphicsDevice.Viewport.Bounds
                        + " desktop=" + MacSdl.GetDesktopMode(0)
                        + " retinaPixels=" + MacRetina.GetPixelSize()
                        + " dpiScale=" + FSOEnvironment.DPIScaleFactor
                        + " ui=" + GlobalSettings.Default.GraphicsWidth + "x" + GlobalSettings.Default.GraphicsHeight
                        + " mouse=" + state.Position);
                    game.Exit();
                }
                return inner != null ? inner(state) : state;
            };
        }

        /// <summary>
        /// Resizes the window at frame 60 and moves it at frame 120, logging focus, back buffer and mouse state
        /// around each, then quits at frame 180. For reproducing input loss after window changes.
        /// </summary>
        private static unsafe void AddResizeProbe(SimitoneGame game)
        {
            var inner = FSO.Common.Rendering.Framework.GameScreen.MouseStateFilter;
            var frames = 0;
            FSO.Common.Rendering.Framework.GameScreen.MouseStateFilter = state =>
            {
                frames++;
                var handle = game.Window.Handle;
                if (frames == 60)
                {
                    var (w, h) = MacSdl.GetWindowSize(handle);
                    ((delegate* unmanaged[Cdecl]<IntPtr, int, int, void>)MacSdl.Get("SDL_SetWindowSize"))(handle, w - 200, h - 100);
                    Console.WriteLine("DIAG2 resized");
                }
                if (frames == 120)
                {
                    ((delegate* unmanaged[Cdecl]<IntPtr, int, int, void>)MacSdl.Get("SDL_SetWindowPosition"))(handle, 300, 150);
                    Console.WriteLine("DIAG2 moved");
                }
                var result = inner != null ? inner(state) : state;
                if ((frames >= 55 && frames <= 70) || (frames >= 118 && frames <= 126) || frames % 20 == 0)
                {
                    var pp = game.GraphicsDevice.PresentationParameters;
                    Console.WriteLine("DIAG2 f=" + frames + " active=" + game.IsActive
                        + " points=" + game.Window.ClientBounds + " pp=" + pp.BackBufferWidth + "x" + pp.BackBufferHeight
                        + " vp=" + game.GraphicsDevice.Viewport.Width + "x" + game.GraphicsDevice.Viewport.Height
                        + " dpi=" + FSOEnvironment.DPIScaleFactor
                        + " ui=" + GlobalSettings.Default.GraphicsWidth + "x" + GlobalSettings.Default.GraphicsHeight
                        + " mouseIn=" + state.Position + " mouseOut=" + result.Position
                        + " screen=" + (game.uiLayer?.CurrentUIScreen?.ScreenWidth) + "x" + (game.uiLayer?.CurrentUIScreen?.ScreenHeight));
                }
                if (frames == 180) game.Exit();
                return result;
            };
        }

        /// <summary>
        /// Logs input and window state to input.log whenever it changes, plus a heartbeat every 2 seconds.
        /// For diagnosing input loss after real (user-driven) window moves and resizes.
        /// </summary>
        private static void AddInputLogger(SimitoneGame game, string userDir)
        {
            var log = new StreamWriter(Path.Combine(userDir, "input.log"), false) { AutoFlush = true };
            var inner = FSO.Common.Rendering.Framework.GameScreen.MouseStateFilter;
            var frames = 0;
            string last = null;
            FSO.Common.Rendering.Framework.GameScreen.MouseStateFilter = state =>
            {
                frames++;
                var result = inner != null ? inner(state) : state;
                var pp = game.GraphicsDevice.PresentationParameters;
                var us = GameFacade.LastUpdateState;
                var mm = (us != null && us.MouseStates.Count > 0) ? us.MouseStates[0] : null;
                var key = "active=" + game.IsActive
                    + " points=" + game.Window.ClientBounds.Width + "x" + game.Window.ClientBounds.Height
                    + " pp=" + pp.BackBufferWidth + "x" + pp.BackBufferHeight
                    + " vp=" + game.GraphicsDevice.Viewport.Width + "x" + game.GraphicsDevice.Viewport.Height
                    + " dpi=" + FSOEnvironment.DPIScaleFactor
                    + " btnIn=" + state.LeftButton + " btnOut=" + result.LeftButton
                    + " process=" + us?.ProcessMouseEvents
                    + " down=" + (mm?.LastMouseDown != null) + " over=" + mm?.LastMouseOver?.Region
                    + " sdlFlags=0x" + MacSdl.GetWindowFlags(game.Window.Handle).ToString("x");
                if (key != last || frames % 120 == 0)
                    log.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff") + " f=" + frames + " " + key + " mouse=" + state.Position + "->" + result.Position);
                last = key;
                return result;
            };
        }

        private static void WriteCrashLog(string userDir, object exception)
        {
            var log = Path.Combine(userDir, "crash.log");
            File.AppendAllText(log, DateTime.Now + Environment.NewLine + exception + Environment.NewLine + Environment.NewLine);
            Console.Error.WriteLine("Simitone crashed. Details written to " + log);
            Console.Error.WriteLine(exception);
        }
    }
}
