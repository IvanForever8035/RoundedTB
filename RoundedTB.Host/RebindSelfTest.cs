using System;

namespace TrayProbe
{
    internal static class RebindSelfTest
    {
        internal static string Run()
        {
            Require(Program.ShouldRebind(new IntPtr(1), new IntPtr(2), true), "changed handle requests a rebind");
            Require(!Program.ShouldRebind(new IntPtr(1), new IntPtr(1), true), "same live handle does not rebind");
            Require(!Program.ShouldRebind(new IntPtr(1), IntPtr.Zero, true), "missing live handle waits");
            Require(Program.ShouldRebind(new IntPtr(1), IntPtr.Zero, false), "destroyed current handle rebinds");
            Require(!Program.ShouldRebind(IntPtr.Zero, new IntPtr(2), true), "no current handle does not rebind");
            Require(Program.ShouldDeferStartup(true, true), "settings host defers startup when the taskbar is missing");
            Require(!Program.ShouldDeferStartup(true, false), "missing settings file is fatal at startup");
            Require(!Program.ShouldDeferStartup(false, true), "args-driven takeover does not defer startup");
            Require(Program.IsGeometrySatisfied(true, true, true, 451, 1618),
                "measured app and tray geometry satisfies both targets");
            Require(!Program.IsGeometrySatisfied(true, true, true, 451, 0),
                "missing tray geometry blocks readiness");
            Require(!Program.IsGeometrySatisfied(true, true, true, 0, 1618),
                "missing app geometry blocks readiness");
            Require(!Program.IsGeometrySatisfied(true, false, false, 0, 0),
                "missing taskbar blocks readiness");
            Require(Program.IsGeometrySatisfied(false, false, false, 0, 0),
                "no enabled target needs no geometry");
            Require(Program.IsGeometrySatisfied(false, true, true, 0, 1618),
                "tray-only readiness ignores app geometry");
            Require(Program.ChooseTrayLeft(0, 0, 1618, 1920) == 1618,
                "zero-width hidden tray window falls back to UIA bounds");
            Require(Program.ChooseTrayLeft(1600, 1920, 1618, 1880) == 1600,
                "valid legacy tray bounds take precedence over UIA");
            Require(Program.ChooseTrayLeft(0, 0, 0, 0) == 0,
                "tray remains unmeasured when both sources are empty");
            Require(Program.StartupCommandTargetsCurrentExe("\"D:\\Stable\\RoundedTBHost.exe\"",
                "D:\\Stable\\RoundedTBHost.exe"), "quoted startup path matches the current exe");
            Require(!Program.StartupCommandTargetsCurrentExe("\"D:\\Stable\\RoundedTBHost.exe\"",
                "D:\\Test\\RoundedTBHost.exe"), "startup path mismatch is detected without rewriting it");
            Require(Program.StartupCommandTargetsCurrentExe("D:\\Stable Folder\\RoundedTBHost.exe --arg",
                "D:\\Stable Folder\\RoundedTBHost.exe"), "unquoted command path is compared through the exe suffix");
            return "PASS: rebind policy, startup deferral, geometry readiness, and startup target path checks";
        }

        private static void Require(bool condition, string check)
        {
            if (!condition) throw new InvalidOperationException(check);
        }
    }
}
