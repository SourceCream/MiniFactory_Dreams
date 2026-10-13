using System;

namespace MiniFactory.Economy
{
    /// <summary>Tracks the UTC boundary already included in the balance, independently of Unity's frame clock.</summary>
    public sealed class OfflineIncomeTracker
    {
        public long AccountedThroughUtcTicks { get; private set; }

        public OfflineIncomeTracker(long savedAtUtcTicks = 0)
        {
            ValidateTime(savedAtUtcTicks);
            AccountedThroughUtcTicks = savedAtUtcTicks;
        }

        public void MarkActive(long utcTicks)
        {
            ValidateTime(utcTicks);
            // Moving the device clock backwards must not reopen an already credited interval.
            AccountedThroughUtcTicks = Math.Max(AccountedThroughUtcTicks, utcTicks);
        }

        public OfflineIncome Resume(FactoryEconomy economy, long utcTicks, double maximumSeconds)
        {
            if (economy == null) throw new ArgumentNullException(nameof(economy));
            ValidateTime(utcTicks);
            if (double.IsNaN(maximumSeconds) || double.IsInfinity(maximumSeconds) || maximumSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(maximumSeconds));

            double away = AccountedThroughUtcTicks == 0 ? 0 :
                Math.Max(0, (utcTicks - AccountedThroughUtcTicks) / (double)TimeSpan.TicksPerSecond);
            double credited = Math.Min(away, maximumSeconds);
            long creditedTicks = Math.Min(Math.Max(0, utcTicks - AccountedThroughUtcTicks),
                (long)Math.Min(credited * TimeSpan.TicksPerSecond, DateTime.MaxValue.Ticks));
            double coins = AccountedThroughUtcTicks == 0 || utcTicks < AccountedThroughUtcTicks ? 0 :
                economy.AdvanceBetween(AccountedThroughUtcTicks, AccountedThroughUtcTicks + creditedTicks);
            // Consume the entire absence, including time beyond the cap, exactly once.
            MarkActive(utcTicks);
            economy.Boost.FinishIfExpired(AccountedThroughUtcTicks);
            return new OfflineIncome(away, credited, coins);
        }

        public double AdvanceActive(FactoryEconomy economy, long utcTicks)
        {
            if (economy == null) throw new ArgumentNullException(nameof(economy));
            ValidateTime(utcTicks);
            double earned = AccountedThroughUtcTicks == 0 || utcTicks < AccountedThroughUtcTicks ? 0 :
                economy.AdvanceBetween(AccountedThroughUtcTicks, utcTicks);
            MarkActive(utcTicks);
            economy.Boost.FinishIfExpired(AccountedThroughUtcTicks);
            return earned;
        }

        private static void ValidateTime(long utcTicks)
        {
            if (utcTicks < 0 || utcTicks > DateTime.MaxValue.Ticks)
                throw new ArgumentOutOfRangeException(nameof(utcTicks));
        }
    }
}
