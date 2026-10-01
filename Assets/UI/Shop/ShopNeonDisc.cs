using UnityEngine;

/// <summary>Independent, editable UI mesh: segmented light disc with layered soft glow.</summary>
[ExecuteAlways]
[RequireComponent(typeof(CanvasRenderer))]
public class ShopNeonDisc : UnityEngine.UI.MaskableGraphic
{
    [SerializeField, Range(0.1f, 0.6f)] private float perspective = 0.28f;
    [SerializeField, Range(1f, 8f)] private float lineWidth = 3f;
    [SerializeField] private float rotationSpeed = 12f;
    private float phase;
    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
    {
        vh.Clear();
        float radius = rectTransform.rect.width * 0.46f;
        for (int glow = 3; glow >= 0; glow--)
        {
            Ring(vh, radius, lineWidth + glow * 8f, glow == 0 ? 0.95f : 0.035f, false);
            Ring(vh, radius * 0.78f, lineWidth + glow * 5f, glow == 0 ? 0.45f : 0.025f, true);
        }
        Ring(vh, radius * 0.96f, 9f, 0.85f, true);
        Ring(vh, radius * 0.55f, 1f, 0.15f, false);
    }
    private void Ring(UnityEngine.UI.VertexHelper vh, float radius, float width, float alpha, bool segmented)
    {
        const int steps = 192;
        Vector2 center = rectTransform.rect.center;
        for (int i = 0; i < steps; i++)
        {
            float degrees = i * 360f / steps;
            if (segmented && Mathf.Repeat(degrees + phase, 90f) > 42f) continue;
            float a = degrees * Mathf.Deg2Rad;
            float b = (degrees + 360f / steps) * Mathf.Deg2Rad;
            var c = color; c.a *= alpha;
            int start = vh.currentVertCount;
            vh.AddVert(center + Point(a, radius - width / 2), c, Vector2.zero);
            vh.AddVert(center + Point(a, radius + width / 2), c, Vector2.zero);
            vh.AddVert(center + Point(b, radius + width / 2), c, Vector2.zero);
            vh.AddVert(center + Point(b, radius - width / 2), c, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
        }
    }
    private Vector2 Point(float angle, float radius) => new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * perspective);
    private void Update()
    {
        if (!Application.isPlaying) return;
        phase = Mathf.Repeat(phase + rotationSpeed * Time.unscaledDeltaTime, 360f);
        SetVerticesDirty();
    }
}
