using System;
using System.IO;
using MiniFactory.Economy;
using MiniFactory.Persistence;
using MiniFactory.Shop;
using NUnit.Framework;

namespace MiniFactory.Tests
{
    public sealed class ShopSessionTests
    {
        private FactoryEconomy economy;
        private TestStoreGateway gateway;
        private ShopSession shop;
        private bool saveWorks;
        private FactoryProgress saved;
        private int saves;

        [SetUp]
        public void SetUp()
        {
            economy = Factory();
            gateway = new TestStoreGateway();
            saveWorks = true;
            saves = 0;
            shop = new ShopSession(new ShopProductDefinition("coins_pack_small", "Coins", 1000), gateway, economy, () =>
            {
                saves++;
                if (saveWorks) saved = economy.CaptureProgress();
                return saveWorks;
            });
            shop.Initialize();
        }

        [TearDown] public void TearDown() => shop.Dispose();

        [Test]
        public void ProductLoadingAndRepeatedClicks_DoNotStartDuplicatePurchases()
        {
            shop.Buy();
            Assert.That(gateway.Purchases, Is.Zero);
            gateway.SetAvailability(StoreAvailability.Ready);
            shop.Buy();
            shop.Buy();
            Assert.That(gateway.Purchases, Is.EqualTo(1));
            Assert.That(shop.IsBusy, Is.True);
            Assert.That(economy.Balance, Is.EqualTo(12));
        }

        [Test]
        public void Success_SavesBalanceAndTransactionBeforeConfirmation()
        {
            gateway.OnConfirm = _ =>
            {
                Assert.That(saved.Balance, Is.EqualTo(1012));
                Assert.That(saved.GrantedPurchaseIds, Does.Contain("fake:one"));
            };
            gateway.Deliver("fake:one");
            Assert.That(economy.Balance, Is.EqualTo(1012));
            Assert.That(gateway.Confirmations, Is.EqualTo(1));
            Assert.That(saves, Is.EqualTo(1));
            Assert.That(shop.Notice, Is.EqualTo(ShopNotice.Purchased));
        }

        [Test]
        public void RepeatedDelivery_IsIdempotentButAnotherConsumableGrantsAgain()
        {
            gateway.Deliver("fake:one");
            gateway.Deliver("fake:one");
            Assert.That(economy.Balance, Is.EqualTo(1012));
            gateway.Deliver("fake:two");
            Assert.That(economy.Balance, Is.EqualTo(2012));
        }

        [Test]
        public void SaveFailure_RollsBackAndRetriesTheSamePaidOrder()
        {
            gateway.SetAvailability(StoreAvailability.Ready);
            saveWorks = false;
            gateway.Deliver("fake:one");
            Assert.That(economy.Balance, Is.EqualTo(12));
            Assert.That(economy.CaptureProgress().GrantedPurchaseIds, Is.Empty);
            Assert.That(gateway.Confirmations, Is.Zero);
            Assert.That(shop.CanBuy, Is.False);
            Assert.That(shop.CanRetry, Is.True);
            saveWorks = true;
            shop.Retry();
            Assert.That(economy.Balance, Is.EqualTo(1012));
            Assert.That(gateway.Confirmations, Is.EqualTo(1));
            Assert.That(gateway.Purchases, Is.Zero);
        }

        [Test]
        public void ConfirmationFailure_RetryDoesNotGrantCoinsAgain()
        {
            gateway.SetAvailability(StoreAvailability.Ready);
            gateway.OnConfirm = _ => gateway.Fail(PurchaseIssue.ConfirmationFailed);
            gateway.Deliver("fake:one");
            Assert.That(shop.CanRetry, Is.True);
            Assert.That(economy.Balance, Is.EqualTo(1012));
            gateway.OnConfirm = null;
            shop.Retry();
            Assert.That(economy.Balance, Is.EqualTo(1012));
            Assert.That(gateway.Confirmations, Is.EqualTo(2));
        }

        [TestCase(PurchaseIssue.Cancelled, ShopNotice.Cancelled)]
        [TestCase(PurchaseIssue.Failed, ShopNotice.Failed)]
        [TestCase(PurchaseIssue.Deferred, ShopNotice.Deferred)]
        public void UnsuccessfulPurchase_DoesNotChangeProgress(PurchaseIssue failure, ShopNotice notice)
        {
            gateway.SetAvailability(StoreAvailability.Ready);
            shop.Buy();
            gateway.Fail(failure);
            Assert.That(economy.Balance, Is.EqualTo(12));
            Assert.That(saves, Is.Zero);
            Assert.That(gateway.Confirmations, Is.Zero);
            Assert.That(shop.Notice, Is.EqualTo(notice));
            Assert.That(shop.IsBusy, Is.False);
        }

        [TestCase("unknown", "fake:one")]
        [TestCase("coins_pack_small", "")]
        public void InvalidOrder_IsNotGrantedOrConfirmed(string product, string transaction)
        {
            gateway.Deliver(transaction, product);
            Assert.That(economy.Balance, Is.EqualTo(12));
            Assert.That(gateway.Confirmations, Is.Zero);
        }

        [Test]
        public void StoreUnavailable_CanRetryInitialization()
        {
            gateway.SetAvailability(StoreAvailability.Unavailable);
            Assert.That(shop.CanBuy, Is.False);
            shop.Retry();
            Assert.That(gateway.Initializations, Is.EqualTo(2));
            Assert.That(shop.Availability, Is.EqualTo(StoreAvailability.Loading));
        }

        [Test]
        public void Boost_DoesNotMultiplyPurchasedCoins()
        {
            economy.TryActivateBoost(new DateTime(2026, 10, 13).Ticks);
            gateway.Deliver("fake:one");
            Assert.That(economy.Balance, Is.EqualTo(1012));
        }

        [Test]
        public void Dispose_UnsubscribesFromLateStoreCallbacks()
        {
            shop.Dispose();
            gateway.Deliver("late");
            Assert.That(economy.Balance, Is.EqualTo(12));
            Assert.That(gateway.Disposed, Is.True);
        }

        [Test]
        public void SaveRoundTrip_PreservesDeduplicationAndDetachedSnapshots()
        {
            string path = Path.Combine(Path.GetTempPath(), "MiniFactoryShop-" + Guid.NewGuid().ToString("N"));
            try
            {
                gateway.Deliver("fake:one");
                var store = new FactorySaveStore(path);
                Assert.That(store.TrySave(economy), Is.True);
                var restored = Factory();
                Assert.That(store.TryLoad(restored), Is.True);
                Assert.That(restored.TryGrantPurchase("fake:one", 1000), Is.False);
                Assert.That(restored.Balance, Is.EqualTo(1012));
                var snapshot = restored.CaptureProgress();
                snapshot.GrantedPurchaseIds[0] = "tampered";
                Assert.That(restored.TryGrantPurchase("fake:one", 1000), Is.False);
            }
            finally
            {
                if (Directory.Exists(path))
                {
                    foreach (string file in Directory.GetFiles(path)) File.Delete(file);
                    Directory.Delete(path);
                }
            }
        }

        [Test]
        public void InvalidPurchaseLedger_RestoreIsAtomic()
        {
            var snapshot = economy.CaptureProgress();
            snapshot.Balance = 9999;
            snapshot.GrantedPurchaseIds = new[] { "same", "same" };
            Assert.That(economy.TryRestoreProgress(snapshot), Is.False);
            Assert.That(economy.Balance, Is.EqualTo(12));
        }

        private static FactoryEconomy Factory() => new FactoryEconomy(
            new[] { new MachineDefinition("press", "Press", 0, 1, 10, 1.5) }, 12,
            new BoostDefinition(true, 60, 2));
    }

    internal sealed class TestStoreGateway : IStoreGateway
    {
        public StoreAvailability Availability { get; private set; }
        public string PriceLabel => "$0.01";
        public int Initializations;
        public int Purchases;
        public int Confirmations;
        public bool Disposed;
        public Action<string> OnConfirm;
        public event Action AvailabilityChanged;
        public event Action<StorePurchase> PurchasePending;
        public event Action<PurchaseIssue> PurchaseFailed;
        public void Initialize(string productId) { Initializations++; SetAvailability(StoreAvailability.Loading); }
        public void Purchase() => Purchases++;
        public void Confirm(string transactionId) { Confirmations++; OnConfirm?.Invoke(transactionId); }
        public void SetAvailability(StoreAvailability state) { Availability = state; AvailabilityChanged?.Invoke(); }
        public void Deliver(string id, string product = "coins_pack_small") => PurchasePending?.Invoke(new StorePurchase(product, id));
        public void Fail(PurchaseIssue issue) => PurchaseFailed?.Invoke(issue);
        public void Dispose() => Disposed = true;
    }
}
