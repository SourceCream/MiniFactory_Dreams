using System;
using MiniFactory.Economy;
using NUnit.Framework;

namespace MiniFactory.Tests
{
    public sealed class FactoryEconomyTests
    {
        [Test]
        public void NewFactory_OnlyFirstMachineIsUnlocked()
        {
            var economy = CreateFactory();

            Assert.That(economy.Balance, Is.Zero);
            Assert.That(economy.GetMachine("press").Level, Is.EqualTo(1));
            Assert.That(economy.GetMachine("assembler").IsUnlocked, Is.False);
            Assert.That(economy.GetMachine("packer").Level, Is.Zero);
            Assert.That(economy.TotalProductionPerSecond, Is.EqualTo(1));
        }

        [Test]
        public void Advance_PreservesFractionalIncomeAcrossSmallSteps()
        {
            var smallSteps = CreateFactory();
            var oneStep = CreateFactory();

            for (int i = 0; i < 100; i++)
                smallSteps.Advance(0.01);
            oneStep.Advance(1);

            Assert.That(smallSteps.Balance, Is.EqualTo(1).Within(1e-10));
            Assert.That(smallSteps.Balance, Is.EqualTo(oneStep.Balance).Within(1e-10));
        }

        [Test]
        public void Unlock_WithExactBalance_SpendsCostAndStartsProduction()
        {
            var economy = CreateFactory(50);

            Assert.That(economy.TryUnlock("assembler"), Is.True);
            Assert.That(economy.Balance, Is.Zero);
            Assert.That(economy.GetMachine("assembler").Level, Is.EqualTo(1));
            Assert.That(economy.TotalProductionPerSecond, Is.EqualTo(6));
            Assert.That(economy.Advance(2), Is.EqualTo(12));
            Assert.That(economy.Balance, Is.EqualTo(12));
        }

        [Test]
        public void Unlock_WhenUnaffordable_LeavesBalanceAndStateUnchanged()
        {
            var economy = CreateFactory(49.99);

            Assert.That(economy.TryUnlock("assembler"), Is.False);
            Assert.That(economy.Balance, Is.EqualTo(49.99));
            Assert.That(economy.GetMachine("assembler").IsUnlocked, Is.False);
            Assert.That(economy.TotalProductionPerSecond, Is.EqualTo(1));
        }

        [Test]
        public void Unlock_AlreadyUnlockedMachine_DoesNotSpendAgain()
        {
            var economy = CreateFactory(100);
            economy.TryUnlock("assembler");

            Assert.That(economy.TryUnlock("assembler"), Is.False);
            Assert.That(economy.Balance, Is.EqualTo(50));
            Assert.That(economy.GetMachine("assembler").Level, Is.EqualTo(1));
        }

        [Test]
        public void Upgrade_SpendsCurrentPriceAndRoundsNextPriceUp()
        {
            var economy = CreateFactory(25);

            Assert.That(economy.TryUpgrade("press"), Is.True);
            Assert.That(economy.Balance, Is.EqualTo(15));
            Assert.That(economy.GetMachine("press").UpgradeCost, Is.EqualTo(15));
            Assert.That(economy.TryUpgrade("press"), Is.True);
            Assert.That(economy.Balance, Is.Zero);
            Assert.That(economy.GetMachine("press").Level, Is.EqualTo(3));
            Assert.That(economy.GetMachine("press").UpgradeCost, Is.EqualTo(23));
            Assert.That(economy.TotalProductionPerSecond, Is.EqualTo(3));
        }

        [Test]
        public void Upgrade_WhenUnaffordable_DoesNotChangeProgress()
        {
            var economy = CreateFactory(9.99);

            Assert.That(economy.TryUpgrade("press"), Is.False);
            Assert.That(economy.Balance, Is.EqualTo(9.99));
            Assert.That(economy.GetMachine("press").Level, Is.EqualTo(1));
            Assert.That(economy.GetMachine("press").UpgradeCost, Is.EqualTo(10));
        }

        [Test]
        public void Upgrade_LockedMachine_DoesNotBypassUnlock()
        {
            var economy = CreateFactory(1000);

            Assert.That(economy.TryUpgrade("assembler"), Is.False);
            Assert.That(economy.Balance, Is.EqualTo(1000));
            Assert.That(economy.GetMachine("assembler").Level, Is.Zero);
        }

        [TestCase(-1)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void Advance_InvalidDuration_LeavesBalanceUnchanged(double duration)
        {
            var economy = CreateFactory(12);

            Assert.Throws<ArgumentOutOfRangeException>(() => economy.Advance(duration));
            Assert.That(economy.Balance, Is.EqualTo(12));
        }

        [Test]
        public void Constructor_DuplicateMachineIds_RejectsAmbiguousConfiguration()
        {
            var machine = new MachineDefinition("press", "Press", 0, 1, 10, 1.5);

            Assert.Throws<ArgumentException>(() => new FactoryEconomy(new[] { machine, machine }));
        }

        [Test]
        public void SeparateFactories_DoNotSharePlayerProgress()
        {
            var definition = new MachineDefinition("press", "Press", 0, 1, 10, 1.5);
            var first = new FactoryEconomy(new[] { definition }, 10);
            var second = new FactoryEconomy(new[] { definition });

            first.TryUpgrade("press");
            first.Advance(5);

            Assert.That(second.GetMachine("press").Level, Is.EqualTo(1));
            Assert.That(second.Balance, Is.Zero);
        }

        private static FactoryEconomy CreateFactory(double balance = 0)
        {
            return new FactoryEconomy(new[]
            {
                new MachineDefinition("press", "Press", 0, 1, 10, 1.5),
                new MachineDefinition("assembler", "Assembler", 50, 5, 50, 1.5),
                new MachineDefinition("packer", "Packer", 250, 20, 200, 1.6)
            }, balance);
        }
    }
}
