using System;
using System.Linq;
using MiniFactory.Configuration;
using MiniFactory.Presentation;
using MiniFactory.Shop;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MiniFactory.Tests
{
    public sealed class ShopScreenTests
    {
        private GameObject root;
        private FactoryScreen screen;
        private TestStoreGateway gateway;
        private ShopPopup popup;

        [SetUp]
        public void SetUp()
        {
            root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/FactoryScreen.prefab"));
            screen = root.GetComponent<FactoryScreen>();
            screen.Initialize(utcClock: () => new DateTime(2026, 10, 13).Ticks);
            gateway = new TestStoreGateway();
            screen.InitializeShop(gateway);
            popup = root.GetComponentInChildren<ShopPopup>(true);
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(root);

        [Test]
        public void ShopButton_OpensOneProductAndCloseReturnsToFactory()
        {
            Assert.That(popup.IsVisible, Is.False);
            root.GetComponentsInChildren<Button>().Single(b => b.name == "Shop Button").onClick.Invoke();
            Assert.That(popup.IsVisible, Is.True);
            Assert.That(Text("Shop Amount"), Is.EqualTo("1 000"));
            Assert.That(Text("Shop Product"), Is.EqualTo("Набор монет"));
            Button("Shop Close").onClick.Invoke();
            Assert.That(popup.IsVisible, Is.False);
        }

        [Test]
        public void LoadingReadyPurchaseAndCancellation_UpdateControls()
        {
            popup.Show();
            Assert.That(Button("Shop Buy").interactable, Is.False);
            Assert.That(Text("Shop Action"), Is.EqualTo("Загрузка…"));
            gateway.SetAvailability(StoreAvailability.Ready);
            Assert.That(Text("Shop Action"), Is.EqualTo("$0.01"));
            Button("Shop Buy").onClick.Invoke();
            Assert.That(Button("Shop Close").interactable, Is.False);
            Assert.That(Button("Shop Buy").interactable, Is.False);
            gateway.Fail(PurchaseIssue.Cancelled);
            Assert.That(Text("Shop Status"), Is.EqualTo("Покупка отменена"));
            Assert.That(Button("Shop Buy").interactable, Is.True);
            Assert.That(screen.Session.Balance, Is.Zero);
        }

        [Test]
        public void Purchase_RefreshesFactoryBalanceAndAffordableMachineButtons()
        {
            gateway.SetAvailability(StoreAvailability.Ready);
            gateway.Deliver("test:coins");
            Assert.That(screen.Session.Balance, Is.EqualTo(1000));
            Assert.That(Text("Shop Status"), Is.EqualTo("+1 000 монет зачислено"));
            Assert.That(root.GetComponentsInChildren<MachineCard>().All(c => c.GetComponentInChildren<Button>().interactable), Is.True);
        }

        [Test]
        public void UnavailableStore_ShowsRetryInsteadOfPrice()
        {
            gateway.SetAvailability(StoreAvailability.Unavailable);
            Assert.That(Text("Shop Action"), Is.EqualTo("Повторить"));
            Button("Shop Buy").onClick.Invoke();
            Assert.That(gateway.Initializations, Is.EqualTo(2));
        }

        [Test]
        public void ConfigurationAssets_AreSeparateAndEconomyDoesNotOwnBoostOrShopFields()
        {
            var economy = AssetDatabase.LoadAssetAtPath<EconomyConfig>("Assets/_Project/Configs/EconomyConfig.asset");
            var boost = AssetDatabase.LoadAssetAtPath<BoostConfig>("Assets/_Project/Configs/BoostConfig.asset");
            var shop = AssetDatabase.LoadAssetAtPath<ShopConfig>("Assets/_Project/Configs/ShopConfig.asset");
            Assert.That(boost.ToDefinition().Multiplier, Is.EqualTo(2));
            Assert.That(boost.ToDefinition().DurationSeconds, Is.EqualTo(60));
            Assert.That(shop.ToDefinition().Coins, Is.EqualTo(1000));
            Assert.That(shop.UseFakeStore, Is.True);
            var data = new SerializedObject(economy);
            Assert.That(data.FindProperty("boostEnabled"), Is.Null);
            Assert.That(data.FindProperty("productId"), Is.Null);
            Assert.That(economy.CreateEconomy().Boost.Definition.Enabled, Is.False);
        }

        private Button Button(string name) => popup.GetComponentsInChildren<Button>(true).Single(b => b.name == name);
        private string Text(string name) => popup.GetComponentsInChildren<Text>(true).Single(t => t.name == name).text;
    }
}
