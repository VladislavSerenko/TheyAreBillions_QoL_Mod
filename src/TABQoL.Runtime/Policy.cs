using System;
using System.Collections.Generic;
using System.Linq;

namespace TABQoL
{
    public static class Policy
    {
        public static readonly double[] Speeds = { 0.5, 1, 2, 3, 4, 5 };

        public static double StepSpeed(double current, int direction)
        {
            if (direction > 0) return Speeds.FirstOrDefault(x => x > current + 0.001) is double next && next != 0 ? next : Speeds[Speeds.Length - 1];
            return Speeds.LastOrDefault(x => x < current - 0.001) is double previous && previous != 0 ? previous : Speeds[0];
        }

        public static bool ValidSpeed(double speed) { return Speeds.Contains(speed); }
        public static int GridKey(int position) { return position >= 1 && position <= 15 ? "QWERTASDFGZXCVB"[position - 1] : 0; }

        public static int Refund(IEnumerable<string> ids, IDictionary<string, int> costs)
        {
            return checked(ids.Distinct(StringComparer.Ordinal).Sum(id => costs[id]));
        }

        public static int RewardDelta(bool completed, int reward, int collected)
        {
            if (reward < 0 || collected < 0) throw new ArgumentOutOfRangeException();
            return completed ? 0 : checked(reward + collected);
        }
    }
}
