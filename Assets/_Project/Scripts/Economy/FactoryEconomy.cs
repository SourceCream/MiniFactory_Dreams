using System;
using System.Collections.Generic;

namespace MiniFactory.Economy
{
    /// <summary>Owns balance and machine progress; the caller supplies elapsed seconds.</summary>
    public sealed class FactoryEconomy
    {
        public double Balance { get; private set; }
        public IReadOnlyList<MachineState> Machines { get; }

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

        public FactoryEconomy(IReadOnlyList<MachineDefinition> definitions, double initialBalance = 0)
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

        private void EnsureProductionCanIncrease(double increase)
        {
            if (double.IsInfinity(TotalProductionPerSecond + increase))
                throw new OverflowException("The upgrade would exceed the supported production range.");
        }

        private static void RequireNonNegativeFinite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
                throw new ArgumentOutOfRangeException(name, "Expected a finite, non-negative value.");
        }
    }
}
