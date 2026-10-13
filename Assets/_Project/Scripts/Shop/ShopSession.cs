using System;
using MiniFactory.Analytics;
using MiniFactory.Economy;

namespace MiniFactory.Shop
{
    public enum ShopNotice { None, Purchased, Cancelled, Failed, SaveFailed, Deferred, ConfirmationFailed }

    public sealed class ShopSession : IDisposable
    {
        private readonly IStoreGateway store;
        private readonly FactoryEconomy economy;
        private readonly Func<bool> save;
        private readonly AnalyticsService analytics;
        private StorePurchase retryPurchase;
        private bool disposed;
        public ShopProductDefinition Product { get; }
        public StoreAvailability Availability => store.Availability;
        public string PriceLabel => store.PriceLabel;
        public bool IsBusy { get; private set; }
        public ShopNotice Notice { get; private set; }
        public bool CanRetry => !IsBusy && (Availability == StoreAvailability.Unavailable ||
            Notice == ShopNotice.SaveFailed || Notice == ShopNotice.ConfirmationFailed);
        public bool CanBuy => Availability == StoreAvailability.Ready && !IsBusy && !CanRetry;
        public event Action Changed;

        public ShopSession(ShopProductDefinition product, IStoreGateway store, FactoryEconomy economy, Func<bool> save,
            AnalyticsService analytics = null)
        {
            Product = product ?? throw new ArgumentNullException(nameof(product));
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.economy = economy ?? throw new ArgumentNullException(nameof(economy));
            this.save = save ?? throw new ArgumentNullException(nameof(save));
            this.analytics = analytics;
            store.AvailabilityChanged += OnAvailabilityChanged;
            store.PurchasePending += Fulfill;
            store.PurchaseFailed += OnFailure;
        }

        public void Initialize()
        {
            if (disposed || IsBusy || Availability == StoreAvailability.Loading) return;
            Notice = ShopNotice.None;
            store.Initialize(Product.Id);
        }

        public void Buy()
        {
            if (disposed || !CanBuy) return;
            IsBusy = true;
            Notice = ShopNotice.None;
            Changed?.Invoke();
            store.Purchase();
        }

        public void Retry()
        {
            if (disposed || !CanRetry) return;
            if (retryPurchase != null && Availability == StoreAvailability.Ready) Fulfill(retryPurchase);
            else Initialize();
        }

        private void Fulfill(StorePurchase purchase)
        {
            if (disposed) return;
            IsBusy = false;
            if (purchase == null || purchase.ProductId != Product.Id || string.IsNullOrWhiteSpace(purchase.TransactionId))
            {
                Notice = ShopNotice.Failed;
                TrackFailure("invalid_order", "fulfillment");
                Changed?.Invoke();
                return;
            }

            retryPurchase = purchase;
            var before = economy.CaptureProgress();
            bool granted;
            try
            {
                granted = economy.TryGrantPurchase(purchase.TransactionId, Product.Coins);
                // Balance and transaction ID must reach the same durable snapshot before confirming.
                if (!save())
                {
                    economy.TryRestoreProgress(before);
                    Notice = ShopNotice.SaveFailed;
                    TrackFailure("save_failed", "fulfillment", purchase.TransactionId);
                    Changed?.Invoke();
                    return;
                }
            }
            catch (Exception)
            {
                economy.TryRestoreProgress(before);
                Notice = ShopNotice.SaveFailed;
                TrackFailure("save_failed", "fulfillment", purchase.TransactionId);
                Changed?.Invoke();
                return;
            }

            // Replayed orders and confirmation retries must not count as additional revenue.
            if (granted)
                analytics?.Track("purchase_succeeded", ("product_id", Product.Id),
                    ("transaction_id", purchase.TransactionId), ("coins", Product.Coins), ("balance", economy.Balance));
            Notice = ShopNotice.Purchased;
            Changed?.Invoke();
            store.Confirm(purchase.TransactionId);
            if (Notice != ShopNotice.ConfirmationFailed) retryPurchase = null;
        }

        private void OnAvailabilityChanged()
        {
            if (Availability == StoreAvailability.Unavailable)
            {
                if (IsBusy) TrackFailure("store_unavailable", "purchase");
                IsBusy = false;
            }
            Changed?.Invoke();
        }

        private void OnFailure(PurchaseIssue issue)
        {
            // Deferred is still waiting for payment, not a failed purchase.
            if (issue != PurchaseIssue.Deferred)
                TrackFailure(issue == PurchaseIssue.Cancelled ? "cancelled" :
                    issue == PurchaseIssue.ConfirmationFailed ? "confirmation_failed" : "failed",
                    issue == PurchaseIssue.ConfirmationFailed ? "confirmation" : "purchase",
                    issue == PurchaseIssue.ConfirmationFailed ? retryPurchase?.TransactionId : null);
            IsBusy = false;
            Notice = issue switch
            {
                PurchaseIssue.Cancelled => ShopNotice.Cancelled,
                PurchaseIssue.Deferred => ShopNotice.Deferred,
                PurchaseIssue.ConfirmationFailed => ShopNotice.ConfirmationFailed,
                _ => ShopNotice.Failed
            };
            Changed?.Invoke();
        }

        private void TrackFailure(string reason, string stage, string transactionId = null) =>
            analytics?.Track("purchase_failed", ("product_id", Product.Id), ("reason", reason),
                ("stage", stage), ("transaction_id", transactionId));

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            store.AvailabilityChanged -= OnAvailabilityChanged;
            store.PurchasePending -= Fulfill;
            store.PurchaseFailed -= OnFailure;
            store.Dispose();
        }
    }
}
