using System;
using System.IO;

namespace RoundedTB
{
    /// <summary>
    /// Per-user data locations for both the host and the settings UI. Keeping
    /// every runtime file in one folder under %LOCALAPPDATA% lets the Light EXE
    /// run standalone without writing beside itself.
    /// </summary>
    public static class AppPaths
    {
        public static string Directory
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RoundedTB"); }
        }

        public static string ConfigPath
        {
            get { return Path.Combine(Directory, "rtb.json"); }
        }

        public static string SettingsLogPath
        {
            get { return Path.Combine(Directory, "rtb.log"); }
        }

        public static string HostLogPath
        {
            get { return Path.Combine(Directory, "rtb-host.log"); }
        }

        public static string LegacyConfigPath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "rtb.json"); }
        }

        /// <summary>
        /// Creates the data folder and copies a settings file left by older
        /// builds. The legacy file is kept; an existing new file always wins.
        /// </summary>
        public static void Prepare()
        {
            try
            {
                System.IO.Directory.CreateDirectory(Directory);
                if (File.Exists(ConfigPath) || !File.Exists(LegacyConfigPath)) return;

                string temporary = ConfigPath + ".migrating";
                File.Copy(LegacyConfigPath, temporary, true);
                if (File.Exists(ConfigPath)) File.Delete(temporary);
                else File.Move(temporary, ConfigPath);
            }
            catch (Exception)
            {
                // Best effort: callers report missing config through their
                // normal load path.
            }
        }
    }
}
