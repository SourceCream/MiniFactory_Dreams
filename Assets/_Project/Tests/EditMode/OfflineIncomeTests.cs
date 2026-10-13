using System;
using MiniFactory.Economy;
using MiniFactory.Presentation;
using NUnit.Framework;

namespace MiniFactory.Tests
{
    public sealed class OfflineIncomeTests
    {
        private static readonly long Start = new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc).Ticks;

        [Test]
        public void Resume_UsesUnlockedProductionAndPreservesFractionalSeconds()
        {
            var economy = CreateFactory(100);
            economy.TryUpgrade("press");
            economy.TryUnlock("assembler");
            var tracker = new OfflineIncomeTracker(Start);

            OfflineIncome income = tracker.Resume(economy, Start + TimeSpan.FromSeconds(12.5).Ticks, 28800);

            Assert.That(income.AwaySeconds, Is.EqualTo(12.5));
            Assert.That(income.CreditedSeconds, Is.EqualTo(12.5));
            Assert.That(income.Coins, Is.EqualTo(87.5));
            Assert.That(income.IsCapped, Is.False);
            Assert.That(economy.Balance, Is.EqualTo(127.5));
        }

        [Test]
        public void Cap_ConsumesWholeAbsenceAndCannotBeClaimedAgain()
        {
            var economy = CreateFactory();
            var tracker = new OfflineIncomeTracker(Start);
            long returned = Start + TimeSpan.FromDays(2).Ticks;

            OfflineIncome income = tracker.Resume(economy, returned, 28800);
            OfflineIncome duplicate = tracker.Resume(economy, returned, 28800);

            Assert.That(income.AwaySeconds, Is.EqualTo(172800));
            Assert.That(income.CreditedSeconds, Is.EqualTo(28800));
            Assert.That(income.Coins, Is.EqualTo(28800));
            Assert.That(income.IsCapped, Is.True);
            Assert.That(duplicate.Coins, Is.Zero);
            Assert.That(tracker.AccountedThroughUtcTicks, Is.EqualTo(returned));
            Assert.That(economy.Balance, Is.EqualTo(28800));
        }

        [Test]
        public void MissingTimestamp_StartsTrackingWithoutInventingIncome()
        {
            var economy = CreateFactory(12);
            var tracker = new OfflineIncomeTracker();

            Assert.That(tracker.Resume(economy, Start, 28800).AwaySeconds, Is.Zero);
            Assert.That(economy.Balance, Is.EqualTo(12));
            Assert.That(tracker.AccountedThroughUtcTicks, Is.EqualTo(Start));
        }

        [Test]
        public void ClockMovedBackwards_DoesNotReopenCreditedTime()
        {
            var economy = CreateFactory();
            var tracker = new OfflineIncomeTracker(Start);
            long earlier = Start - TimeSpan.FromHours(1).Ticks;

            Assert.That(tracker.Resume(economy, earlier, 28800).Coins, Is.Zero);
            tracker.MarkActive(earlier);
            Assert.That(tracker.Resume(economy, Start, 28800).Coins, Is.Zero);
            Assert.That(tracker.Resume(economy, Start + TimeSpan.FromSeconds(5).Ticks, 28800).Coins, Is.EqualTo(5));
            Assert.That(economy.Balance, Is.EqualTo(5));
        }

        [Test]
        public void MarkActive_ExcludesTimeAlreadyProducedInForeground()
        {
            var economy = CreateFactory();
            var tracker = new OfflineIncomeTracker(Start);
            economy.Advance(10);
            tracker.MarkActive(Start + TimeSpan.FromSeconds(10).Ticks);

            Assert.That(tracker.Resume(economy, Start + TimeSpan.FromSeconds(15).Ticks, 28800).Coins, Is.EqualTo(5));
            Assert.That(economy.Balance, Is.EqualTo(15));
        }

        [Test]
        public void ZeroCap_ConsumesAbsenceWithoutIncome()
        {
            var economy = CreateFactory();
            var tracker = new OfflineIncomeTracker(Start);
            long returned = Start + TimeSpan.FromHours(1).Ticks;

            Assert.That(tracker.Resume(economy, returned, 0).Coins, Is.Zero);
            Assert.That(tracker.Resume(economy, returned, 28800).Coins, Is.Zero);
        }

        [TestCase(-1)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void InvalidCap_ChangesNeitherBalanceNorClock(double cap)
        {
            var economy = CreateFactory(12);
            var tracker = new OfflineIncomeTracker(Start);
            Assert.Throws<ArgumentOutOfRangeException>(() => tracker.Resume(economy, Start + TimeSpan.TicksPerSecond, cap));
            Assert.That(economy.Balance, Is.EqualTo(12));
            Assert.That(tracker.AccountedThroughUtcTicks, Is.EqualTo(Start));
        }

        [TestCase(45.9, "45 с")]
        [TestCase(125, "2 мин 5 с")]
        [TestCase(7380, "2 ч 3 мин")]
        [TestCase(176400, "2 д 1 ч")]
        public void PopupDuration_UsesReadableUnits(double seconds, string expected)
        {
            Assert.That(OfflineIncomePopup.FormatDuration(seconds), Is.EqualTo(expected));
        }

        private static FactoryEconomy CreateFactory(double balance = 0) => new FactoryEconomy(new[]
        {
            new MachineDefinition("press", "Press", 0, 1, 10, 1.5),
            new MachineDefinition("assembler", "Assembler", 50, 5, 50, 1.5),
            new MachineDefinition("packer", "Packer", 250, 20, 200, 1.6)
        }, balance);
    }
}
