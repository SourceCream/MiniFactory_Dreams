using MiniFactory.Economy;
using NUnit.Framework;

namespace MiniFactory.Tests
{
    public sealed class FactoryProgressTests
    {
        [Test]
        public void RoundTrip_RestoresFractionalBalanceAndMachineLevels()
        {
            var original = CreateFactory(100.125);
            original.TryUnlock("assembler");
            original.TryUpgrade("press");
            var restored = CreateFactory();

            Assert.That(restored.TryRestoreProgress(original.CaptureProgress()), Is.True);
            Assert.That(restored.Balance, Is.EqualTo(40.125));
            Assert.That(restored.GetMachine("press").Level, Is.EqualTo(2));
            Assert.That(restored.GetMachine("assembler").Level, Is.EqualTo(1));
            Assert.That(restored.GetMachine("packer").Level, Is.Zero);
            Assert.That(restored.TotalProductionPerSecond, Is.EqualTo(7));
            Assert.That(restored.GetMachine("press").UpgradeCost, Is.EqualTo(15));
        }

        [Test]
        public void SnapshotAndRestoredSession_DoNotShareMutableProgress()
        {
            var original = CreateFactory(100);
            FactoryProgress progress = original.CaptureProgress();
            var restored = CreateFactory();
            restored.TryRestoreProgress(progress);
            progress.Balance = 999;
            progress.Machines[0].Level = 99;
            original.TryUpgrade("press");

            Assert.That(restored.Balance, Is.EqualTo(100));
            Assert.That(restored.GetMachine("press").Level, Is.EqualTo(1));
            Assert.That(original.GetMachine("press").Level, Is.EqualTo(2));
        }

        [Test]
        public void Restore_MatchesIdsAndUsesCurrentTuningForReorderedAndNewMachines()
        {
            var original = CreateFactory(100);
            original.TryUnlock("assembler");
            original.TryUpgrade("press");
            var changedConfig = new FactoryEconomy(new[]
            {
                new MachineDefinition("packer", "Packer", 250, 20, 200, 1.6),
                new MachineDefinition("press", "Renamed press", 0, 3, 20, 2),
                new MachineDefinition("new-machine", "New machine", 500, 50, 500, 2)
            });

            Assert.That(changedConfig.TryRestoreProgress(original.CaptureProgress()), Is.True);
            Assert.That(changedConfig.Balance, Is.EqualTo(40));
            Assert.That(changedConfig.GetMachine("press").Level, Is.EqualTo(2));
            Assert.That(changedConfig.GetMachine("press").UpgradeCost, Is.EqualTo(40));
            Assert.That(changedConfig.GetMachine("packer").Level, Is.Zero);
            Assert.That(changedConfig.GetMachine("new-machine").Level, Is.Zero);
            Assert.That(changedConfig.TotalProductionPerSecond, Is.EqualTo(6));
        }

        [TestCase(-1)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void InvalidBalance_DoesNotPartiallyRestore(double balance)
        {
            var target = CreateFactory(25);
            var progress = CreateFactory(100).CaptureProgress();
            progress.Balance = balance;
            progress.Machines[0].Level = 7;
            AssertRejectedWithoutChanges(target, progress);
        }

        [TestCase("negative-level")]
        [TestCase("duplicate-id")]
        [TestCase("missing-id")]
        [TestCase("null-machine")]
        [TestCase("all-locked")]
        [TestCase("missing-machines")]
        [TestCase("empty-machines")]
        public void InvalidMachines_DoNotPartiallyRestore(string corruption)
        {
            var target = CreateFactory(25);
            var progress = CreateFactory(100).CaptureProgress();
            progress.Machines[0].Level = 7;
            switch (corruption)
            {
                case "negative-level": progress.Machines[2].Level = -1; break;
                case "duplicate-id": progress.Machines[2].Id = "press"; break;
                case "missing-id": progress.Machines[2].Id = " "; break;
                case "null-machine": progress.Machines[2] = null; break;
                case "all-locked": progress.Machines[0].Level = 0; break;
                case "missing-machines": progress.Machines = null; break;
                case "empty-machines": progress.Machines = new MachineProgress[0]; break;
            }
            AssertRejectedWithoutChanges(target, progress);
        }

        [Test]
        public void Restore_ProductionOverflow_LeavesSessionUnchanged()
        {
            var target = new FactoryEconomy(new[]
            {
                new MachineDefinition("press", "Press", 0, double.MaxValue, 10, 1.5)
            }, 25);
            var progress = target.CaptureProgress();
            progress.Machines[0].Level = 2;
            AssertRejectedWithoutChanges(target, progress);
        }

        private static void AssertRejectedWithoutChanges(FactoryEconomy target, FactoryProgress progress)
        {
            Assert.That(target.TryRestoreProgress(progress), Is.False);
            Assert.That(target.Balance, Is.EqualTo(25));
            Assert.That(target.Machines[0].Level, Is.EqualTo(1));
            for (int i = 1; i < target.Machines.Count; i++)
                Assert.That(target.Machines[i].Level, Is.Zero);
        }

        private static FactoryEconomy CreateFactory(double balance = 0) => new FactoryEconomy(new[]
        {
            new MachineDefinition("press", "Press", 0, 1, 10, 1.5),
            new MachineDefinition("assembler", "Assembler", 50, 5, 50, 1.5),
            new MachineDefinition("packer", "Packer", 250, 20, 200, 1.6)
        }, balance);
    }
}
