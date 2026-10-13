using System;

namespace MiniFactory.Economy
{
    public sealed class BoostDefinition
    {
        public static readonly BoostDefinition Disabled = new BoostDefinition(false, 0, 1);
        public bool Enabled { get; }
        public double DurationSeconds { get; }
        public double Multiplier { get; }
        internal long DurationTicks { get; }

        public BoostDefinition(bool enabled, double durationSeconds, double multiplier)
        {
            if (double.IsNaN(durationSeconds) || double.IsInfinity(durationSeconds) || durationSeconds < 0 ||
                durationSeconds >= DateTime.MaxValue.Ticks / (double)TimeSpan.TicksPerSecond)
                throw new ArgumentOutOfRangeException(nameof(durationSeconds));
            if (double.IsNaN(multiplier) || double.IsInfinity(multiplier) || multiplier < 1)
                throw new ArgumentOutOfRangeException(nameof(multiplier));
            DurationTicks = (long)Math.Round(durationSeconds * TimeSpan.TicksPerSecond);
            if (enabled && (DurationTicks < 1 || multiplier <= 1))
                throw new ArgumentException("An enabled boost needs a positive duration and a multiplier greater than one.");
            Enabled = enabled;
            DurationSeconds = durationSeconds;
            Multiplier = multiplier;
        }
    }
}
