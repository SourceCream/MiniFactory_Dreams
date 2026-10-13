using System;
using MiniFactory.Economy;
using UnityEngine;

namespace MiniFactory.Configuration
{
    [CreateAssetMenu(fileName = "EconomyConfig", menuName = "Mini Factory/Economy Config")]
    public sealed class EconomyConfig : ScriptableObject
    {
        [SerializeField, Min(0)] private double initialBalance;
        [SerializeField, Min(0)] private double maximumOfflineSeconds = 28800;
        [SerializeField] private bool boostEnabled = true;
        [SerializeField, Min(0.1f)] private double boostDurationSeconds = 60;
        [SerializeField, Min(1.01f)] private double boostMultiplier = 2;
        [Tooltip("The first machine starts unlocked at level 1. All others start locked.")]
        [SerializeField] private MachineConfig[] machines = Array.Empty<MachineConfig>();

        public double MaximumOfflineSeconds => maximumOfflineSeconds;

        public FactoryEconomy CreateEconomy()
        {
            if (machines == null)
                throw new InvalidOperationException("Machine configuration is missing.");

            var definitions = new MachineDefinition[machines.Length];
            for (int i = 0; i < machines.Length; i++)
            {
                if (machines[i] == null)
                    throw new InvalidOperationException("Machine configuration cannot contain empty entries.");
                definitions[i] = machines[i].ToDefinition();
            }

            return new FactoryEconomy(definitions, initialBalance,
                new BoostDefinition(boostEnabled, boostDurationSeconds, boostMultiplier));
        }

        [Serializable]
        private sealed class MachineConfig
        {
            [SerializeField] private string id;
            [SerializeField] private string displayName;
            [SerializeField, Min(0)] private double unlockCost;
            [SerializeField, Min(0.001f)] private double baseProductionPerSecond = 1;
            [SerializeField, Min(0.001f)] private double baseUpgradeCost = 10;
            [SerializeField, Min(1)] private double upgradeCostMultiplier = 1.5;

            public MachineDefinition ToDefinition()
            {
                return new MachineDefinition(id, displayName, unlockCost,
                    baseProductionPerSecond, baseUpgradeCost, upgradeCostMultiplier);
            }
        }
    }
}
