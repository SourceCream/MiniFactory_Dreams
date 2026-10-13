using MiniFactory.Configuration;
using NUnit.Framework;
using UnityEditor;

namespace MiniFactory.Tests
{
    public sealed class EconomyConfigTests
    {
        private const string ConfigPath = "Assets/_Project/Configs/EconomyConfig.asset";

        [Test]
        public void LocalAsset_CreatesThreeMachinesWithOnlyFirstUnlocked()
        {
            var config = AssetDatabase.LoadAssetAtPath<EconomyConfig>(ConfigPath);
            Assert.That(config, Is.Not.Null, "The local economy asset must import successfully.");

            var economy = config.CreateEconomy();

            Assert.That(economy.Machines.Count, Is.EqualTo(3));
            Assert.That(economy.Machines[0].Level, Is.EqualTo(1));
            Assert.That(economy.Machines[1].IsUnlocked, Is.False);
            Assert.That(economy.Machines[2].IsUnlocked, Is.False);
            Assert.That(economy.TotalProductionPerSecond, Is.GreaterThan(0));
        }

        [Test]
        public void LocalAsset_PlayerProgressDoesNotModifyConfiguration()
        {
            var config = AssetDatabase.LoadAssetAtPath<EconomyConfig>(ConfigPath);
            Assert.That(config, Is.Not.Null);
            var first = config.CreateEconomy();
            var baseline = config.CreateEconomy();
            first.Advance(100);
            Assert.That(first.TryUpgrade(first.Machines[0].Definition.Id), Is.True);

            var fresh = config.CreateEconomy();

            Assert.That(fresh.Balance, Is.EqualTo(baseline.Balance));
            Assert.That(fresh.Machines[0].Level, Is.EqualTo(1));
            Assert.That(fresh.Machines[0].UpgradeCost, Is.EqualTo(baseline.Machines[0].UpgradeCost));
        }
    }
}
