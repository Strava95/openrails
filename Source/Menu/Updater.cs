using System;
using System.IO;
using System.Linq;
using System.Reflection;
using ORTS.Settings;
using Squirrel;
using Squirrel.Sources;

namespace ORTS
{
    public class Updater
    {
        public static void Update()
        {
            if (!IsSquirrelInstalled())
            {
                Console.WriteLine("Updater not found. Automatic updates will not be available.");
                return;
            }

            var options = Environment.GetCommandLineArgs().Where(a => (a.StartsWith("-") || a.StartsWith("/"))).Select(a => a.Substring(1));
            var settings = new UserSettings(options);

            using (var manager = new UpdateManager(new GithubSource("https://github.com/Sharpe49/openrails", string.Empty, settings.PreRelease)))
            {
                Console.WriteLine("Checking for updates... ");
                var updates = manager.CheckForUpdate().Result;
                var releases = updates.ReleasesToApply;

                if (releases.Count > 0)
                {
                    Console.WriteLine("Downloading updates... ");
                    manager.DownloadReleases(releases).Wait();

                    Console.WriteLine("Applying updates... ");
                    var version = manager.ApplyReleases(updates).Result;

                    Console.WriteLine("Successfully updated to version {0}", version);
                    UpdateManager.RestartApp();
                }
                else
                {
                    Console.WriteLine("No updates available");
                }
            }
        }

        private static bool IsSquirrelInstalled()
        {
            try
            {
                Assembly assembly = Assembly.GetEntryAssembly();
                String updateDotExe = Path.Combine(new DirectoryInfo(Path.GetDirectoryName(assembly.Location)).Parent.FullName, "Update.exe");
                Boolean isInstalled = File.Exists(updateDotExe);

                return isInstalled;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error determining if Open Rails was installed by the installer.");
                return false;
            }
        }
    }
}
