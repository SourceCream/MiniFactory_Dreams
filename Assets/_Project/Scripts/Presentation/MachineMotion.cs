using UnityEngine;

namespace MiniFactory.Presentation
{
    // Animation is decorative. Only FactoryEconomy produces and spends money.
    public sealed class MachineMotion : MonoBehaviour
    {
        [SerializeField] private RectTransform piston;
        [SerializeField] private float pistonTravel = 28;
        [SerializeField] private RectTransform[] gears;
        [SerializeField] private RectTransform[] beltItems;
        [SerializeField] private float beltStart = 15;
        [SerializeField] private float beltEnd = 266;
        [SerializeField] private RectTransform coin;
        [SerializeField] private CanvasGroup coinOpacity;
        [SerializeField] private float cycleSeconds = 2.4f;

        public bool Running { get; set; }
        public float SpeedMultiplier { get; set; } = 1;
        private float phase;
        private Vector2 pistonOrigin;
        private Vector2 coinOrigin;

        private void Awake()
        {
            if (piston != null) pistonOrigin = piston.anchoredPosition;
            if (coin != null) coinOrigin = coin.anchoredPosition;
        }

        private void Update()
        {
            if (!Running)
            {
                if (coinOpacity != null) coinOpacity.alpha = 0;
                return;
            }

            phase = Mathf.Repeat(phase + Time.deltaTime * SpeedMultiplier / cycleSeconds, 1);
            if (piston != null)
                piston.anchoredPosition = pistonOrigin + Vector2.down *
                    (0.5f - 0.5f * Mathf.Cos(phase * Mathf.PI * 2)) * pistonTravel;
            for (int i = 0; i < gears.Length; i++)
                gears[i].localRotation = Quaternion.Euler(0, 0, phase * 360 * (i % 2 == 0 ? 1 : -1));
            for (int i = 0; i < beltItems.Length; i++)
            {
                Vector2 position = beltItems[i].anchoredPosition;
                position.x = Mathf.Lerp(beltStart, beltEnd, Mathf.Repeat(phase + (float)i / beltItems.Length, 1));
                beltItems[i].anchoredPosition = position;
            }

            if (coin == null) return;
            coin.anchoredPosition = coinOrigin + Vector2.up * phase * 40;
            coinOpacity.alpha = Mathf.Sin(phase * Mathf.PI) * 0.9f;
        }
    }
}
