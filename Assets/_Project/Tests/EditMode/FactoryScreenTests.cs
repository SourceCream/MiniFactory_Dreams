using System.Linq;
using MiniFactory.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MiniFactory.Tests
{
    public sealed class FactoryScreenTests
    {
        private GameObject instance;
        private FactoryScreen screen;

        [SetUp]
        public void SetUp()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/FactoryScreen.prefab");
            Assert.That(prefab, Is.Not.Null, "The playable factory prefab must be committed with the scripts.");
            instance = Object.Instantiate(prefab);
            screen = instance.GetComponent<FactoryScreen>();
            screen.Initialize(utcClock: () => new System.DateTime(2026, 10, 13).Ticks);
        }

        [TearDown]
        public void TearDown()
        {
            if (instance != null) Object.DestroyImmediate(instance);
        }

        [Test]
        public void NewScreen_ShowsFirstMachineRunningAndUnaffordableActionsDisabled()
        {
            Assert.That(screen.Session.Balance, Is.Zero);
            Assert.That(screen.Session.TotalProductionPerSecond, Is.EqualTo(1));
            Assert.That(instance.GetComponentsInChildren<MachineCard>().Length, Is.EqualTo(3));
            Assert.That(Card("press").GetComponentInChildren<MachineMotion>().Running, Is.True);
            Assert.That(Card("assembler").GetComponentInChildren<MachineMotion>().Running, Is.False);
            Assert.That(Label(Card("assembler"), "Rate").text, Is.EqualTo("0 /с"));
            Assert.That(Label(Card("assembler"), "Production Gain").text, Is.EqualTo("+5 /с"));
            Assert.That(Label(Card("packer"), "Rate").text, Is.EqualTo("0 /с"));
            Assert.That(instance.GetComponentsInChildren<MachineCard>()
                .All(card => !card.GetComponentInChildren<Button>().interactable), Is.True);
        }

        [Test]
        public void UpgradeButton_SpendsCoinsOnceAndRefreshesIncomeAndLevel()
        {
            screen.Session.Advance(10);
            screen.RefreshViews();
            var card = Card("press");
            var button = card.GetComponentInChildren<Button>();
            Assert.That(button.interactable, Is.True);

            button.onClick.Invoke();

            Assert.That(screen.Session.Balance, Is.Zero);
            Assert.That(screen.Session.GetMachine("press").Level, Is.EqualTo(2));
            Assert.That(screen.Session.TotalProductionPerSecond, Is.EqualTo(2));
            Assert.That(Label(card, "Level").text, Is.EqualTo("Ур. 2"));
            Assert.That(screen.Session.GetMachine("press").UpgradeCost, Is.EqualTo(15));
            Assert.That(Label(card, "Cost").text, Is.EqualTo("15"));
            Assert.That(Label(card, "Rate").text, Is.EqualTo("+2 /с"));
            Assert.That(Label(card, "Production Gain").text, Is.EqualTo("+1 /с"));
            Assert.That(button.interactable, Is.False);
            // A stale/double click must also be rejected by the economy.
            button.onClick.Invoke();
            Assert.That(screen.Session.GetMachine("press").Level, Is.EqualTo(2));
        }

        [Test]
        public void UnlockButton_StartsMachineAndChangesActionToUpgrade()
        {
            screen.Session.Advance(50);
            screen.RefreshViews();
            var card = Card("assembler");
            var button = card.GetComponentInChildren<Button>();
            Assert.That(button.interactable, Is.True);
            Assert.That(Shade(card).enabled, Is.True, "An affordable machine stays locked until bought.");
            Assert.That(Label(card, "Cost").text, Is.EqualTo("50"));
            button.onClick.Invoke();

            Assert.That(screen.Session.Balance, Is.Zero);
            Assert.That(screen.Session.TotalProductionPerSecond, Is.EqualTo(6));
            Assert.That(card.GetComponentInChildren<MachineMotion>().Running, Is.True);
            Assert.That(Card("packer").GetComponentInChildren<MachineMotion>().Running, Is.False);
            Assert.That(Label(card, "Action").text, Is.EqualTo("Улучшить"));
            Assert.That(Label(card, "Rate").text, Is.EqualTo("+5 /с"));
            Assert.That(Label(card, "Production Gain").text, Is.EqualTo("+5 /с"));
            Assert.That(Shade(card).enabled, Is.False);
            Assert.That(button.interactable, Is.False);
        }

        [Test]
        public void LockedMachine_CannotBeBoughtWithInsufficientFunds()
        {
            screen.Session.Advance(49.99);
            screen.RefreshViews();
            var button = Card("assembler").GetComponentInChildren<Button>();
            Assert.That(button.interactable, Is.False);
            button.onClick.Invoke();
            Assert.That(screen.Session.GetMachine("assembler").IsUnlocked, Is.False);
            Assert.That(screen.Session.Balance, Is.EqualTo(49.99).Within(0.000001));
            Assert.That(EconomyDisplay.Coins(screen.Session.Balance), Is.EqualTo("49"));
        }

        private MachineCard Card(string id) => instance.GetComponentsInChildren<MachineCard>().Single(card => card.MachineId == id);
        private static Text Label(MachineCard card, string name) => card.GetComponentsInChildren<Text>().Single(text => text.name == name);
        private static FactoryGraphic Shade(MachineCard card) => card.GetComponentsInChildren<FactoryGraphic>().Single(graphic => graphic.name == "Locked Shade");
    }
}
