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
    public sealed class BoostScreenTests
    {
        private static readonly long Start = new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc).Ticks;
        private string directory;
        private long now;
        private GameObject instance;
        private FactoryScreen screen;
        private EconomyConfig config;
        private BoostView View => instance.GetComponentInChildren<BoostView>(true);
        private Button Button => View.GetComponent<Button>();
        private FactorySaveStore Store => new FactorySaveStore(directory);

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "MiniFactoryBoostTests-" + Guid.NewGuid().ToString("N"));
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
        public void Activation_UpdatesActualIncomeGainAnimationAndSavesImmediately()
        {
            CreateScreen();
            Assert.That(Button.interactable, Is.True);
            Button.onClick.Invoke();

            Assert.That(screen.Session.Boost.IsActive(now), Is.True);
            Assert.That(Button.interactable, Is.False);
            Assert.That(Label(View, "Boost Action").text, Is.EqualTo("Ускорение ×2"));
            Assert.That(Label(View, "Boost Timer").text, Is.EqualTo("01:00"));
            var press = Card("press");
            Assert.That(Label(press, "Rate").text, Is.EqualTo("+2 /с"));
            Assert.That(Label(press, "Production Gain").text, Is.EqualTo("+2 /с"));
            Assert.That(Label(Card("assembler"), "Rate").text, Is.EqualTo("0 /с"));
            Assert.That(press.GetComponentInChildren<MachineMotion>().SpeedMultiplier, Is.EqualTo(2));
            var saved = config.CreateEconomy();
            Assert.That(Store.TryLoad(saved, out long timestamp), Is.True);
            Assert.That(saved.Boost.EndsAtUtcTicks, Is.EqualTo(Start + TimeSpan.FromSeconds(60).Ticks));
            Assert.That(timestamp, Is.EqualTo(now));
            Button.onClick.Invoke();
            Assert.That(screen.Session.Boost.EndsAtUtcTicks, Is.EqualTo(saved.Boost.EndsAtUtcTicks));
        }

        [Test]
        public void PurchaseDuringBoost_SettlesOldRateBeforeApplyingUpgrade()
        {
            CreateScreen();
            Button.onClick.Invoke();
            now += TimeSpan.FromSeconds(10).Ticks;
            screen.RefreshViews();
            Card("press").GetComponentInChildren<Button>().onClick.Invoke();
            Assert.That(screen.Session.Balance, Is.EqualTo(10));
            Assert.That(screen.Session.GetMachine("press").Level, Is.EqualTo(2));
            now += TimeSpan.FromSeconds(10).Ticks;
            Callback("OnApplicationPause", true);
            Assert.That(screen.Session.Balance, Is.EqualTo(50));
        }

        [Test]
        public void RestartAndExpiry_KeepRealDeadlineAndRestoreReadyButton()
        {
            CreateScreen();
            Button.onClick.Invoke();
            Callback("OnApplicationPause", true);
            Object.DestroyImmediate(instance);
            now += TimeSpan.FromSeconds(20).Ticks;
            CreateScreen();
            Assert.That(screen.LastOfflineIncome.Coins, Is.EqualTo(40));
            Assert.That(Label(View, "Boost Timer").text, Is.EqualTo("00:40"));
            Assert.That(Button.interactable, Is.False);
            Callback("OnApplicationPause", true);
            now += TimeSpan.FromSeconds(50).Ticks;
            Callback("OnApplicationPause", false);
            Assert.That(screen.LastOfflineIncome.Coins, Is.EqualTo(90));
            Assert.That(screen.Session.Balance, Is.EqualTo(130));
            Assert.That(Button.interactable, Is.True);
            Assert.That(Label(View, "Boost Action").text, Is.EqualTo("Ускорить ×2"));
            Assert.That(Label(Card("press"), "Rate").text, Is.EqualTo("+1 /с"));
            Assert.That(Card("press").GetComponentInChildren<MachineMotion>().SpeedMultiplier, Is.EqualTo(1));
        }

        [TestCase(1)]
        [TestCase(2)]
        public void OlderSaveVersion_PreservesProgressAndStartsWithoutBoost(int version)
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Store.FilePath, "{\"Version\":" + version + ",\"Progress\":{\"Balance\":42.5,\"Machines\":[{\"Id\":\"press\",\"Level\":3}]}}");
            CreateScreen();
            Assert.That(screen.Session.Balance, Is.EqualTo(42.5));
            Assert.That(screen.Session.GetMachine("press").Level, Is.EqualTo(3));
            Assert.That(screen.Session.Boost.IsActive(now), Is.False);
            Assert.That(Button.interactable, Is.True);
        }

        [Test]
        public void DisabledConfig_HidesControlAndDoesNotChangeImportedAsset()
        {
            var disabled = Object.Instantiate(config);
            try
            {
                var data = new SerializedObject(disabled);
                data.FindProperty("boostEnabled").boolValue = false;
                data.ApplyModifiedPropertiesWithoutUndo();
                CreateScreen(disabled);
                Assert.That(View.gameObject.activeSelf, Is.False);
                Assert.That(screen.Session.TryActivateBoost(now), Is.False);
                Assert.That(config.CreateEconomy().Boost.Definition.Enabled, Is.True);
            }
            finally { Object.DestroyImmediate(disabled); }
        }

        private void CreateScreen(EconomyConfig configuration = null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/FactoryScreen.prefab");
            instance = Object.Instantiate(prefab);
            screen = instance.GetComponent<FactoryScreen>();
            if (configuration != null)
            {
                var data = new SerializedObject(screen);
                data.FindProperty("config").objectReferenceValue = configuration;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            screen.Initialize(Store, () => now);
            Assert.That(View, Is.Not.Null);
        }

        private MachineCard Card(string id) => instance.GetComponentsInChildren<MachineCard>().Single(c => c.MachineId == id);
        private static Text Label(Component parent, string name) => parent.GetComponentsInChildren<Text>(true).Single(t => t.name == name);
        private void Callback(string method, bool value) => typeof(FactoryScreen)
            .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(screen, new object[] { value });
    }
}
