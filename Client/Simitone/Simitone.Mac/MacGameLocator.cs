using System;
using System.IO;
using System.Linq;

namespace Simitone.Mac
{
    /// <summary>
    /// Finds a The Sims 1 install (Complete or Legacy Collection) on macOS.
    /// </summary>
    public static class MacGameLocator
    {
        private static readonly string[] FolderNames =
        {
            "The Sims Legacy Collection",
            "The Sims Complete Collection",
            "The Sims"
        };

        /// <summary>
        /// Checks, in order: the -path argument, SIMITONE_TS1_PATH, ts1path.txt in the user folder,
        /// then the usual folder names in Documents, Desktop, Applications and next to the app.
        /// Returns the path with a trailing slash, or null.
        /// </summary>
        public static string FindTheSims1(string argPath, string userDir)
        {
            var candidates = new[]
            {
                argPath,
                Environment.GetEnvironmentVariable("SIMITONE_TS1_PATH"),
                ReadSavedPath(userDir)
            }.Concat(DefaultLocations());

            foreach (var candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate)) continue;
                var full = Path.GetFullPath(ExpandHome(candidate.Trim()));
                if (IsTheSims1(full)) return full.TrimEnd('/') + "/";
            }
            return null;
        }

        public static bool IsTheSims1(string dir)
        {
            return File.Exists(Path.Combine(dir, "GameData", "Behavior.iff"));
        }

        private static string ReadSavedPath(string userDir)
        {
            var file = Path.Combine(userDir, "ts1path.txt");
            return File.Exists(file) ? File.ReadAllText(file) : null;
        }

        private static string[] DefaultLocations()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var roots = new[]
            {
                Path.Combine(home, "Documents"),
                Path.Combine(home, "Desktop"),
                "/Applications",
                Path.Combine(AppContext.BaseDirectory, "..")
            };
            return roots.SelectMany(root => FolderNames.Select(name => Path.Combine(root, name))).ToArray();
        }

        private static string ExpandHome(string path)
        {
            if (path == "~" || path.StartsWith("~/"))
                return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + path.Substring(1);
            return path;
        }
    }
}
