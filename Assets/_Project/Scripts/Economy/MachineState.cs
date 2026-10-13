using System;

namespace MiniFactory.Economy
{
    public sealed class MachineState
    {
        public MachineDefinition Definition { get; }
        public int Level { get; internal set; }
        public bool IsUnlocked => Level > 0;
        public double ProductionPerSecond => Definition.GetProductionPerSecond(Level);

        // A locked machine previews the price of its first upgrade after unlocking.
        public double UpgradeCost => Definition.GetUpgradeCost(Math.Max(1, Level));

        internal MachineState(MachineDefinition definition, int level)
        {
            Definition = definition;
            Level = level;
        }
    }
}
