using System;

namespace TrayProbe
{
    internal static class AutoHidePolicySelfTest
    {
        internal static string Run()
        {
            // Always show: follow the saved Windows setting.
            var record = new AutoHideRecord();
            AutoHideAction action = AutoHidePolicy.Evaluate(false, true, false, record);
            Require(action.Change && action.AutoHide, "follow saved: observed off -> enable");

            // Idempotent: already matches, so no shell call at all.
            action = AutoHidePolicy.Evaluate(false, true, true, record);
            Require(!action.Change, "follow saved: already on -> no-op");

            action = AutoHidePolicy.Evaluate(false, false, true, record);
            Require(action.Change && !action.AutoHide, "follow saved: observed on -> disable");

            // Registry unavailable and we never changed it: leave alone.
            record = new AutoHideRecord();
            action = AutoHidePolicy.Evaluate(false, null, true, record);
            Require(!action.Change, "registry unavailable, untouched -> no-op");

            // Always hide: capture the original (off) and enable.
            record = new AutoHideRecord();
            action = AutoHidePolicy.Evaluate(true, null, false, record);
            Require(action.Change && action.AutoHide && record.Captured && !record.OriginalAutoHide && record.ChangedByUs,
                "always hide: capture off and enable");

            // Capture happens only once; already-on is a no-op.
            action = AutoHidePolicy.Evaluate(true, null, true, record);
            Require(!action.Change && record.Captured && !record.OriginalAutoHide, "always hide: capture stable");

            // Switching to Always show with the registry unavailable restores
            // the captured original, then forgets it.
            action = AutoHidePolicy.Evaluate(false, null, true, record);
            Require(action.Change && !action.AutoHide, "unavailable: restore captured original off");
            Require(!record.Captured && !record.ChangedByUs, "record reset after restore");

            // Exit restore prefers the live saved Windows setting; without a
            // readable value it restores the state captured before forcing.
            record = new AutoHideRecord();
            AutoHidePolicy.Evaluate(true, null, false, record); // capture original off and force on
            action = AutoHidePolicy.RestoreForExit(null, true, record);
            Require(action.Change && !action.AutoHide, "exit restores captured original off when registry unavailable");
            Require(!record.Captured && !record.ChangedByUs, "exit clears captured state after restore");
            record = new AutoHideRecord();
            AutoHidePolicy.Evaluate(true, null, true, record); // capture original on
            action = AutoHidePolicy.RestoreForExit(null, false, record);
            Require(action.Change && action.AutoHide, "exit restores captured original on when registry unavailable");

            record = new AutoHideRecord();
            AutoHidePolicy.Evaluate(true, null, false, record); // capture off and force on
            action = AutoHidePolicy.RestoreForExit(true, true, record);
            Require(!action.Change, "exit keeps auto-hide when saved Windows preference is on");
            record = new AutoHideRecord();
            AutoHidePolicy.Evaluate(true, null, true, record); // capture on
            action = AutoHidePolicy.RestoreForExit(false, true, record);
            Require(action.Change && !action.AutoHide, "exit follows saved Windows preference when off");

            // Debounce coalesces a burst into one firing.
            var debounce = new Debouncer();
            DateTime t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            debounce.Request(t0, 1000);
            Require(debounce.Pending && !debounce.TryFire(t0.AddMilliseconds(500)), "debounce not due yet");
            debounce.Request(t0.AddMilliseconds(800), 1000);
            Require(!debounce.TryFire(t0.AddMilliseconds(1000)), "debounce extended by the burst");
            Require(debounce.TryFire(t0.AddMilliseconds(1800)), "debounce fires after the quiet period");
            Require(!debounce.TryFire(t0.AddMilliseconds(2000)), "debounce fires only once");
            Require(!debounce.Pending, "debounce clears pending after firing");

            return "PASS: auto-hide alignment (follow/force/capture/restore/idempotent) and debounce coalescing";
        }

        private static void Require(bool condition, string check)
        {
            if (!condition) throw new InvalidOperationException(check);
        }
    }
}
