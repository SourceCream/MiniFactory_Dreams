using System;
using System.Collections.Generic;
using System.Linq;
using MiniFactory.Shop;
using UnityEngine;
using UnityEngine.Purchasing;

namespace MiniFactory.Purchasing
{
    // Only this adapter knows Unity IAP orders, products and callbacks.
    public sealed class UnityIapGateway : IStoreGateway
    {
        private readonly bool useFakeStore;
        private readonly Dictionary<string, PendingOrder> pending = new Dictionary<string, PendingOrder>();
        private StoreController controller;
        private string productId;
        private Product product;
        private bool disposed;
        public StoreAvailability Availability { get; private set; }
        public string PriceLabel { get; private set; } = string.Empty;
        public event Action AvailabilityChanged;
        public event Action<StorePurchase> PurchasePending;
        public event Action<PurchaseIssue> PurchaseFailed;

        public UnityIapGateway(bool useFakeStore) => this.useFakeStore = useFakeStore;

        public async void Initialize(string id)
        {
            if (disposed || Availability == StoreAvailability.Loading) return;
            productId = id;
            SetAvailability(StoreAvailability.Loading);
            try
            {
                if (controller == null)
                {
                    controller = UnityIAPServices.StoreController(useFakeStore ? "fake" : null);
                    controller.OnStoreConnected += OnConnected;
                    controller.OnStoreDisconnected += OnDisconnected;
                    controller.OnProductsFetched += OnProducts;
                    controller.OnProductsFetchFailed += OnProductsFailed;
                    controller.OnPurchasesFetched += OnPurchases;
                    controller.OnPurchasesFetchFailed += OnPurchasesFailed;
                    controller.OnPurchasePending += OnPending;
                    controller.OnPurchaseFailed += OnFailed;
                    controller.OnPurchaseDeferred += OnDeferred;
                    controller.OnPurchaseConfirmed += OnConfirmed;
                    controller.ProcessPendingOrdersOnPurchasesFetched(false);
                    controller.SetStoreReconnectionRetryPolicyOnDisconnection(null);
                }
                await controller.Connect();
                if (disposed) return;
                controller.FetchProductsWithNoRetries(new List<ProductDefinition> { new ProductDefinition(id, ProductType.Consumable) });
            }
            catch (Exception exception) { Unavailable(exception.Message); }
        }

        public void Purchase()
        {
            if (disposed || Availability != StoreAvailability.Ready || product == null)
            {
                Unavailable("Product is not ready.");
                return;
            }
            try { controller.PurchaseProduct(product); }
            catch (Exception exception)
            {
                Debug.LogWarning("IAP purchase failed: " + exception.Message);
                PurchaseFailed?.Invoke(PurchaseIssue.Failed);
            }
        }

        public void Confirm(string transactionId)
        {
            if (disposed || !pending.TryGetValue(transactionId, out PendingOrder order)) return;
            try { controller.ConfirmPurchase(order); }
            catch (Exception exception)
            {
                Debug.LogWarning("IAP confirmation failed: " + exception.Message);
                PurchaseFailed?.Invoke(PurchaseIssue.ConfirmationFailed);
            }
        }

        private void OnConnected() { }
        private void OnDisconnected(StoreConnectionFailureDescription failure) => Unavailable(failure.message);
        private void OnProductsFailed(ProductFetchFailed failure) => Unavailable(failure.FailureReason);
        private void OnPurchasesFailed(PurchasesFetchFailureDescription failure) => Unavailable(failure.Message);

        private void OnProducts(List<Product> products)
        {
            product = products.FirstOrDefault(p => p.definition.id == productId && p.availableToPurchase);
            if (product == null || string.IsNullOrWhiteSpace(product.metadata?.localizedPriceString))
            {
                Unavailable("The coin pack is unavailable.");
                return;
            }
            PriceLabel = product.metadata.localizedPriceString;
            controller.FetchPurchases();
        }

        private void OnPurchases(Orders orders)
        {
            SetAvailability(StoreAvailability.Ready);
            // Retry unconfirmed orders as well as newly fetched orders. Delivery is idempotent.
            foreach (PendingOrder order in pending.Values.Concat(orders.PendingOrders).ToArray()) OnPending(order);
        }

        private string TransactionKey(Order order) => (useFakeStore ? "fake:" : "store:") + order.Info.TransactionID;

        private void OnPending(PendingOrder order)
        {
            if (disposed) return;
            var items = order.CartOrdered.Items().ToArray();
            if (items.Length != 1 || items[0].Product.definition.id != productId ||
                items[0].Quantity != 1 || string.IsNullOrWhiteSpace(order.Info.TransactionID))
            {
                Debug.LogWarning("IAP returned an unsupported order; it remains unconfirmed.");
                PurchaseFailed?.Invoke(PurchaseIssue.Failed);
                return;
            }
            string key = TransactionKey(order);
            pending[key] = order;
            PurchasePending?.Invoke(new StorePurchase(productId, key));
        }

        private void OnFailed(FailedOrder order) => PurchaseFailed?.Invoke(
            order.FailureReason == PurchaseFailureReason.UserCancelled ? PurchaseIssue.Cancelled : PurchaseIssue.Failed);
        private void OnDeferred(DeferredOrder order) => PurchaseFailed?.Invoke(PurchaseIssue.Deferred);
        private void OnConfirmed(Order order)
        {
            if (order is FailedOrder)
                PurchaseFailed?.Invoke(PurchaseIssue.ConfirmationFailed);
            else if (order is ConfirmedOrder)
                pending.Remove(TransactionKey(order));
        }

        private void Unavailable(string reason)
        {
            if (disposed) return;
            Debug.LogWarning("IAP unavailable: " + reason);
            SetAvailability(StoreAvailability.Unavailable);
        }

        private void SetAvailability(StoreAvailability value)
        {
            if (disposed) return;
            Availability = value;
            AvailabilityChanged?.Invoke();
        }

        public void Dispose()
        {
            disposed = true;
            if (controller == null) return;
            controller.OnStoreConnected -= OnConnected;
            controller.OnStoreDisconnected -= OnDisconnected;
            controller.OnProductsFetched -= OnProducts;
            controller.OnProductsFetchFailed -= OnProductsFailed;
            controller.OnPurchasesFetched -= OnPurchases;
            controller.OnPurchasesFetchFailed -= OnPurchasesFailed;
            controller.OnPurchasePending -= OnPending;
            controller.OnPurchaseFailed -= OnFailed;
            controller.OnPurchaseDeferred -= OnDeferred;
            controller.OnPurchaseConfirmed -= OnConfirmed;
        }
    }
}
