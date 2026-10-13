using System;
using System.Collections.Generic;
using System.IO;
using MiniFactory.Economy;
using MiniFactory.Persistence;
using NUnit.Framework;

namespace MiniFactory.Tests
{
    public sealed class FactorySaveStoreTests
    {
        private string directory;
        private FactorySaveStore store;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "MiniFactorySaveTests-" + Guid.NewGuid().ToString("N"));
            store = new FactorySaveStore(directory);
        }

        [TearDown]
        public void TearDown()
        {
            if (!Directory.Exists(directory)) return;
            foreach (string file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }

        [Test]
        public void MissingSave_LeavesNewGameUntouched()
        {
            var economy = CreateFactory();
            Assert.That(store.TryLoad(economy), Is.False);
            Assert.That(economy.Balance, Is.Zero);
            Assert.That(economy.Machines[0].Level, Is.EqualTo(1));
            Assert.That(Directory.Exists(directory), Is.False);
        }

        [Test]
        public void NewStoreInstance_RestoresWrittenProgressAndOverwritesWithLatestSave()
        {
            var economy = CreateFactory(100.125);
            Assert.That(store.TrySave(economy), Is.True);
            economy.TryUnlock("assembler");
            economy.TryUpgrade("press");
            Assert.That(store.TrySave(economy), Is.True);
            var restored = CreateFactory();

            Assert.That(new FactorySaveStore(directory).TryLoad(restored), Is.True);
            Assert.That(restored.Balance, Is.EqualTo(40.125));
            Assert.That(restored.Machines[0].Level, Is.EqualTo(2));
            Assert.That(restored.Machines[1].Level, Is.EqualTo(1));
            Assert.That(File.Exists(store.FilePath + ".bak"), Is.True);
            Assert.That(File.Exists(store.FilePath + ".tmp"), Is.False);
        }

        [TestCase("not json")]
        [TestCase("")]
        [TestCase("{}")]
        [TestCase("{\"Version\":1,\"Progress\":{\"Balance\":-5,\"Machines\":[{\"Id\":\"press\",\"Level\":1}]}}")]
        [TestCase("{\"Version\":1,\"Progress\":{\"Machines\":[{\"Id\":\"press\",\"Level\":1}]}}")]
        [TestCase("{\"Version\":1,\"Progress\":{\"Balance\":5,\"Machines\":[{\"Id\":\"press\"}]}}")]
        [TestCase("{\"Version\":2,\"Progress\":{\"Balance\":5,\"Machines\":[{\"Id\":\"press\",\"Level\":1}]}}")]
        public void InvalidSave_LeavesStartingProgressUntouched(string contents)
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(store.FilePath, contents);
            var economy = CreateFactory(12);
            Assert.That(store.TryLoad(economy), Is.False);
            Assert.That(economy.Balance, Is.EqualTo(12));
            Assert.That(economy.Machines[0].Level, Is.EqualTo(1));
            Assert.That(economy.Machines[1].IsUnlocked, Is.False);
        }

        [Test]
        public void DamagedPrimary_RecoversBackupAndKeepsItSafeDuringNextSave()
        {
            var economy = CreateFactory(100);
            store.TrySave(economy);
            economy.TryUpgrade("press");
            store.TrySave(economy);
            File.WriteAllText(store.FilePath, "interrupted or damaged data");
            var recovered = CreateFactory();
            var recoveryStore = new FactorySaveStore(directory);

            Assert.That(recoveryStore.TryLoad(recovered), Is.True);
            Assert.That(recovered.Balance, Is.EqualTo(100));
            Assert.That(recovered.Machines[0].Level, Is.EqualTo(1));
            recovered.TryUnlock("assembler");
            Assert.That(recoveryStore.TrySave(recovered), Is.True);
            File.WriteAllText(store.FilePath, "damaged again");

            Assert.That(new FactorySaveStore(directory).TryLoad(recovered), Is.True);
            Assert.That(recovered.Balance, Is.EqualTo(100), "Recovery must not replace a good backup with corrupt data.");
        }

        [Test]
        public void InterruptedRename_RecoversBackupAndCanSaveAgain()
        {
            store.TrySave(CreateFactory(25));
            File.Move(store.FilePath, store.FilePath + ".bak");
            File.WriteAllText(store.FilePath + ".tmp", "unfinished write");
            var restored = CreateFactory();
            var newStore = new FactorySaveStore(directory);

            Assert.That(newStore.TryLoad(restored), Is.True);
            Assert.That(restored.Balance, Is.EqualTo(25));
            restored.TryUpgrade("press");
            Assert.That(newStore.TrySave(restored), Is.True);
            var latest = CreateFactory();
            Assert.That(new FactorySaveStore(directory).TryLoad(latest), Is.True);
            Assert.That(latest.Balance, Is.EqualTo(15));
            Assert.That(latest.Machines[0].Level, Is.EqualTo(2));
        }

        [Test]
        public void UnwritableLocation_DoesNotThrowOrChangeProgressAndReportsFailureOnce()
        {
            Directory.CreateDirectory(directory);
            string blockedDirectory = Path.Combine(directory, "file-instead-of-directory");
            File.WriteAllText(blockedDirectory, "");
            var warnings = new List<string>();
            var blockedStore = new FactorySaveStore(blockedDirectory, warnings.Add);
            var economy = CreateFactory(12);

            Assert.That(blockedStore.TrySave(economy), Is.False);
            Assert.That(blockedStore.TrySave(economy), Is.False);
            Assert.That(warnings.Count, Is.EqualTo(1));
            Assert.That(economy.Balance, Is.EqualTo(12));
            Assert.That(economy.Advance(1), Is.EqualTo(1));
        }

        private static FactoryEconomy CreateFactory(double balance = 0) => new FactoryEconomy(new[]
        {
            new MachineDefinition("press", "Press", 0, 1, 10, 1.5),
            new MachineDefinition("assembler", "Assembler", 50, 5, 50, 1.5)
        }, balance);
    }
}
