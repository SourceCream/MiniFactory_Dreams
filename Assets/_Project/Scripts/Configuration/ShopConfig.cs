using MiniFactory.Shop;
using UnityEngine;

namespace MiniFactory.Configuration
{
    [CreateAssetMenu(fileName = "ShopConfig", menuName = "Mini Factory/Shop Config")]
    public sealed class ShopConfig : ScriptableObject
    {
        [SerializeField] private string productId = "coins_pack_small";
        [SerializeField] private string displayName = "Набор монет";
        [SerializeField, Min(1)] private int coins = 1000;
        [Tooltip("Use Unity IAP's test store in both Editor and Android builds; no real charge.")]
        [SerializeField] private bool useFakeStore = true;

        public bool UseFakeStore => useFakeStore;
        public ShopProductDefinition ToDefinition() => new ShopProductDefinition(productId, displayName, coins);
    }
}
