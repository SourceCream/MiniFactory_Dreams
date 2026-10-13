using System;

namespace MiniFactory.Economy
{
    /// <summary>Detached player progress. Prices and production still come from the current configuration.</summary>
    [Serializable]
    public sealed class FactoryProgress
    {
        public double Balance = -1;
        public MachineProgress[] Machines;
        public BoostProgress Boost;
    }

    [Serializable]
    public sealed class MachineProgress
    {
        public string Id;
        public int Level = -1;
    }

    [Serializable]
    public sealed class BoostProgress
    {
        public long StartedAtUtcTicks;
        public long EndsAtUtcTicks;
        // Preserve the terms of the already activated boost across configuration updates.
        public double Multiplier = 1;
    }
}
