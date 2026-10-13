using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MiniFactory.Analytics;
using MiniFactory.Economy;
using MiniFactory.Presentation;
using MiniFactory.Shop;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MiniFactory.Tests
{
    public sealed class AnalyticsTests
    {
        [Test]
        public void MultipleProviders_ReceiveTheSameEventDespiteProviderAndDiagnosticFailures()
        {
            var first = new RecordingAnalyticsProvider();
            var last = new RecordingAnalyticsProvider();
            var service = new AnalyticsService(new IAnalyticsProvider[] { first, new ThrowingProvider(), last },
                _ => throw new InvalidOperationException("Diagnostic failed"));
            Assert.DoesNotThrow(() => service.Track("game_started", ("balance", 25.5)));
            Assert.That(first.Events.Single(), Is.SameAs(last.Events.Single()));
            Assert.That(last.Events[0].Parameters["balance"], Is.EqualTo(25.5));
        }

        [Test]
        public void Parameters_AreDetachedAndReadOnlyForEveryProvider()
        {
            var input = new (string Key, object Value)[] { ("coins", 1000) };
            var analyticsEvent = new AnalyticsEvent("purchase_succeeded", input);
            input[0] = ("coins", 9999);
            Assert.That(analyticsEvent.Parameters["coins"], Is.EqualTo(1000));
            Assert.Throws<NotSupportedException>(() =>
                ((IDictionary<string, object>)analyticsEvent.Parameters)["coins"] = 1);
        }

        [Test]
        public void DebugProvider_LogsTheEventAndUsefulParametersWithInvariantNumbers()
        {
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
                LogAssert.Expect(LogType.Log, "[Analytics] offline_income_applied | coins=12.5 | capped=True");
                new DebugAnalyticsProvider().Track(new AnalyticsEvent("offline_income_applied", ("coins", 12.5), ("capped", true)));
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }

        [Test]
        public void PurchaseSuccess_IsTrackedAfterSaveAndNotRepeatedAfterRedeliveryOrReload()
        {
            var recorded = new RecordingAnalyticsProvider();
            var economy = Factory();
            var store = new TestStoreGateway();
            var service = new AnalyticsService(new[] { recorded });
            FactoryProgress saved = null;
            using (var shop = new ShopSession(Product(), store, economy, () =>
            {
                Assert.That(recorded.Events, Is.Empty);
                saved = economy.CaptureProgress();
                return true;
            }, service))
                store.Deliver("fake:one");

            var restored = Factory();
            Assert.That(restored.TryRestoreProgress(saved), Is.True);
            var nextStore = new TestStoreGateway();
            using (var shop = new ShopSession(Product(), nextStore, restored, () => true, service))
            {
                nextStore.Deliver("fake:one");
                nextStore.Deliver("fake:two");
            }
            Assert.That(recorded.Events.Select(e => e.Name), Is.EqualTo(new[] { "purchase_succeeded", "purchase_succeeded" }));
            Assert.That(recorded.Events[0].Parameters["transaction_id"], Is.EqualTo("fake:one"));
            Assert.That(recorded.Events[0].Parameters["coins"], Is.EqualTo(1000));
            Assert.That(recorded.Events[1].Parameters["balance"], Is.EqualTo(2000));
        }

        [Test]
        public void FailedSave_ReportsFailureAndSuccessfulRetryReportsOneSuccess()
        {
            var recorded = new RecordingAnalyticsProvider();
            var store = new TestStoreGateway();
            store.SetAvailability(StoreAvailability.Ready);
            bool saveWorks = false;
            using var shop = new ShopSession(Product(), store, Factory(), () => saveWorks, new AnalyticsService(new[] { recorded }));
            store.Deliver("fake:one");
            Assert.That(recorded.Events.Single().Name, Is.EqualTo("purchase_failed"));
            Assert.That(recorded.Events[0].Parameters["reason"], Is.EqualTo("save_failed"));
            saveWorks = true;
            shop.Retry();
            store.Deliver("fake:one");
            Assert.That(recorded.Events.Select(e => e.Name), Is.EqualTo(new[] { "purchase_failed", "purchase_succeeded" }));
        }

        [TestCase(PurchaseIssue.Cancelled, "cancelled")]
        [TestCase(PurchaseIssue.Failed, "failed")]
        [TestCase(PurchaseIssue.ConfirmationFailed, "confirmation_failed")]
        public void PurchaseFailures_IncludeProductReasonAndStage(PurchaseIssue issue, string reason)
        {
            var recorded = new RecordingAnalyticsProvider();
            var store = new TestStoreGateway();
            using var shop = new ShopSession(Product(), store, Factory(), () => true, new AnalyticsService(new[] { recorded }));
            store.Fail(issue);
            var analyticsEvent = recorded.Events.Single();
            Assert.That(analyticsEvent.Name, Is.EqualTo("purchase_failed"));
            Assert.That(analyticsEvent.Parameters["product_id"], Is.EqualTo("coins_pack_small"));
            Assert.That(analyticsEvent.Parameters["reason"], Is.EqualTo(reason));
            Assert.That(analyticsEvent.Parameters["stage"], Is.EqualTo(issue == PurchaseIssue.ConfirmationFailed ? "confirmation" : "purchase"));
        }

        [Test]
        public void DeferredPayment_IsNotReportedAsSuccessOrFailure()
        {
            var recorded = new RecordingAnalyticsProvider();
            var store = new TestStoreGateway();
            using var shop = new ShopSession(Product(), store, Factory(), () => true, new AnalyticsService(new[] { recorded }));
            store.Fail(PurchaseIssue.Deferred);
            Assert.That(recorded.Events, Is.Empty);
        }

        [Test]
        public void FailingProvider_DoesNotRollBackOrPreventPurchaseConfirmation()
        {
            var recorded = new RecordingAnalyticsProvider();
            var store = new TestStoreGateway();
            var economy = Factory();
            using var shop = new ShopSession(Product(), store, economy, () => true,
                new AnalyticsService(new IAnalyticsProvider[] { new ThrowingProvider(), recorded }));
            store.Deliver("fake:one");
            Assert.That(economy.Balance, Is.EqualTo(1000));
            Assert.That(store.Confirmations, Is.EqualTo(1));
            Assert.That(recorded.Events.Single().Name, Is.EqualTo("purchase_succeeded"));
        }

        private static FactoryEconomy Factory() => new FactoryEconomy(new[] { new MachineDefinition("press", "Press", 0, 1, 10, 1.5) });
        private static ShopProductDefinition Product() => new ShopProductDefinition("coins_pack_small", "Coins", 1000);
        private sealed class ThrowingProvider : IAnalyticsProvider
        {
            public void Track(AnalyticsEvent analyticsEvent) => throw new InvalidOperationException("Provider failed");
        }
    }

    internal sealed class RecordingAnalyticsProvider : IAnalyticsProvider
    {
        public readonly List<AnalyticsEvent> Events = new List<AnalyticsEvent>();
        public void Track(AnalyticsEvent analyticsEvent) => Events.Add(analyticsEvent);
    }
}
