using UnityEngine;
using UnityEngine.UI;

namespace MiniFactory.Presentation
{
    // Resolution-independent rounded shapes for panels, buttons and machine parts.
    [AddComponentMenu("Mini Factory/Factory Graphic")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FactoryGraphic : MaskableGraphic
    {
        [SerializeField, Min(0)] private float cornerRadius = 12;
        public float CornerRadius
        {
            get => cornerRadius;
            set { cornerRadius = value; SetVerticesDirty(); }
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect r = GetPixelAdjustedRect();
            float radius = Mathf.Min(cornerRadius, Mathf.Min(r.width, r.height) * 0.5f);
            const int steps = 8;
            const int count = 4 * (steps + 1);
            mesh.AddVert(r.center, color, Vector2.zero);
            float feather = canvas != null ? 0.8f / canvas.scaleFactor : 0.8f;
            var clear = new Color(color.r, color.g, color.b, 0);
            for (int corner = 0; corner < 4; corner++)
            {
                var center = new Vector2(corner < 2 ? r.xMax - radius : r.xMin + radius,
                    corner == 0 || corner == 3 ? r.yMax - radius : r.yMin + radius);
                for (int step = 0; step <= steps; step++)
                {
                    float angle = (90 - corner * 90 - step * 90f / steps) * Mathf.Deg2Rad;
                    var normal = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    Vector2 edge = center + normal * radius;
                    mesh.AddVert(edge, color, Vector2.zero);
                    mesh.AddVert(edge + normal * feather, clear, Vector2.zero);
                }
            }
            for (int i = 0; i < count; i++)
            {
                int a = 1 + i * 2;
                int b = 1 + ((i + 1) % count) * 2;
                mesh.AddTriangle(0, a, b);
                mesh.AddTriangle(a, a + 1, b + 1);
                mesh.AddTriangle(a, b + 1, b);
            }
        }
    }
}
