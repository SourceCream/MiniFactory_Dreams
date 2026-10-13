using MiniFactory.Shop;
using UnityEngine;
using UnityEngine.UI;

namespace MiniFactory.Presentation
{
    public sealed class ShopPopup : MonoBehaviour
    {
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text amountLabel;
        [SerializeField] private Text actionLabel;
        [SerializeField] private Text statusLabel;
        [SerializeField] private Button actionButton;
        [SerializeField] private Button closeButton;
        private ShopSession session;
        public bool IsVisible => gameObject.activeSelf;

        public void Bind(ShopSession model)
        {
            Unbind();
            session = model;
            titleLabel.text = model.Product.DisplayName;
            amountLabel.text = EconomyDisplay.Coins(model.Product.Coins);
            actionButton.onClick.AddListener(Act);
            closeButton.onClick.AddListener(Close);
            session.Changed += Refresh;
            Refresh();
        }

        public void Show()
        {
            if (session == null) return;
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
            Refresh();
        }

        public void Close()
        {
            if (session == null || !session.IsBusy) gameObject.SetActive(false);
        }

        private void Act()
        {
            if (session.CanRetry) session.Retry();
            else session.Buy();
        }

        private void Refresh()
        {
            closeButton.interactable = !session.IsBusy;
            actionButton.interactable = session.CanBuy || session.CanRetry;
            actionLabel.text = session.IsBusy ? "Покупка…" :
                session.Availability == StoreAvailability.Loading ? "Загрузка…" :
                session.CanRetry ? "Повторить" : session.PriceLabel;
            statusLabel.text = session.Availability == StoreAvailability.Unavailable ? "Магазин недоступен. Попробуй ещё раз." :
                session.Notice switch
                {
                    ShopNotice.Purchased => "+" + EconomyDisplay.Coins(session.Product.Coins) + " монет зачислено",
                    ShopNotice.Cancelled => "Покупка отменена",
                    ShopNotice.Failed => "Покупка не прошла. Попробуй ещё раз.",
                    ShopNotice.SaveFailed => "Не удалось сохранить покупку. Повтори попытку.",
                    ShopNotice.Deferred => "Покупка ожидает подтверждения",
                    ShopNotice.ConfirmationFailed => "Монеты сохранены. Подтверди покупку повторно.",
                    _ => string.Empty
                };
        }

        public void Unbind()
        {
            if (session != null) session.Changed -= Refresh;
            actionButton.onClick.RemoveListener(Act);
            closeButton.onClick.RemoveListener(Close);
            session = null;
        }
    }
}
