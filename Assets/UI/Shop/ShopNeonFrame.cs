using UnityEngine;

[RequireComponent(typeof(CanvasRenderer))]
public class ShopNeonFrame : UnityEngine.UI.MaskableGraphic
{
    [SerializeField] private float corner = 14f;
    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
    {
        vh.Clear(); var r = rectTransform.rect;
        var p = new Vector2[] {
            new Vector2(r.xMin + corner, r.yMax), new Vector2(r.xMax, r.yMax),
            new Vector2(r.xMax, r.yMin + corner), new Vector2(r.xMax - corner, r.yMin),
            new Vector2(r.xMin, r.yMin), new Vector2(r.xMin, r.yMax - corner)
        };
        for (int layer = 2; layer >= 0; layer--)
        for (int i = 0; i < p.Length; i++)
        {
            Vector2 a = p[i], b = p[(i + 1) % p.Length];
            Vector2 n = new Vector2(-(b - a).y, (b - a).x).normalized * (layer == 0 ? 1 : 3 + layer * 2);
            Color c = color; c.a *= layer == 0 ? 1 : 0.06f;
            int start = vh.currentVertCount;
            vh.AddVert(a - n, c, Vector2.zero); vh.AddVert(a + n, c, Vector2.zero);
            vh.AddVert(b + n, c, Vector2.zero); vh.AddVert(b - n, c, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
