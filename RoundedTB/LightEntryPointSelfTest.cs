using System;

namespace RoundedTB
{
    internal static class LightEntryPointSelfTest
    {
        internal static string Run()
        {
            Require(!LightEntryPoint.IsSettingsMode(null), "null args select host mode");
            Require(!LightEntryPoint.IsSettingsMode(new string[0]), "empty args select host mode");
            Require(!LightEntryPoint.IsSettingsMode(new[] { "/settings" }), "host switches stay in host mode");
            Require(LightEntryPoint.IsSettingsMode(new[] { "--settings-only" }), "settings argument selects settings mode");
            Require(LightEntryPoint.IsSettingsMode(new[] { "--SETTINGS-ONLY" }), "settings argument is case-insensitive");
            Require(LightEntryPoint.IsSettingsMode(new[] { "/log", "--settings-only" }), "settings argument can follow other args");

            App settingsApp = new App();
            settingsApp.InitializeComponent();
            Require(settingsApp.StartupUri != null, "WPF application resources and StartupUri initialize");
            Require(settingsApp.Resources.Count > 0, "WPF application resources load");

            return "PASS: unified executable host/settings dispatch and WPF settings initialization";
        }

        private static void Require(bool condition, string check)
        {
            if (!condition) throw new InvalidOperationException(check);
        }
    }
}
