using System;
using MiniFactory.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace MiniFactory.Presentation
{
    public sealed class OfflineIncomePopup : MonoBehaviour
    {
        [SerializeField] private Text durationLabel;
        [SerializeField] private Text earningsLabel;
        [SerializeField] private Text limitLabel;
        [SerializeField] private Button continueButton;

        public bool IsVisible => gameObject.activeSelf;

        private void OnDisable() => continueButton.onClick.RemoveListener(Dismiss);

        public void Show(OfflineIncome income)
        {
            continueButton.onClick.RemoveListener(Dismiss);
            continueButton.onClick.AddListener(Dismiss);
            durationLabel.text = FormatDuration(income.AwaySeconds);
            earningsLabel.text = "+" + EconomyDisplay.Coins(income.Coins);
            limitLabel.text = income.IsCapped ? "Лимит дохода: " + FormatDuration(income.CreditedSeconds) : string.Empty;
            gameObject.SetActive(true);
        }

        // The session already credited and saved this income. Dismissing is only a UI action.
        public void Dismiss() => gameObject.SetActive(false);

        public static string FormatDuration(double seconds)
        {
            long total = (long)Math.Max(0, Math.Floor(seconds));
            long days = total / 86400;
            long hours = total / 3600 % 24;
            long minutes = total / 60 % 60;
            long remainder = total % 60;
            if (days > 0) return days + " д " + hours + " ч";
            if (hours > 0) return hours + " ч " + minutes + " мин";
            if (minutes > 0) return minutes + " мин " + remainder + " с";
            return remainder + " с";
        }
    }
}
