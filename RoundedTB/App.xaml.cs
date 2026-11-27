using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace RoundedTB
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        internal static bool SettingsOnly;
        internal static int HostProcessId;

        protected override void OnStartup(StartupEventArgs e)
        {
            foreach (string argument in e.Args)
            {
                if (string.Equals(argument, "--settings-only", StringComparison.OrdinalIgnoreCase))
                {
                    SettingsOnly = true;
                }
            }

            WPFUI.Theme.Watcher.Start();
        }
    }
}
