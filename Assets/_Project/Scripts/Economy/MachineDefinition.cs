using System;

namespace MiniFactory.Economy
{
    /// <summary>Immutable tuning data. Contains no player progress or Unity dependencies.</summary>
    public sealed class MachineDefinition
    {
        public string Id { get; }
        public string DisplayName { get; }
        public double UnlockCost { get; }
        public double BaseProductionPerSecond { get; }
        public double BaseUpgradeCost { get; }
        public double UpgradeCostMultiplier { get; }

        public MachineDefinition(string id, string displayName, double unlockCost,
            double baseProductionPerSecond, double baseUpgradeCost, double upgradeCostMultiplier)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A machine must have a stable ID.", nameof(id));
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("A machine must have a display name.", nameof(displayName));

            RequireFiniteAtLeast(unlockCost, 0, nameof(unlockCost));
            RequireFiniteAtLeast(baseProductionPerSecond, 0, nameof(baseProductionPerSecond));
            RequireFiniteAtLeast(baseUpgradeCost, 0, nameof(baseUpgradeCost));
            RequireFiniteAtLeast(upgradeCostMultiplier, 1, nameof(upgradeCostMultiplier));
            if (baseProductionPerSecond == 0 || baseUpgradeCost == 0)
                throw new ArgumentException("Production and base upgrade cost must be positive.");

            Id = id;
            DisplayName = displayName;
            UnlockCost = unlockCost;
            BaseProductionPerSecond = baseProductionPerSecond;
            BaseUpgradeCost = baseUpgradeCost;
            UpgradeCostMultiplier = upgradeCostMultiplier;
        }

        public double GetProductionPerSecond(int level)
        {
            if (level < 0)
                throw new ArgumentOutOfRangeException(nameof(level));

            return BaseProductionPerSecond * level;
        }

        public double GetUpgradeCost(int currentLevel)
        {
            if (currentLevel < 1)
                throw new ArgumentOutOfRangeException(nameof(currentLevel));

            return Math.Ceiling(BaseUpgradeCost * Math.Pow(UpgradeCostMultiplier, currentLevel - 1));
        }

        private static void RequireFiniteAtLeast(double value, double minimum, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < minimum)
                throw new ArgumentOutOfRangeException(name, "Expected a finite value in the allowed range.");
        }
    }
}
