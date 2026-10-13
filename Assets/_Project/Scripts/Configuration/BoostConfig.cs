using MiniFactory.Economy;
using UnityEngine;

namespace MiniFactory.Configuration
{
    [CreateAssetMenu(fileName = "BoostConfig", menuName = "Mini Factory/Boost Config")]
    public sealed class BoostConfig : ScriptableObject
    {
        [SerializeField] private bool boostEnabled = true;
        [SerializeField, Min(0.1f)] private double durationSeconds = 60;
        [SerializeField, Min(1.01f)] private double multiplier = 2;

        public BoostDefinition ToDefinition() => new BoostDefinition(boostEnabled, durationSeconds, multiplier);
    }
}
