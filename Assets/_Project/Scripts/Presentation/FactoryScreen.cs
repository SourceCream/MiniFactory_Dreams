using System;
using MiniFactory.Analytics;
using MiniFactory.Configuration;
using MiniFactory.Economy;
using MiniFactory.Persistence;
using MiniFactory.Purchasing;
using MiniFactory.Shop;
using UnityEngine;
using UnityEngine.UI;

namespace MiniFactory.Presentation
{
    public sealed class FactoryScreen : MonoBehaviour
    {
        [SerializeField] private EconomyConfig config;
        [SerializeField] private BoostConfig boostConfig;
        [SerializeField] private ShopConfig shopConfig;
        [SerializeField] private Text balanceLabel;
        [SerializeField] private Text incomeLabel;
        [SerializeField] private MachineCard[] cards;
        [SerializeField] private OfflineIncomePopup offlinePopup;
        [SerializeField] private BoostView boostView;
        [SerializeField] private Button shopButton;
        [SerializeField] private ShopPopup shopPopup;
        [SerializeField, Min(1)] private float autosaveIntervalSeconds = 5;

        public FactoryEconomy Session { get; private set; }
        public OfflineIncome LastOfflineIncome { get; private set; }
        public ShopSession Shop { get; private set; }
        private float refreshTimer;
        private float saveTimer;
        private FactorySaveStore saveStore;
        private AnalyticsService analytics;
        private OfflineIncomeTracker offlineTracker;
        private Func<long> utcNow;
        private bool paused;
        private bool unfocused;
        private bool isAway;

        private void Awake()
        {
            if (Session == null)
                Initialize(new FactorySaveStore(Application.persistentDataPath, message => Debug.LogWarning(message, this)));
            if (Shop == null) InitializeShop(new UnityIapGateway(shopConfig.UseFakeStore));
        }

        // Passing no store keeps editor previews and economy/UI tests independent of player files.
        public void Initialize(FactorySaveStore store = null, Func<long> utcClock = null,
            AnalyticsService analyticsService = null)
        {
            if (Session != null) return;
            saveStore = store;
            analytics = analyticsService ?? new AnalyticsService(new IAnalyticsProvider[] { new DebugAnalyticsProvider() },
                exception => Debug.LogWarning("Analytics provider failed: " + exception.Message));
            utcNow = utcClock ?? (() => DateTime.UtcNow.Ticks);
            Session = config.CreateEconomy(boostConfig.ToDefinition());
            long savedAt = 0;
            bool restored = saveStore != null && saveStore.TryLoad(Session, out savedAt);
            offlineTracker = new OfflineIncomeTracker(savedAt);
            analytics.Track("game_started", ("restored_save", restored), ("balance", Session.Balance),
                ("machine_count", Session.Machines.Count), ("platform", Application.platform.ToString()),
                ("app_version", Application.version));
            ResumeProduction();
            foreach (MachineCard card in cards)
                card.Bind(Session.GetMachine(card.MachineId), BuyOrUpgrade);
            boostView?.Bind(Session.Boost, ActivateBoost);
            RefreshViews();
            SaveProgress(false);
            ShowOfflineIncome();
        }

        // Explicit injection keeps editor previews and unit tests away from the live SDK.
        public void InitializeShop(IStoreGateway gateway)
        {
            if (Shop != null) return;
            Shop = new ShopSession(shopConfig.ToDefinition(), gateway, Session,
                () => saveStore == null || saveStore.TrySave(Session, offlineTracker.AccountedThroughUtcTicks), analytics);
            Shop.Changed += RefreshViews;
            shopPopup.Bind(Shop);
            shopButton.onClick.AddListener(shopPopup.Show);
            Shop.Initialize();
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
            ResumeProduction();
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
            double cost = wasUnlocked ? machine.UpgradeCost : machine.Definition.UnlockCost;
            bool succeeded = wasUnlocked
                ? Session.TryUpgrade(card.MachineId)
                : Session.TryUnlock(card.MachineId);
            if (!succeeded) return;

            SaveProgress(false);
            analytics.Track(wasUnlocked ? "machine_upgraded" : "machine_unlocked",
                ("machine_id", card.MachineId), ("level", machine.Level), ("cost", cost),
                ("production_per_second", machine.ProductionPerSecond), ("balance", Session.Balance));
            RefreshViews();
            card.PlayPurchaseFeedback(Session.Boost.MultiplierAt(offlineTracker.AccountedThroughUtcTicks));
        }

        private void SettleActiveProduction()
        {
            if (isAway) return;
            var previousBoost = CurrentBoost;
            offlineTracker.AdvanceActive(Session, utcNow());
            TrackFinishedBoost(previousBoost, false);
        }

        private (long Start, long End, double Multiplier) CurrentBoost =>
            (Session.Boost.StartedAtUtcTicks, Session.Boost.EndsAtUtcTicks, Session.Boost.ActiveMultiplier);

        private void ResumeProduction()
        {
            var previousBoost = CurrentBoost;
            LastOfflineIncome = offlineTracker.Resume(Session, utcNow(), config.MaximumOfflineSeconds);
            TrackFinishedBoost(previousBoost, true);
            if (LastOfflineIncome.CreditedSeconds > 0)
                analytics.Track("offline_income_applied", ("away_seconds", LastOfflineIncome.AwaySeconds),
                    ("credited_seconds", LastOfflineIncome.CreditedSeconds), ("coins", LastOfflineIncome.Coins),
                    ("capped", LastOfflineIncome.IsCapped), ("balance", Session.Balance));
        }

        private void TrackFinishedBoost((long Start, long End, double Multiplier) previous, bool offline)
        {
            if (previous.End == 0 || Session.Boost.EndsAtUtcTicks != 0) return;
            analytics.Track("boost_finished", ("duration_seconds", (previous.End - previous.Start) / (double)TimeSpan.TicksPerSecond),
                ("multiplier", previous.Multiplier), ("completed_offline", offline));
        }

        private void ActivateBoost()
        {
            if (isAway) return;
            SettleActiveProduction();
            if (!Session.TryActivateBoost(offlineTracker.AccountedThroughUtcTicks)) return;
            SaveProgress(false);
            analytics.Track("boost_started", ("duration_seconds", Session.Boost.Definition.DurationSeconds),
                ("multiplier", Session.Boost.ActiveMultiplier),
                ("ends_at_utc", new DateTime(Session.Boost.EndsAtUtcTicks, DateTimeKind.Utc).ToString("O")));
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
            if (Shop != null)
            {
                shopButton.onClick.RemoveListener(shopPopup.Show);
                shopPopup.Unbind();
                Shop.Changed -= RefreshViews;
                Shop.Dispose();
            }
            boostView?.Unbind();
            if (cards == null) return;
            foreach (MachineCard card in cards)
                if (card != null) card.Unbind();
        }
    }
}
