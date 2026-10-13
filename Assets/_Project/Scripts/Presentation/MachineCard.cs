using System;
using MiniFactory.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace MiniFactory.Presentation
{
    public sealed class MachineCard : MonoBehaviour
    {
        [SerializeField] private string machineId;
        [SerializeField] private Text nameLabel;
        [SerializeField] private Text levelLabel;
        [SerializeField] private Text rateLabel;
        [SerializeField] private Text actionLabel;
        [SerializeField] private Text costLabel;
        [SerializeField] private Text productionGainLabel;
        [SerializeField] private Button actionButton;
        [SerializeField] private FactoryGraphic lockedShade;
        [SerializeField] private MachineMotion motion;
        [SerializeField] private CanvasGroup feedback;
        [SerializeField] private Text feedbackLabel;
        [SerializeField] private Color unavailableColor = new Color32(229, 235, 225, 255);
        [SerializeField] private Color lockedUnavailableColor = new Color32(161, 177, 174, 255);

        public string MachineId => machineId;
        private MachineState machine;
        private Action<MachineCard> onAction;
        private float feedbackTime;
        private Vector2 feedbackOrigin;

        public void Bind(MachineState state, Action<MachineCard> action)
        {
            Unbind();
            machine = state;
            onAction = action;
            nameLabel.text = state.Definition.DisplayName;
            feedbackOrigin = ((RectTransform)feedback.transform).anchoredPosition;
            feedback.alpha = 0;
            actionButton.onClick.AddListener(HandleAction);
        }

        public void Unbind()
        {
            actionButton.onClick.RemoveListener(HandleAction);
            onAction = null;
        }

        private void HandleAction() => onAction?.Invoke(this);

        public void Refresh(double balance)
        {
            bool running = machine.IsUnlocked;
            double cost = running ? machine.UpgradeCost : machine.Definition.UnlockCost;
            bool canBuy = balance >= cost && (!running || machine.Level < int.MaxValue);
            rateLabel.text = (running ? "+" : string.Empty) + EconomyDisplay.Rate(machine.ProductionPerSecond) + " /с";
            levelLabel.text = running ? "Ур. " + machine.Level : string.Empty;
            actionLabel.text = running ? "Улучшить" : "Купить";
            costLabel.text = EconomyDisplay.Cost(cost);
            productionGainLabel.text = "+" + EconomyDisplay.Rate(machine.Definition.BaseProductionPerSecond) + " /с";
            lockedShade.enabled = !running;
            var colors = actionButton.colors;
            colors.disabledColor = running ? unavailableColor : lockedUnavailableColor;
            if (actionButton.colors != colors) actionButton.colors = colors;
            actionButton.interactable = canBuy;
            motion.Running = running;
        }

        public void PlayPurchaseFeedback()
        {
            feedbackLabel.text = "+" + EconomyDisplay.Rate(machine.Definition.BaseProductionPerSecond) + " /с";
            feedbackTime = 1.6f;
        }

        private void Update()
        {
            if (feedbackTime <= 0) return;
            feedbackTime = Mathf.Max(0, feedbackTime - Time.deltaTime);
            feedback.alpha = Mathf.Min(1, feedbackTime * 3);
            ((RectTransform)feedback.transform).anchoredPosition = feedbackOrigin +
                Vector2.up * (1.6f - feedbackTime) * 12;
        }
    }
}
