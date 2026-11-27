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

        /// <summary>
        /// The host passes its process id so the settings window can close
        /// itself when the resident host exits.
        /// </summary>
        internal static bool TryGetHostPid(string[] args, out int pid)
        {
            pid = 0;
            if (args == null) return false;
            const string prefix = "--host-pid";
            for (int i = 0; i < args.Length; i++)
            {
                string argument = args[i];
                string value = null;
                if (string.Equals(argument, prefix, StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 >= args.Length) return false;
                    value = args[i + 1];
                }
                else if (argument.StartsWith(prefix + "=", StringComparison.OrdinalIgnoreCase))
                {
                    value = argument.Substring(prefix.Length + 1);
                }
                if (value == null) continue;
                return int.TryParse(value, out pid) && pid > 0;
            }
            return false;
        }

        [STAThread]
        private static int Main(string[] args)
        {
            if (IsSettingsMode(args))
            {
                App.SettingsOnly = true;
                int hostPid;
                if (TryGetHostPid(args, out hostPid)) App.HostProcessId = hostPid;
                App app = new App();
                app.InitializeComponent();
                return app.Run();
            }

            return TrayProbe.Program.Run(args);
        }
    }
}
