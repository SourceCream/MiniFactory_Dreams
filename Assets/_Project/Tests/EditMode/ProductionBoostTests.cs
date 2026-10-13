using System;
using MiniFactory.Economy;
using NUnit.Framework;

namespace MiniFactory.Tests
{
    public sealed class ProductionBoostTests
    {
        private static readonly long Start = new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc).Ticks;
        private static long At(double seconds) => Start + (long)(seconds * TimeSpan.TicksPerSecond);

        [Test]
        public void Activation_AppliesToWholeFactoryWithoutSpendingCoinsOrStacking()
        {
            var economy = CreateFactory(100);
            economy.TryUnlock("assembler");
            Assert.That(economy.TryActivateBoost(Start), Is.True);
            Assert.That(economy.ProductionPerSecondAt(Start), Is.EqualTo(12));
            Assert.That(economy.TotalProductionPerSecond, Is.EqualTo(6));
            Assert.That(economy.Balance, Is.EqualTo(50));
            Assert.That(economy.TryActivateBoost(At(10)), Is.False);
            Assert.That(economy.Boost.EndsAtUtcTicks, Is.EqualTo(At(60)));
        }

        [Test]
        public void ForegroundInterval_SplitsAtExpiryAndAllowsNextActivation()
        {
            var economy = CreateFactory();
            economy.TryActivateBoost(Start);
            Assert.That(economy.AdvanceBetween(Start, At(59.5)), Is.EqualTo(119));
            Assert.That(economy.AdvanceBetween(At(59.5), At(61)), Is.EqualTo(2));
            Assert.That(economy.Balance, Is.EqualTo(121));
            Assert.That(economy.Boost.EndsAtUtcTicks, Is.Zero);
            Assert.That(economy.ProductionPerSecondAt(At(61)), Is.EqualTo(1));
            Assert.That(economy.TryActivateBoost(At(61)), Is.True);
            Assert.That(economy.Boost.EndsAtUtcTicks, Is.EqualTo(At(121)));
        }

        [Test]
        public void IncomeBeforeActivation_DoesNotReceiveRetroactiveMultiplier()
        {
            var economy = CreateFactory();
            economy.TryActivateBoost(At(10));
            Assert.That(economy.AdvanceBetween(Start, At(20)), Is.EqualTo(30));
        }

        [TestCase(20, 28800, 40, 40)]
        [TestCase(90, 28800, 150, 0)]
        [TestCase(90, 30, 60, 0)]
        [TestCase(32400, 28800, 28860, 0)]
        public void OfflineResume_CombinesBoostOverlapWithCapAndRealExpiry(double away, double cap, double coins, double remaining)
        {
            var economy = CreateFactory();
            economy.TryActivateBoost(Start);
            var clock = new OfflineIncomeTracker(Start);

            OfflineIncome income = clock.Resume(economy, At(away), cap);

            Assert.That(income.Coins, Is.EqualTo(coins));
            Assert.That(economy.Boost.RemainingSeconds(At(away)), Is.EqualTo(remaining));
            Assert.That(clock.Resume(economy, At(away), cap).Coins, Is.Zero);
        }

        [Test]
        public void Snapshot_RestoresOriginalTermsAndDoesNotShareMutableBoostData()
        {
            var original = CreateFactory();
            original.TryActivateBoost(Start);
            FactoryProgress saved = original.CaptureProgress();
            var updatedConfiguration = CreateFactory(definition: new BoostDefinition(true, 300, 5));
            Assert.That(updatedConfiguration.TryRestoreProgress(saved), Is.True);
            saved.Boost.Multiplier = 9;

            Assert.That(updatedConfiguration.Boost.ActiveMultiplier, Is.EqualTo(2));
            Assert.That(updatedConfiguration.Boost.RemainingSeconds(At(20)), Is.EqualTo(40));
            Assert.That(updatedConfiguration.AdvanceBetween(At(20), At(70)), Is.EqualTo(90));
            Assert.That(updatedConfiguration.TryActivateBoost(At(70)), Is.True);
            Assert.That(updatedConfiguration.Boost.ActiveMultiplier, Is.EqualTo(5));
            Assert.That(updatedConfiguration.Boost.EndsAtUtcTicks, Is.EqualTo(At(370)));
        }

        [Test]
        public void DisabledConfiguration_PreventsActivationAndIgnoresSavedBoost()
        {
            var previous = CreateFactory();
            previous.TryActivateBoost(Start);
            var disabled = CreateFactory(definition: new BoostDefinition(false, 60, 2));
            Assert.That(disabled.TryRestoreProgress(previous.CaptureProgress()), Is.True);
            Assert.That(disabled.TryActivateBoost(Start), Is.False);
            Assert.That(disabled.Boost.IsActive(Start), Is.False);
            Assert.That(disabled.AdvanceBetween(Start, At(10)), Is.EqualTo(10));
        }

        [TestCase("backwards")]
        [TestCase("nan")]
        [TestCase("infinite")]
        public void InvalidBoostSnapshot_DoesNotPartiallyRestore(string corruption)
        {
            var previous = CreateFactory(100);
            previous.TryActivateBoost(Start);
            FactoryProgress saved = previous.CaptureProgress();
            if (corruption == "backwards") saved.Boost.EndsAtUtcTicks = Start - 1;
            if (corruption == "nan") saved.Boost.Multiplier = double.NaN;
            if (corruption == "infinite") saved.Boost.Multiplier = double.PositiveInfinity;
            var target = CreateFactory(12);
            Assert.That(target.TryRestoreProgress(saved), Is.False);
            Assert.That(target.Balance, Is.EqualTo(12));
            Assert.That(target.Boost.EndsAtUtcTicks, Is.Zero);
        }

        [Test]
        public void ClockRollback_DoesNotExtendOrReviveExpiredBoost()
        {
            var economy = CreateFactory();
            economy.TryActivateBoost(Start);
            var clock = new OfflineIncomeTracker(Start);
            clock.AdvanceActive(economy, At(60));
            Assert.That(clock.AdvanceActive(economy, At(20)), Is.Zero);
            Assert.That(economy.Boost.IsActive(At(20)), Is.False);
            Assert.That(clock.AdvanceActive(economy, At(61)), Is.EqualTo(1));
        }

        [TestCase(0, 2)]
        [TestCase(60, 1)]
        [TestCase(double.NaN, 2)]
        [TestCase(60, double.PositiveInfinity)]
        public void InvalidDefinition_IsRejected(double duration, double multiplier)
        {
            Assert.That(() => new BoostDefinition(true, duration, multiplier), Throws.InstanceOf<ArgumentException>());
        }

        private static FactoryEconomy CreateFactory(double balance = 0, BoostDefinition definition = null) =>
            new FactoryEconomy(new[]
            {
                new MachineDefinition("press", "Press", 0, 1, 10, 1.5),
                new MachineDefinition("assembler", "Assembler", 50, 5, 50, 1.5)
            }, balance, definition ?? new BoostDefinition(true, 60, 2));
    }
}
