using System;

namespace MiniFactory.Shop
{
    public enum StoreAvailability { Unavailable, Loading, Ready }
    public enum PurchaseIssue { Cancelled, Failed, Deferred, ConfirmationFailed }

    public sealed class StorePurchase
    {
        public string ProductId { get; }
        public string TransactionId { get; }
        public StorePurchase(string productId, string transactionId)
        {
            ProductId = productId;
            TransactionId = transactionId;
        }
    }

    // SDK types remain in the adapter. A pending purchase is paid but not yet fulfilled.
    public interface IStoreGateway : IDisposable
    {
        StoreAvailability Availability { get; }
        string PriceLabel { get; }
        event Action AvailabilityChanged;
        event Action<StorePurchase> PurchasePending;
        event Action<PurchaseIssue> PurchaseFailed;
        void Initialize(string productId);
        void Purchase();
        void Confirm(string transactionId);
    }
}
