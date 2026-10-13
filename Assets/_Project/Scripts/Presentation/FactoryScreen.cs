using MiniFactory.Configuration;
using MiniFactory.Economy;
using MiniFactory.Persistence;
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
        [SerializeField, Min(1)] private float autosaveIntervalSeconds = 5;

        public FactoryEconomy Session { get; private set; }
        private float refreshTimer;
        private float saveTimer;
        private FactorySaveStore saveStore;
        private bool paused;
        private bool unfocused;
        private bool skipNextFrame;

        private void Awake()
        {
            if (Session == null)
                Initialize(new FactorySaveStore(Application.persistentDataPath, message => Debug.LogWarning(message, this)));
        }

        // Passing no store keeps editor previews and economy/UI tests independent of player files.
        public void Initialize(FactorySaveStore store = null)
        {
            if (Session != null) return;
            saveStore = store;
            Session = config.CreateEconomy();
            saveStore?.TryLoad(Session);
            foreach (MachineCard card in cards)
                card.Bind(Session.GetMachine(card.MachineId), BuyOrUpgrade);
            RefreshViews();
        }

        private void Update()
        {
            if (paused || unfocused) return;
            if (skipNextFrame)
            {
                skipNextFrame = false;
                return;
            }

            Session.Advance(Time.deltaTime);
            saveTimer += Time.unscaledDeltaTime;
            if (saveTimer >= Mathf.Max(1, autosaveIntervalSeconds)) SaveProgress();
            refreshTimer += Time.deltaTime;
            if (refreshTimer < 0.1f) return;
            refreshTimer = 0;
            RefreshViews();
        }

        private void OnApplicationPause(bool isPaused)
        {
            paused = isPaused;
            if (isPaused) SaveProgress();
            // Background income belongs to the later save/offline system.
            skipNextFrame = !isPaused;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            unfocused = !hasFocus;
            if (!hasFocus) SaveProgress();
            skipNextFrame = hasFocus;
        }

        private void OnApplicationQuit() => SaveProgress();

        private void SaveProgress()
        {
            if (Session != null) saveStore?.TrySave(Session);
            saveTimer = 0;
        }

        private void BuyOrUpgrade(MachineCard card)
        {
            MachineState machine = Session.GetMachine(card.MachineId);
            bool wasUnlocked = machine.IsUnlocked;
            bool succeeded = wasUnlocked
                ? Session.TryUpgrade(card.MachineId)
                : Session.TryUnlock(card.MachineId);
            if (!succeeded) return;

            SaveProgress();
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
            SaveProgress();
            if (cards == null) return;
            foreach (MachineCard card in cards)
                if (card != null) card.Unbind();
        }
    }
}
