namespace MiniFactory.Economy
{
    public readonly struct OfflineIncome
    {
        public double AwaySeconds { get; }
        public double CreditedSeconds { get; }
        public double Coins { get; }
        public bool IsCapped => AwaySeconds > CreditedSeconds;

        public OfflineIncome(double awaySeconds, double creditedSeconds, double coins)
        {
            AwaySeconds = awaySeconds;
            CreditedSeconds = creditedSeconds;
            Coins = coins;
        }
    }
}
