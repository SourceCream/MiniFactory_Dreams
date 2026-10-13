using System;
using System.IO;
using System.Linq;
using System.Reflection;
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
    public sealed class OfflinePersistenceTests
    {
        private static readonly long Start = new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc).Ticks;
        private string directory;
        private long now;
        private GameObject instance;
        private FactoryScreen screen;
        private EconomyConfig config;
        private FactorySaveStore Store => new FactorySaveStore(directory);
        private OfflineIncomePopup Popup => instance.GetComponentInChildren<OfflineIncomePopup>(true);

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "MiniFactoryOfflineTests-" + Guid.NewGuid().ToString("N"));
            now = Start;
            config = AssetDatabase.LoadAssetAtPath<EconomyConfig>("Assets/_Project/Configs/EconomyConfig.asset");
        }

        [TearDown]
        public void TearDown()
        {
            if (instance != null) Object.DestroyImmediate(instance);
            if (!Directory.Exists(directory)) return;
            foreach (string file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }

        [Test]
        public void ColdStart_CreditsSavesAndShowsIncomeBeforeAnyDismissal()
        {
            var prior = config.CreateEconomy();
            prior.Advance(100);
            prior.TryUnlock("assembler");
            prior.TryUpgrade("press");
            Store.TrySave(prior, now);
            now += TimeSpan.FromSeconds(90).Ticks;

            CreateScreen();

            Assert.That(screen.Session.Balance, Is.EqualTo(670));
            Assert.That(screen.LastOfflineIncome.Coins, Is.EqualTo(630));
            Assert.That(Popup.IsVisible, Is.True);
            Assert.That(Label("Absence Duration").text, Is.EqualTo("1 мин 30 с"));
            Assert.That(Label("Offline Earnings").text, Is.EqualTo("+630"));
            var saved = config.CreateEconomy();
            Assert.That(Store.TryLoad(saved, out long timestamp), Is.True);
            Assert.That(saved.Balance, Is.EqualTo(670));
            Assert.That(timestamp, Is.EqualTo(now));

            Button button = Popup.GetComponentInChildren<Button>();
            button.onClick.Invoke();
            button.onClick.Invoke();
            Assert.That(Popup.IsVisible, Is.False);
            Assert.That(screen.Session.Balance, Is.EqualTo(670));

            // Reopening without another absence must not award the same interval again.
            Object.DestroyImmediate(instance);
            CreateScreen();
            Assert.That(screen.Session.Balance, Is.EqualTo(670));
            Assert.That(Popup.IsVisible, Is.False);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void PauseAndFocusPairs_CreditOneAbsenceRegardlessOfCallbackOrder(bool focusFirst)
        {
            CreateScreen();
            screen.Session.Advance(12);
            Callback(focusFirst ? "OnApplicationFocus" : "OnApplicationPause", !focusFirst);
            now += TimeSpan.FromSeconds(3).Ticks;
            Callback(focusFirst ? "OnApplicationPause" : "OnApplicationFocus", focusFirst);
            now += TimeSpan.FromSeconds(7).Ticks;
            Callback(focusFirst ? "OnApplicationFocus" : "OnApplicationPause", focusFirst);
            Assert.That(screen.Session.Balance, Is.EqualTo(12), "Still away until both pause and focus are restored.");
            Callback(focusFirst ? "OnApplicationPause" : "OnApplicationFocus", !focusFirst);
            Callback("OnApplicationPause", false);
            Callback("OnApplicationFocus", true);

            Assert.That(screen.Session.Balance, Is.EqualTo(22));
            Assert.That(screen.LastOfflineIncome.AwaySeconds, Is.EqualTo(10));
            Assert.That(Popup.IsVisible, Is.True);
        }

        [Test]
        public void DestroyWhileSuspended_PreservesDepartureTimeForNextLaunch()
        {
            CreateScreen();
            screen.Session.Advance(12);
            Callback("OnApplicationPause", true);
            now += TimeSpan.FromSeconds(30).Ticks;
            Object.DestroyImmediate(instance);

            var loaded = config.CreateEconomy();
            Assert.That(Store.TryLoad(loaded, out long timestamp), Is.True);
            Assert.That(timestamp, Is.EqualTo(Start));
            now += TimeSpan.FromSeconds(30).Ticks;
            CreateScreen();
            Assert.That(screen.Session.Balance, Is.EqualTo(72));
            Assert.That(screen.LastOfflineIncome.AwaySeconds, Is.EqualTo(60));
        }

        [Test]
        public void LongAbsence_UsesConfiguredLimitButShowsActualTimeAway()
        {
            Store.TrySave(config.CreateEconomy(), now);
            now += TimeSpan.FromDays(2).Ticks;
            CreateScreen();

            Assert.That(config.MaximumOfflineSeconds, Is.EqualTo(28800));
            Assert.That(screen.Session.Balance, Is.EqualTo(config.MaximumOfflineSeconds));
            Assert.That(screen.LastOfflineIncome.AwaySeconds, Is.EqualTo(172800));
            Assert.That(Label("Absence Duration").text, Is.EqualTo("2 д 0 ч"));
            Assert.That(Label("Offline Limit").text, Is.EqualTo("Лимит дохода: 8 ч 0 мин"));
        }

        [Test]
        public void LegacySave_MigratesWithoutResetOrInventedOfflineIncome()
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Store.FilePath, "{\"Version\":1,\"Progress\":{\"Balance\":42.5,\"Machines\":[{\"Id\":\"press\",\"Level\":3}]}}");
            CreateScreen();

            Assert.That(screen.Session.Balance, Is.EqualTo(42.5));
            Assert.That(screen.Session.GetMachine("press").Level, Is.EqualTo(3));
            Assert.That(screen.LastOfflineIncome.Coins, Is.Zero);
            Assert.That(Popup.IsVisible, Is.False);
            Assert.That(Store.TryLoad(config.CreateEconomy(), out long timestamp), Is.True);
            Assert.That(timestamp, Is.EqualTo(now));
        }

        [Test]
        public void CorruptPrimary_RestoresTimestampFromTheSameBackupAsBalance()
        {
            var store = Store;
            var progress = config.CreateEconomy();
            store.TrySave(progress, Start);
            progress.Advance(10);
            store.TrySave(progress, Start + TimeSpan.FromSeconds(10).Ticks);
            File.WriteAllText(store.FilePath, "corrupt");
            now += TimeSpan.FromSeconds(20).Ticks;
            CreateScreen();

            Assert.That(screen.Session.Balance, Is.EqualTo(20));
            Assert.That(screen.LastOfflineIncome.AwaySeconds, Is.EqualTo(20));
        }

        private void CreateScreen()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/FactoryScreen.prefab");
            instance = Object.Instantiate(prefab);
            screen = instance.GetComponent<FactoryScreen>();
            screen.Initialize(Store, () => now);
            Assert.That(Popup, Is.Not.Null, "The committed screen must include the offline income dialog.");
        }

        private Text Label(string name) => Popup.GetComponentsInChildren<Text>(true).Single(text => text.name == name);
        private void Callback(string method, bool value) => typeof(FactoryScreen)
            .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(screen, new object[] { value });
    }
}
