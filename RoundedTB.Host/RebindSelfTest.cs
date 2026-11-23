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
            return "PASS: rebind policy for changed, same, missing, and destroyed taskbar handles";
        }

        private static void Require(bool condition, string check)
        {
            if (!condition) throw new InvalidOperationException(check);
        }
    }
}
