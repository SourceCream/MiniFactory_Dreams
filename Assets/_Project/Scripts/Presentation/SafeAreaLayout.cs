using UnityEngine;

namespace MiniFactory.Presentation
{
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaLayout : MonoBehaviour
    {
        [SerializeField] private RectTransform content;
        [SerializeField] private Vector2 designSize = new Vector2(900, 1600);
        private Rect lastSafeArea;
        private Vector2 lastScreenSize;
        private Vector2 lastParentSize;

        private void OnEnable() => Apply();
        private void Update()
        {
            var parent = (RectTransform)transform.parent;
            if (lastSafeArea != Screen.safeArea || lastScreenSize != new Vector2(Screen.width, Screen.height)
                || lastParentSize != parent.rect.size)
                Apply();
        }

        public void Apply()
        {
            if (content == null || transform.parent == null || Screen.width <= 0 || Screen.height <= 0) return;
            var rect = (RectTransform)transform;
            var parent = (RectTransform)transform.parent;
            Rect safe = Screen.safeArea;
            if (safe.width <= 0 || safe.height <= 0) safe = new Rect(0, 0, Screen.width, Screen.height);
            rect.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            rect.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Vector2 available = Vector2.Scale(parent.rect.size, rect.anchorMax - rect.anchorMin);
            float scale = Mathf.Min(available.x / designSize.x, available.y / designSize.y);
            content.sizeDelta = designSize;
            content.localScale = Vector3.one * scale;
            lastSafeArea = Screen.safeArea;
            lastScreenSize = new Vector2(Screen.width, Screen.height);
            lastParentSize = parent.rect.size;
        }
    }
}
