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
            return "PASS: rebind policy for changed, same, missing, and destroyed taskbar handles; startup deferral only for a settings host";
        }

        private static void Require(bool condition, string check)
        {
            if (!condition) throw new InvalidOperationException(check);
        }
    }
}
