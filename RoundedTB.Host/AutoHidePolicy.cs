using System;

namespace TrayProbe
{
    /// <summary>
    /// Per-taskbar capture state for the auto-hide alignment (D4/D5). Kept as a
    /// reference type so the pure policy can mutate it in place.
    /// </summary>
    internal sealed class AutoHideRecord
    {
        internal bool Captured;
        internal bool OriginalAutoHide;
        internal bool ChangedByUs;
    }

    internal struct AutoHideAction
    {
        internal bool Change;   // call SetTaskbarState
        internal bool AutoHide; // target state when Change is true
    }

    /// <summary>
    /// Pure mirror of the WPF AutoHide(...) decision, one taskbar at a time.
    /// No OS calls; exercised by <see cref="AutoHidePolicySelfTest"/>.
    /// </summary>
    internal static class AutoHidePolicy
    {
        internal static AutoHideAction RestoreForExit(bool? savedWindowsAutoHide,
            bool observedAutoHide, AutoHideRecord record)
        {
            // WPF calls AutoHide(false) during shutdown: follow the current
            // saved Windows preference if readable; otherwise use the original
            // state captured immediately before Always hide changed Explorer.
            return Evaluate(false, savedWindowsAutoHide, observedAutoHide, record);
        }

        /// <summary>
        /// Whether the setting means "Always hide" (force auto-hide).
        /// AutoHide == 0 is "Always show" (follow the Windows setting).
        /// </summary>
        internal static bool WantsAutoHide(int autoHideSetting, bool enabled)
        {
            return enabled && autoHideSetting > 0;
        }

        /// <param name="wantAutoHide">Always hide selected.</param>
        /// <param name="savedWindowsAutoHide">StuckRects3 Settings[8] &amp; 0x02, or null if unreadable.</param>
        /// <param name="observedAutoHide">Current appbar AutoHide bit.</param>
        internal static AutoHideAction Evaluate(bool wantAutoHide, bool? savedWindowsAutoHide,
            bool observedAutoHide, AutoHideRecord record)
        {
            AutoHideAction action = new AutoHideAction();

            if (wantAutoHide)
            {
                if (!record.Captured)
                {
                    // Capture immediately before our first change, not at
                    // startup when the shell may not have applied its setting.
                    record.Captured = true;
                    record.OriginalAutoHide = observedAutoHide;
                }
                if (!observedAutoHide)
                {
                    action.Change = true;
                    action.AutoHide = true;
                }
                record.ChangedByUs = true;
            }
            else if (savedWindowsAutoHide.HasValue)
            {
                // Always show: make the shell match the Windows setting. This
                // also repairs a state left behind by an older build.
                if (observedAutoHide != savedWindowsAutoHide.Value)
                {
                    action.Change = true;
                    action.AutoHide = savedWindowsAutoHide.Value;
                }
            }
            else if (record.ChangedByUs && record.Captured)
            {
                // Registry unavailable: restore what Windows had before our
                // Always hide, rather than leaving it forced.
                if (observedAutoHide != record.OriginalAutoHide)
                {
                    action.Change = true;
                    action.AutoHide = record.OriginalAutoHide;
                }
            }

            if (!wantAutoHide)
            {
                // Re-read the Windows state next time Always hide is selected,
                // in case the user changed the setting in the meantime.
                record.ChangedByUs = false;
                record.Captured = false;
            }

            return action;
        }
    }

    /// <summary>
    /// Coalescing timer: a burst of events pushes the due time forward, so the
    /// caller acts once after the burst goes quiet (D5's ~1 s debounce).
    /// </summary>
    internal sealed class Debouncer
    {
        private bool pending;
        private DateTime dueUtc;

        internal bool Pending { get { return pending; } }

        internal void Request(DateTime nowUtc, int delayMs)
        {
            pending = true;
            dueUtc = nowUtc.AddMilliseconds(delayMs);
        }

        internal bool TryFire(DateTime nowUtc)
        {
            if (!pending || nowUtc < dueUtc) return false;
            pending = false;
            return true;
        }
    }
}
