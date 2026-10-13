using System;

namespace MiniFactory.Economy
{
    /// <summary>Detached player progress. Prices and production still come from the current configuration.</summary>
    [Serializable]
    public sealed class FactoryProgress
    {
        public double Balance = -1;
        public MachineProgress[] Machines;
    }

    [Serializable]
    public sealed class MachineProgress
    {
        public string Id;
        public int Level = -1;
    }
}
