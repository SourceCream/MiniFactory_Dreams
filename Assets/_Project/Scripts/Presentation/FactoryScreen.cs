using System;
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
        [SerializeField] private OfflineIncomePopup offlinePopup;
        [SerializeField] private BoostView boostView;
        [SerializeField, Min(1)] private float autosaveIntervalSeconds = 5;

        public FactoryEconomy Session { get; private set; }
        public OfflineIncome LastOfflineIncome { get; private set; }
        private float refreshTimer;
        private float saveTimer;
        private FactorySaveStore saveStore;
        private OfflineIncomeTracker offlineTracker;
        private Func<long> utcNow;
        private bool paused;
        private bool unfocused;
        private bool isAway;

        private void Awake()
        {
            if (Session == null)
                Initialize(new FactorySaveStore(Application.persistentDataPath, message => Debug.LogWarning(message, this)));
        }

        // Passing no store keeps editor previews and economy/UI tests independent of player files.
        public void Initialize(FactorySaveStore store = null, Func<long> utcClock = null)
        {
            if (Session != null) return;
            saveStore = store;
            utcNow = utcClock ?? (() => DateTime.UtcNow.Ticks);
            Session = config.CreateEconomy();
            long savedAt = 0;
            saveStore?.TryLoad(Session, out savedAt);
            offlineTracker = new OfflineIncomeTracker(savedAt);
            LastOfflineIncome = offlineTracker.Resume(Session, utcNow(), config.MaximumOfflineSeconds);
            foreach (MachineCard card in cards)
                card.Bind(Session.GetMachine(card.MachineId), BuyOrUpgrade);
            boostView?.Bind(Session.Boost, ActivateBoost);
            RefreshViews();
            SaveProgress(false);
            ShowOfflineIncome();
        }

        private void Update()
        {
            if (isAway) return;
            bool hadBoost = Session.Boost.EndsAtUtcTicks != 0;
            SettleActiveProduction();
            saveTimer += Time.unscaledDeltaTime;
            if ((hadBoost && Session.Boost.EndsAtUtcTicks == 0) || saveTimer >= Mathf.Max(1, autosaveIntervalSeconds))
                SaveProgress(false);
            refreshTimer += Time.deltaTime;
            if (refreshTimer < 0.1f) return;
            refreshTimer = 0;
            RefreshViews();
        }

        private void OnApplicationPause(bool isPaused)
        {
            paused = isPaused;
            UpdateApplicationState();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            unfocused = !hasFocus;
            UpdateApplicationState();
        }

        private void UpdateApplicationState()
        {
            if (Session == null) return;
            bool nowAway = paused || unfocused;
            if (nowAway == isAway) return;

            if (nowAway)
            {
                SettleActiveProduction();
                isAway = true;
                SaveProgress(false);
                return;
            }

            isAway = false;
            LastOfflineIncome = offlineTracker.Resume(Session, utcNow(), config.MaximumOfflineSeconds);
            SaveProgress(false);
            RefreshViews();
            ShowOfflineIncome();
        }

        private void ShowOfflineIncome()
        {
            // Sub-second focus flickers still earn income but do not interrupt play with a dialog.
            if (LastOfflineIncome.AwaySeconds >= 1)
                offlinePopup?.Show(LastOfflineIncome);
        }

        private void OnApplicationQuit() => SaveProgress();

        private void SaveProgress(bool updateActiveTime = true)
        {
            if (Session != null && offlineTracker != null)
            {
                // Quit/destroy can arrive long after pause. Keep the departure time in that case.
                if (updateActiveTime) SettleActiveProduction();
                saveStore?.TrySave(Session, offlineTracker.AccountedThroughUtcTicks);
            }
            saveTimer = 0;
        }

        private void BuyOrUpgrade(MachineCard card)
        {
            if (isAway) return;
            SettleActiveProduction();
            MachineState machine = Session.GetMachine(card.MachineId);
            bool wasUnlocked = machine.IsUnlocked;
            bool succeeded = wasUnlocked
                ? Session.TryUpgrade(card.MachineId)
                : Session.TryUnlock(card.MachineId);
            if (!succeeded) return;

            SaveProgress(false);
            RefreshViews();
            card.PlayPurchaseFeedback(Session.Boost.MultiplierAt(offlineTracker.AccountedThroughUtcTicks));
        }

        private void SettleActiveProduction()
        {
            if (!isAway) offlineTracker.AdvanceActive(Session, utcNow());
        }

        private void ActivateBoost()
        {
            if (isAway) return;
            SettleActiveProduction();
            if (!Session.TryActivateBoost(offlineTracker.AccountedThroughUtcTicks)) return;
            SaveProgress(false);
            RefreshViews();
        }

        public void RefreshViews()
        {
            balanceLabel.text = EconomyDisplay.Coins(Session.Balance);
            long displayTime = Math.Max(utcNow(), offlineTracker.AccountedThroughUtcTicks);
            double multiplier = Session.Boost.MultiplierAt(displayTime);
            incomeLabel.text = "+" + EconomyDisplay.Rate(Session.ProductionPerSecondAt(displayTime)) + " /с";
            foreach (MachineCard card in cards)
                card.Refresh(Session.Balance, multiplier);
            boostView?.Refresh(displayTime);
        }

        private void OnDestroy()
        {
            SaveProgress();
            boostView?.Unbind();
            if (cards == null) return;
            foreach (MachineCard card in cards)
                if (card != null) card.Unbind();
        }
    }
}
