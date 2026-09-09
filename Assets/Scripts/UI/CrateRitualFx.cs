using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 开箱仪式 mesh 特效：软光晕 / 脉冲环 / 暗角（避免硬放射光柱）。
/// </summary>
public static class CrateRitualFx
{
    public static void BindRayHost(VisualElement host, System.Func<float> intensity, System.Func<Color> color)
    {
        if (host == null) return;
        host.pickingMode = PickingMode.Ignore;
        host.style.overflow = Overflow.Visible;
        host.generateVisualContent += ctx =>
        {
            var rect = host.contentRect;
            if (rect.width < 2f || rect.height < 2f) return;
            DrawHalo(ctx, rect, intensity?.Invoke() ?? 0f, color?.Invoke() ?? Color.white);
        };
        host.schedule.Execute(() => host.MarkDirtyRepaint()).Every(16);
    }

    public static void BindVignettePulse(VisualElement host, System.Func<float> strength)
    {
        if (host == null) return;
        host.pickingMode = PickingMode.Ignore;
        host.generateVisualContent += ctx =>
        {
            var rect = host.contentRect;
            if (rect.width < 2f || rect.height < 2f) return;
            float s = strength?.Invoke() ?? 0.55f;
            DrawVignette(ctx, rect, s);
        };
        host.schedule.Execute(() => host.MarkDirtyRepaint()).Every(32);
    }

    private static void DrawHalo(MeshGenerationContext ctx, Rect rect, float intensity, Color color)
    {
        if (intensity <= 0.01f) return;

        var center = new Vector2(rect.width * 0.5f, rect.height * 0.56f);
        Color warm = Color.Lerp(color, new Color(1f, 0.95f, 0.75f), 0.45f);

        // 多层软光晕（由外到内渐亮）
        float baseR = Mathf.Lerp(70f, 210f, intensity);
        DrawSoftDisc(ctx, center, baseR * 1.15f, WithAlpha(warm, intensity * 0.05f));
        DrawSoftDisc(ctx, center, baseR * 0.78f, WithAlpha(warm, intensity * 0.10f));
        DrawSoftDisc(ctx, center, baseR * 0.48f, WithAlpha(Color.Lerp(warm, Color.white, 0.35f), intensity * 0.18f));
        DrawSoftDisc(ctx, center, baseR * 0.22f, WithAlpha(Color.white, intensity * 0.28f));

        // 蓄力脉冲环：随强度外扩
        float ringPulse = 0.55f + 0.45f * Mathf.Sin(Time.realtimeSinceStartup * (2.2f + intensity * 3.5f));
        float ringR = Mathf.Lerp(36f, 150f, intensity) * (0.85f + 0.15f * ringPulse);
        float ringW = Mathf.Lerp(10f, 22f, intensity);
        DrawSoftRing(ctx, center, ringR, ringR + ringW, WithAlpha(warm, intensity * 0.22f * ringPulse));

        if (intensity > 0.55f)
        {
            float ring2 = ringR * 0.62f;
            DrawSoftRing(ctx, center, ring2, ring2 + ringW * 0.7f,
                WithAlpha(Color.Lerp(warm, Color.white, 0.4f), (intensity - 0.55f) * 0.35f));
        }
    }

    private static void DrawVignette(MeshGenerationContext ctx, Rect rect, float strength)
    {
        strength = Mathf.Clamp01(strength);
        var c = new Vector2(rect.width * 0.5f, rect.height * 0.52f);
        float outer = Mathf.Max(rect.width, rect.height) * 0.72f;
        float inner = outer * (0.28f + (1f - strength) * 0.18f);
        DrawSoftRing(ctx, c, inner, outer, new Color(0.02f, 0.03f, 0.06f, 0.15f + strength * 0.55f));
    }

    private static void DrawSoftDisc(MeshGenerationContext ctx, Vector2 center, float radius, Color color)
    {
        const int seg = 20;
        // 中心不透明 → 边缘透明，做假渐变
        var edge = WithAlpha(color, 0f);
        var mesh = ctx.Allocate(seg + 1, seg * 3);
        mesh.SetNextVertex(new Vertex { position = new Vector3(center.x, center.y, Vertex.nearZ), tint = color });
        for (int i = 0; i < seg; i++)
        {
            float a = i / (float)seg * Mathf.PI * 2f;
            mesh.SetNextVertex(new Vertex
            {
                position = new Vector3(center.x + Mathf.Cos(a) * radius, center.y + Mathf.Sin(a) * radius, Vertex.nearZ),
                tint = edge
            });
        }
        for (int i = 0; i < seg; i++)
        {
            mesh.SetNextIndex(0);
            mesh.SetNextIndex((ushort)(i + 1));
            mesh.SetNextIndex((ushort)(i + 1 < seg ? i + 2 : 1));
        }
    }

    private static void DrawSoftRing(MeshGenerationContext ctx, Vector2 center, float inner, float outer, Color color)
    {
        const int seg = 28;
        var mesh = ctx.Allocate((seg + 1) * 2, seg * 6);
        var outerC = WithAlpha(color, 0f);
        for (int i = 0; i <= seg; i++)
        {
            float a = i / (float)seg * Mathf.PI * 2f;
            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            mesh.SetNextVertex(new Vertex
            {
                position = new Vector3(center.x + dir.x * inner, center.y + dir.y * inner, Vertex.nearZ),
                tint = color
            });
            mesh.SetNextVertex(new Vertex
            {
                position = new Vector3(center.x + dir.x * outer, center.y + dir.y * outer, Vertex.nearZ),
                tint = outerC
            });
        }
        for (int i = 0; i < seg; i++)
        {
            ushort i0 = (ushort)(i * 2);
            ushort i1 = (ushort)(i0 + 1);
            ushort i2 = (ushort)(i0 + 2);
            ushort i3 = (ushort)(i0 + 3);
            mesh.SetNextIndex(i0); mesh.SetNextIndex(i2); mesh.SetNextIndex(i1);
            mesh.SetNextIndex(i1); mesh.SetNextIndex(i2); mesh.SetNextIndex(i3);
        }
    }

    private static Color WithAlpha(Color c, float a)
    {
        c.a = a;
        return c;
    }
}

public static class CrateEasing
{
    public static float OutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    public static float OutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);

    public static float InCubic(float t) => t * t * t;

    public static float InOutSine(float t) => -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f;

    public static float InOutCubic(float t) =>
        t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
}
