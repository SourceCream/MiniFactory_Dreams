using System;

namespace MiniFactory.Economy
{
    /// <summary>A real-time production interval. The caller supplies UTC; no engine or storage APIs are used.</summary>
    public sealed class ProductionBoost
    {
        public BoostDefinition Definition { get; }
        public long StartedAtUtcTicks { get; private set; }
        public long EndsAtUtcTicks { get; private set; }
        public double ActiveMultiplier { get; private set; } = 1;

        public ProductionBoost(BoostDefinition definition) => Definition = definition ?? BoostDefinition.Disabled;

        public bool IsActive(long utcTicks) => Definition.Enabled && EndsAtUtcTicks > utcTicks;

        public double RemainingSeconds(long utcTicks) => IsActive(utcTicks)
            ? (EndsAtUtcTicks - Math.Max(StartedAtUtcTicks, utcTicks)) / (double)TimeSpan.TicksPerSecond : 0;

        public double MultiplierAt(long utcTicks) => IsActive(utcTicks) ? ActiveMultiplier : 1;

        internal bool TryActivate(long utcTicks)
        {
            ValidateTime(utcTicks);
            if (!Definition.Enabled || IsActive(utcTicks) || utcTicks == 0 ||
                Definition.DurationTicks > DateTime.MaxValue.Ticks - utcTicks)
                return false;
            StartedAtUtcTicks = utcTicks;
            EndsAtUtcTicks = utcTicks + Definition.DurationTicks;
            ActiveMultiplier = Definition.Multiplier;
            return true;
        }

        internal double EffectiveProductionSeconds(long fromUtcTicks, long toUtcTicks)
        {
            ValidateTime(fromUtcTicks);
            ValidateTime(toUtcTicks);
            if (toUtcTicks < fromUtcTicks) throw new ArgumentOutOfRangeException(nameof(toUtcTicks));
            double elapsed = (toUtcTicks - fromUtcTicks) / (double)TimeSpan.TicksPerSecond;
            if (!Definition.Enabled || EndsAtUtcTicks == 0) return elapsed;
            long overlap = Math.Max(0, Math.Min(toUtcTicks, EndsAtUtcTicks) - Math.Max(fromUtcTicks, StartedAtUtcTicks));
            return elapsed + overlap / (double)TimeSpan.TicksPerSecond * (ActiveMultiplier - 1);
        }

        public bool FinishIfExpired(long utcTicks)
        {
            if (EndsAtUtcTicks == 0 || utcTicks < EndsAtUtcTicks) return false;
            Restore(null);
            return true;
        }

        internal BoostProgress Capture() => new BoostProgress
        {
            StartedAtUtcTicks = StartedAtUtcTicks,
            EndsAtUtcTicks = EndsAtUtcTicks,
            Multiplier = ActiveMultiplier
        };

        internal static bool IsValid(BoostProgress progress)
        {
            if (progress == null) return true;
            if (progress.StartedAtUtcTicks == 0 && progress.EndsAtUtcTicks == 0)
                return progress.Multiplier == 1;
            return progress.StartedAtUtcTicks > 0 && progress.EndsAtUtcTicks > progress.StartedAtUtcTicks &&
                progress.EndsAtUtcTicks <= DateTime.MaxValue.Ticks && !double.IsNaN(progress.Multiplier) &&
                !double.IsInfinity(progress.Multiplier) && progress.Multiplier > 1;
        }

        internal void Restore(BoostProgress progress)
        {
            StartedAtUtcTicks = Definition.Enabled && progress != null ? progress.StartedAtUtcTicks : 0;
            EndsAtUtcTicks = Definition.Enabled && progress != null ? progress.EndsAtUtcTicks : 0;
            ActiveMultiplier = Definition.Enabled && progress != null ? progress.Multiplier : 1;
        }

        private static void ValidateTime(long utcTicks)
        {
            if (utcTicks < 0 || utcTicks > DateTime.MaxValue.Ticks)
                throw new ArgumentOutOfRangeException(nameof(utcTicks));
        }
    }
}
