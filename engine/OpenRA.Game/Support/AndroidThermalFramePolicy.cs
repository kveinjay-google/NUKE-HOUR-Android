using System;

namespace OpenRA.Support
{
    // Android thermal statuses are stable platform values 0..6. -1 means unavailable.
    // All limits are transient render limits; simulation and saved settings are untouched.
    public sealed class AndroidThermalFramePolicy
    {
        public const long RecoveryDelayMilliseconds = 60000;
        int currentCap;
        int pendingCap = -1;
        long coolingSince;

        public int Update(int thermalStatus, long now)
        {
            if (thermalStatus < 0 || thermalStatus > 6)
            {
                pendingCap = -1;
                return currentCap;
            }

            var requested = thermalStatus >= 4 ? 20 : thermalStatus >= 3 ? 30 : thermalStatus >= 2 ? 60 : 0;
            if (requested == currentCap || (requested != 0 && (currentCap == 0 || requested < currentCap)))
            {
                currentCap = requested;
                pendingCap = -1;
            }
            else
            {
                if (pendingCap != requested || now < coolingSince)
                {
                    pendingCap = requested;
                    coolingSince = now;
                }
                else if (now - coolingSince >= RecoveryDelayMilliseconds)
                {
                    currentCap = requested;
                    pendingCap = -1;
                }
            }

            return currentCap;
        }

        // A capped renderer must not become a prerequisite for the next simulation tick.
        public static bool RequiresRenderBeforeLogic(bool requested, int cap) => requested && (cap <= 0 || cap > 1000);

        public static int LimitRenderInterval(int interval, int cap)
        {
            if (cap <= 0 || cap > 1000) return interval;
            return Math.Max(interval, (1000 + cap - 1) / cap);
        }
    }
}
