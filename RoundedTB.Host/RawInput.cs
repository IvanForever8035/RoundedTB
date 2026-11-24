using System;
using System.Runtime.InteropServices;

namespace TrayProbe
{
    internal static class RawInput
    {
        internal static bool Enabled;
        internal static int X;
        internal static int Y;
        internal static int EdgeOnCount;
        internal static int EdgeOffCount;

        private static int lastTick;

        internal static bool Start(IntPtr hwnd)
        {
            RAWINPUTDEVICE[] devices = new RAWINPUTDEVICE[1];
            devices[0].UsagePage = 1;
            devices[0].Usage = 2;
            devices[0].Flags = 0x00000100; // RIDEV_INPUTSINK
            devices[0].Target = hwnd;
            Enabled = RegisterRawInputDevices(devices, 1, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE)));
            lastTick = 0;
            EdgeOnCount = 0;
            EdgeOffCount = 0;
            return Enabled;
        }

        internal static void Stop()
        {
            if (Enabled)
            {
                RAWINPUTDEVICE[] devices = new RAWINPUTDEVICE[1];
                devices[0].UsagePage = 1;
                devices[0].Usage = 2;
                devices[0].Flags = 0x00000001; // RIDEV_REMOVE
                devices[0].Target = IntPtr.Zero;
                RegisterRawInputDevices(devices, 1, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE)));
            }
            Enabled = false;
        }

        internal static void OnInput()
        {
            if (!Enabled) return;

            // Raw input can arrive at several hundred hertz. Eight milliseconds
            // still catches an edge arrival well before the eye notices.
            int now = Environment.TickCount;
            if (now - lastTick < 8) return;
            lastTick = now;

            POINT point;
            if (!GetCursorPos(out point)) return;
            X = point.X;
            Y = point.Y;

            bool wasEdge = TaskbarTakeover.PointerOnEdge;
            TaskbarTakeover.SetPointer(point.X, point.Y);
            if (TaskbarTakeover.PointerOnEdge && !wasEdge) EdgeOnCount++;
            else if (!TaskbarTakeover.PointerOnEdge && wasEdge) EdgeOffCount++;

            // Tick on every throttled input: the region builder dedups via its
            // signature, so this stays cheap and stays event-driven across
            // monitors and dock edges.
            TaskbarTakeover.Tick();
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X, Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTDEVICE
        {
            public ushort UsagePage;
            public ushort Usage;
            public uint Flags;
            public IntPtr Target;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] devices, uint count, uint size);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT point);
    }
}
