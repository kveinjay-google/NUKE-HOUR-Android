using System;

namespace OpenRA.Mods.RA2.Content
{
    public static class ImportProgressEstimate
    {
        // A phase-local estimate: scanning and future map conversion cannot be
        // predicted from file-copy speed. Wait for a useful timing sample.
        public static double? RemainingSeconds(long done, long total, double elapsedSeconds)
        {
            if (total <= 0 || done <= 0 || elapsedSeconds < 1 || !double.IsFinite(elapsedSeconds))
                return null;
            if (done >= total)
                return 0;
            return Math.Min(86400, elapsedSeconds * ((double)total - done) / done);
        }
    }
}
