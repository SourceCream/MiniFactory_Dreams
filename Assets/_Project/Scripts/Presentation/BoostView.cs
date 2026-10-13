using System;
using MiniFactory.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace MiniFactory.Presentation
{
    public sealed class BoostView : MonoBehaviour
    {
        [SerializeField] private Button actionButton;
        [SerializeField] private Text actionLabel;
        [SerializeField] private Text timerLabel;
        [SerializeField] private FactoryGraphic background;
        [SerializeField] private Color readyColor = new Color32(175, 230, 190, 255);
        [SerializeField] private Color activeColor = new Color32(243, 199, 94, 255);
        private ProductionBoost boost;
        private Action onActivate;

        public void Bind(ProductionBoost state, Action activate)
        {
            Unbind();
            boost = state;
            onActivate = activate;
            actionButton.onClick.AddListener(Activate);
        }

        public void Refresh(long utcTicks)
        {
            gameObject.SetActive(boost.Definition.Enabled);
            if (!boost.Definition.Enabled) return;
            bool active = boost.IsActive(utcTicks);
            double multiplier = active ? boost.ActiveMultiplier : boost.Definition.Multiplier;
            actionLabel.text = (active ? "Ускорение ×" : "Ускорить ×") + EconomyDisplay.Rate(multiplier);
            timerLabel.text = FormatTimer(active ? boost.RemainingSeconds(utcTicks) : boost.Definition.DurationSeconds);
            background.color = active ? activeColor : readyColor;
            actionButton.interactable = !active;
        }

        public void Unbind()
        {
            actionButton.onClick.RemoveListener(Activate);
            onActivate = null;
        }

        private void Activate() => onActivate?.Invoke();

        public static string FormatTimer(double seconds)
        {
            long total = (long)Math.Max(0, Math.Ceiling(seconds));
            return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
        }
    }
}
