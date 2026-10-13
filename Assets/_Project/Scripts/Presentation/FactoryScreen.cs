using MiniFactory.Configuration;
using MiniFactory.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace MiniFactory.Presentation
{
    public sealed class FactoryScreen : MonoBehaviour
    {
        [SerializeField] private EconomyConfig config;
        [SerializeField] private Text balanceLabel;
        [SerializeField] private Text incomeLabel;
        [SerializeField] private MachineCard[] cards;

        public FactoryEconomy Session { get; private set; }
        private float refreshTimer;
        private bool paused;
        private bool skipNextFrame;

        private void Awake() => Initialize();

        public void Initialize()
        {
            if (Session != null) return;
            Session = config.CreateEconomy();
            foreach (MachineCard card in cards)
                card.Bind(Session.GetMachine(card.MachineId), BuyOrUpgrade);
            RefreshViews();
        }

        private void Update()
        {
            if (paused) return;
            if (skipNextFrame)
            {
                skipNextFrame = false;
                return;
            }

            Session.Advance(Time.deltaTime);
            refreshTimer += Time.deltaTime;
            if (refreshTimer < 0.1f) return;
            refreshTimer = 0;
            RefreshViews();
        }

        private void OnApplicationPause(bool isPaused)
        {
            paused = isPaused;
            // Background income belongs to the later save/offline system.
            skipNextFrame = !isPaused;
        }

        private void BuyOrUpgrade(MachineCard card)
        {
            MachineState machine = Session.GetMachine(card.MachineId);
            bool wasUnlocked = machine.IsUnlocked;
            bool succeeded = wasUnlocked
                ? Session.TryUpgrade(card.MachineId)
                : Session.TryUnlock(card.MachineId);
            if (!succeeded) return;

            RefreshViews();
            card.PlayPurchaseFeedback();
        }

        public void RefreshViews()
        {
            balanceLabel.text = EconomyDisplay.Coins(Session.Balance);
            incomeLabel.text = "+" + EconomyDisplay.Rate(Session.TotalProductionPerSecond) + " /с";
            foreach (MachineCard card in cards)
                card.Refresh(Session.Balance);
        }

        private void OnDestroy()
        {
            if (cards == null) return;
            foreach (MachineCard card in cards)
                if (card != null) card.Unbind();
        }
    }
}
