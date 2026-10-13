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
    public sealed class FactoryScreenPersistenceTests
    {
        private string directory;
        private GameObject instance;
        private FactoryScreen screen;
        private EconomyConfig config;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "MiniFactoryScreenTests-" + Guid.NewGuid().ToString("N"));
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
        public void Initialize_LoadsBeforeBindingCardsAndDisplaysSavedIncome()
        {
            var saved = config.CreateEconomy();
            saved.Advance(100.125);
            saved.TryUnlock("assembler");
            saved.TryUpgrade("press");
            new FactorySaveStore(directory).TrySave(saved);

            CreateScreen();

            Assert.That(screen.Session.Balance, Is.EqualTo(40.125));
            Assert.That(screen.Session.TotalProductionPerSecond, Is.EqualTo(7));
            var cards = screen.GetComponentsInChildren<MachineCard>();
            var assembler = cards.Single(card => card.MachineId == "assembler");
            Assert.That(assembler.GetComponentInChildren<MachineMotion>().Running, Is.True);
            Assert.That(assembler.GetComponentsInChildren<FactoryGraphic>().Single(g => g.name == "Locked Shade").enabled, Is.False);
            var press = cards.Single(card => card.MachineId == "press");
            Assert.That(press.GetComponentsInChildren<Text>().Single(t => t.name == "Level").text, Is.EqualTo("Ур. 2"));
            Assert.That(press.GetComponentsInChildren<Text>().Single(t => t.name == "Cost").text, Is.EqualTo("15"));
        }

        [TestCase("press", 90, 2, 0)]
        [TestCase("assembler", 50, 1, 1)]
        public void SuccessfulPurchase_IsSavedImmediately(string id, double balance, int pressLevel, int assemblerLevel)
        {
            CreateScreen();
            screen.Session.Advance(100);
            screen.RefreshViews();
            screen.GetComponentsInChildren<MachineCard>().Single(c => c.MachineId == id)
                .GetComponentInChildren<Button>().onClick.Invoke();
            var loaded = config.CreateEconomy();

            Assert.That(new FactorySaveStore(directory).TryLoad(loaded), Is.True);
            Assert.That(loaded.Balance, Is.EqualTo(balance));
            Assert.That(loaded.GetMachine("press").Level, Is.EqualTo(pressLevel));
            Assert.That(loaded.GetMachine("assembler").Level, Is.EqualTo(assemblerLevel));
        }

        [TestCase("OnApplicationPause", true)]
        [TestCase("OnApplicationFocus", false)]
        public void LifecycleCallback_SavesPassiveIncomeWithoutWaitingForAutosave(string method, bool value)
        {
            CreateScreen();
            screen.Session.Advance(12.75);
            typeof(FactoryScreen).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(screen, new object[] { value });
            var loaded = config.CreateEconomy();

            Assert.That(new FactorySaveStore(directory).TryLoad(loaded), Is.True);
            Assert.That(loaded.Balance, Is.EqualTo(12.75));
        }

        private void CreateScreen()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/FactoryScreen.prefab");
            instance = Object.Instantiate(prefab);
            screen = instance.GetComponent<FactoryScreen>();
            screen.Initialize(new FactorySaveStore(directory), () => new DateTime(2026, 10, 13).Ticks);
        }
    }
}
