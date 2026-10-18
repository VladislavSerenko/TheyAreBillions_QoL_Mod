using System;

namespace TABQoL
{
    // Uses elapsed real play time, independent of the simulation multiplier.
    public sealed class AutosaveSchedule
    {
        private double? last;
        private double elapsed;
        private int interval = -1;

        public void Reset() { last = null; elapsed = 0; }

        public bool Tick(double seconds, bool eligible, int minutes)
        {
            if (minutes != interval) { Reset(); interval = minutes; }
            double delta = last.HasValue ? seconds - last.Value : 0;
            last = seconds;
            // Loading, suspended processes and long stalls do not create catch-up saves.
            if (eligible && minutes > 0 && delta > 0 && delta <= 5) elapsed += delta;
            return eligible && minutes > 0 && elapsed >= minutes * 60d;
        }

        public void Completed() { elapsed = 0; }
        public void Failed() { elapsed = Math.Max(0, interval * 60d - 60); }
    }
}
