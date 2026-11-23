using System;

namespace RoundedTB
{
    /// <summary>
    /// One executable image with two process modes: the default lightweight
    /// Win32 tray host, or the existing WPF settings UI via --settings-only.
    /// This dispatch runs before constructing a WPF Application so host mode
    /// does not pay WPF startup/dispatcher costs.
    /// </summary>
    internal static class LightEntryPoint
    {
        internal static bool IsSettingsMode(string[] args)
        {
            if (args == null) return false;
            foreach (string argument in args)
            {
                if (string.Equals(argument, "--settings-only", StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        [STAThread]
        private static int Main(string[] args)
        {
            if (IsSettingsMode(args))
            {
                App.SettingsOnly = true;
                App app = new App();
                return app.Run();
            }

            return TrayProbe.Program.Run(args);
        }
    }
}
