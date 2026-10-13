using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MiniFactory.Analytics;
using MiniFactory.Configuration;
using MiniFactory.Persistence;
using MiniFactory.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MiniFactory.Tests
{
    public sealed class AnalyticsScreenTests
    {
        private static readonly long Start = new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc).Ticks;
        private long now;
        private string directory;
        private GameObject root;
        private FactoryScreen screen;
        private RecordingAnalyticsProvider recorded;

        [SetUp]
        public void SetUp()
        {
            now = Start;
            directory = Path.Combine(Path.GetTempPath(), "MiniFactoryAnalytics-" + Guid.NewGuid().ToString("N"));
            recorded = new RecordingAnalyticsProvider();
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
            if (!Directory.Exists(directory)) return;
            foreach (string file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }

        [Test]
        public void GameplayActions_ReportActualResultsAndIgnoreRejectedActions()
        {
            CreateScreen();
            screen.Initialize();
            ClickMachine("assembler"); // Not enough money.
            Assert.That(recorded.Events.Select(e => e.Name), Is.EqualTo(new[] { "game_started" }));
            screen.Session.Advance(100);
            ClickMachine("assembler");
            ClickMachine("press");
            var boostButton = root.GetComponentInChildren<BoostView>().GetComponent<Button>();
            boostButton.onClick.Invoke();
            boostButton.onClick.Invoke(); // Already active.
            now += TimeSpan.FromSeconds(61).Ticks;
            Callback("Update");
            Callback("Update");
            Assert.That(recorded.Events.Select(e => e.Name), Is.EqualTo(new[]
                { "game_started", "machine_unlocked", "machine_upgraded", "boost_started", "boost_finished" }));
            Assert.That(recorded.Events[1].Parameters["machine_id"], Is.EqualTo("assembler"));
            Assert.That(recorded.Events[1].Parameters["cost"], Is.EqualTo(50));
            Assert.That(recorded.Events[2].Parameters["level"], Is.EqualTo(2));
            Assert.That(recorded.Events[2].Parameters["balance"], Is.EqualTo(40));
            Assert.That(recorded.Events[3].Parameters["multiplier"], Is.EqualTo(2));
            Assert.That(recorded.Events[4].Parameters["completed_offline"], Is.False);
        }

        [Test]
        public void RestoredExpiredBoostAndCappedOfflineIncome_AreReportedOnceAcrossFocusAndPause()
        {
            var config = AssetDatabase.LoadAssetAtPath<EconomyConfig>("Assets/_Project/Configs/EconomyConfig.asset");
            var boost = AssetDatabase.LoadAssetAtPath<BoostConfig>("Assets/_Project/Configs/BoostConfig.asset");
            var economy = config.CreateEconomy(boost.ToDefinition());
            economy.TryActivateBoost(Start);
            Assert.That(new FactorySaveStore(directory).TrySave(economy, Start), Is.True);
            now += TimeSpan.FromHours(9).Ticks;
            CreateScreen();
            Assert.That(recorded.Events.Select(e => e.Name), Is.EqualTo(new[]
                { "game_started", "boost_finished", "offline_income_applied" }));
            Assert.That(recorded.Events[0].Parameters["restored_save"], Is.True);
            Assert.That(recorded.Events[1].Parameters["completed_offline"], Is.True);
            var income = recorded.Events[2];
            Assert.That(income.Parameters["away_seconds"], Is.EqualTo(32400));
            Assert.That(income.Parameters["credited_seconds"], Is.EqualTo(28800));
            Assert.That(income.Parameters["coins"], Is.EqualTo(28860));
            Assert.That(income.Parameters["capped"], Is.True);
            Callback("OnApplicationPause", true);
            Callback("OnApplicationFocus", false);
            now += TimeSpan.FromSeconds(10).Ticks;
            Callback("OnApplicationPause", false);
            Assert.That(recorded.Events.Count, Is.EqualTo(3));
            Callback("OnApplicationFocus", true);
            Callback("OnApplicationFocus", true);
            Assert.That(recorded.Events.Count(e => e.Name == "offline_income_applied"), Is.EqualTo(2));
            Assert.That(recorded.Events.Count(e => e.Name == "boost_finished"), Is.EqualTo(1));
            Assert.That(recorded.Events.Count(e => e.Name == "game_started"), Is.EqualTo(1));
        }

        [Test]
        public void ShopReceivesTheSameAnalyticsServiceAsTheGame()
        {
            CreateScreen();
            var store = new TestStoreGateway();
            screen.InitializeShop(store);
            store.Deliver("fake:one");
            store.Fail(MiniFactory.Shop.PurchaseIssue.Cancelled);
            Assert.That(recorded.Events.Select(e => e.Name), Is.EqualTo(new[]
                { "game_started", "purchase_succeeded", "purchase_failed" }));
        }

        private void CreateScreen()
        {
            root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/FactoryScreen.prefab"));
            screen = root.GetComponent<FactoryScreen>();
            screen.Initialize(new FactorySaveStore(directory), () => now, new AnalyticsService(new[] { recorded }));
        }

        private void ClickMachine(string id) => root.GetComponentsInChildren<MachineCard>().Single(c => c.MachineId == id)
            .GetComponentInChildren<Button>().onClick.Invoke();

        private void Callback(string method, params object[] args) => typeof(FactoryScreen)
            .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(screen, args);
    }
}
