using System;

namespace MiniFactory.Shop
{
    public sealed class ShopProductDefinition
    {
        public string Id { get; }
        public string DisplayName { get; }
        public int Coins { get; }

        public ShopProductDefinition(string id, string displayName, int coins)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Product ID is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Product name is required.", nameof(displayName));
            if (coins <= 0) throw new ArgumentOutOfRangeException(nameof(coins));
            Id = id;
            DisplayName = displayName;
            Coins = coins;
        }
    }
}
