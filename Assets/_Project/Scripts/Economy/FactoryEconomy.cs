using System;
using System.Collections.Generic;

namespace MiniFactory.Economy
{
    /// <summary>Owns balance and machine progress; the caller supplies elapsed seconds.</summary>
    public sealed class FactoryEconomy
    {
        public double Balance { get; private set; }
        public IReadOnlyList<MachineState> Machines { get; }
        public ProductionBoost Boost { get; }

        public double TotalProductionPerSecond
        {
            get
            {
                double total = 0;
                foreach (MachineState machine in Machines)
                    total += machine.ProductionPerSecond;
                return total;
            }
        }

        public FactoryEconomy(IReadOnlyList<MachineDefinition> definitions, double initialBalance = 0,
            BoostDefinition boostDefinition = null)
        {
            if (definitions == null)
                throw new ArgumentNullException(nameof(definitions));
            if (definitions.Count == 0)
                throw new ArgumentException("The factory needs at least one machine.", nameof(definitions));
            RequireNonNegativeFinite(initialBalance, nameof(initialBalance));

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var machines = new MachineState[definitions.Count];
            for (int i = 0; i < definitions.Count; i++)
            {
                MachineDefinition definition = definitions[i];
                if (definition == null || !ids.Add(definition.Id))
                    throw new ArgumentException("Machine definitions must be non-null and have unique IDs.", nameof(definitions));

                machines[i] = new MachineState(definition, i == 0 ? 1 : 0);
            }

            Machines = Array.AsReadOnly(machines);
            Balance = initialBalance;
            Boost = new ProductionBoost(boostDefinition);
        }

        public MachineState GetMachine(string id)
        {
            foreach (MachineState machine in Machines)
            {
                if (string.Equals(machine.Definition.Id, id, StringComparison.Ordinal))
                    return machine;
            }

            throw new ArgumentException("Unknown machine ID: " + id, nameof(id));
        }

        /// <summary>Base production for a supplied duration. Use AdvanceBetween for real-time boosted production.</summary>
        public double Advance(double elapsedSeconds)
        {
            RequireNonNegativeFinite(elapsedSeconds, nameof(elapsedSeconds));
            double earned = TotalProductionPerSecond * elapsedSeconds;
            double newBalance = Balance + earned;
            if (double.IsInfinity(newBalance))
                throw new OverflowException("Production would exceed the supported balance range.");

            Balance = newBalance;
            return earned;
        }

        public double AdvanceBetween(long fromUtcTicks, long toUtcTicks)
        {
            double earned = Advance(Boost.EffectiveProductionSeconds(fromUtcTicks, toUtcTicks));
            Boost.FinishIfExpired(toUtcTicks);
            return earned;
        }

        public double ProductionPerSecondAt(long utcTicks) => TotalProductionPerSecond * Boost.MultiplierAt(utcTicks);

        public bool TryActivateBoost(long utcTicks)
        {
            if (double.IsInfinity(TotalProductionPerSecond * Boost.Definition.Multiplier)) return false;
            return Boost.TryActivate(utcTicks);
        }

        public bool TryUnlock(string id)
        {
            MachineState machine = GetMachine(id);
            if (machine.IsUnlocked || Balance < machine.Definition.UnlockCost)
                return false;

            EnsureProductionCanIncrease(machine.Definition.BaseProductionPerSecond);
            Balance -= machine.Definition.UnlockCost;
            machine.Level = 1;
            return true;
        }

        public bool TryUpgrade(string id)
        {
            MachineState machine = GetMachine(id);
            if (!machine.IsUnlocked || machine.Level == int.MaxValue)
                return false;

            double cost = machine.UpgradeCost;
            if (Balance < cost)
                return false;

            EnsureProductionCanIncrease(machine.Definition.BaseProductionPerSecond);
            Balance -= cost;
            machine.Level++;
            return true;
        }

        public FactoryProgress CaptureProgress()
        {
            var progress = new FactoryProgress
            {
                Balance = Balance,
                Boost = Boost.Capture(),
                Machines = new MachineProgress[Machines.Count]
            };
            for (int i = 0; i < Machines.Count; i++)
                progress.Machines[i] = new MachineProgress
                {
                    Id = Machines[i].Definition.Id,
                    Level = Machines[i].Level
                };
            return progress;
        }

        public bool TryRestoreProgress(FactoryProgress progress)
        {
            if (progress == null || progress.Machines == null || progress.Machines.Length == 0 ||
                double.IsNaN(progress.Balance) || double.IsInfinity(progress.Balance) || progress.Balance < 0 ||
                !ProductionBoost.IsValid(progress.Boost))
                return false;

            var savedLevels = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (MachineProgress machine in progress.Machines)
            {
                if (machine == null || string.IsNullOrWhiteSpace(machine.Id) || machine.Level < 0 ||
                    !savedLevels.TryAdd(machine.Id, machine.Level))
                    return false;
            }

            var levels = new int[Machines.Count];
            double totalProduction = 0;
            bool hasUnlockedMachine = false;
            for (int i = 0; i < Machines.Count; i++)
            {
                // Stable IDs survive config reordering. New machines use their starting state;
                // removed machines are ignored. No tuning data is restored from a save.
                levels[i] = savedLevels.TryGetValue(Machines[i].Definition.Id, out int level)
                    ? level : (i == 0 ? 1 : 0);
                hasUnlockedMachine |= levels[i] > 0;
                totalProduction += Machines[i].Definition.GetProductionPerSecond(levels[i]);
            }
            double multiplier = Boost.Definition.Enabled && progress.Boost != null ? progress.Boost.Multiplier : 1;
            if (!hasUnlockedMachine || double.IsInfinity(totalProduction * multiplier)) return false;

            // Commit only after validating the whole snapshot, so rejected data changes nothing.
            Balance = progress.Balance;
            for (int i = 0; i < Machines.Count; i++)
                Machines[i].Level = levels[i];
            Boost.Restore(progress.Boost);
            return true;
        }

        private void EnsureProductionCanIncrease(double increase)
        {
            if (double.IsInfinity((TotalProductionPerSecond + increase) * Boost.ActiveMultiplier))
                throw new OverflowException("The upgrade would exceed the supported production range.");
        }

        private static void RequireNonNegativeFinite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
                throw new ArgumentOutOfRangeException(name, "Expected a finite, non-negative value.");
        }
    }
}
