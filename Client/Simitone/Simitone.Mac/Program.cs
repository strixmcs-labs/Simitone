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
            if (MacMouseLatch.Install()) FSO.Common.Rendering.Framework.GameScreen.MouseStateFilter = MacMouseLatch.Filter;
            else Console.Error.WriteLine("Couldn't hook SDL mouse events; quick clicks may be missed.");
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
                game.Run();
            }
            return 0;
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
