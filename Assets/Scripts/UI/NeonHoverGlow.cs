using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 主菜单科技框按钮：软边发光 + 切角装饰（对齐生图）。
/// Active 仍用 Glitch Press（translate），不用 scale。
/// </summary>
public static class NeonHoverGlow
{
    private const string HoverClass = "neon-glow-hot";
    private const string BoundClass = "neon-glow-bound";
    private const string HostClass = "neon-glow-host";

    public static void AttachAll(VisualElement root)
    {
        if (root == null) return;
        root.Query<Button>(className: "menu-btn").ForEach(Attach);
    }

    public static void Attach(Button button)
    {
        if (button == null || button.ClassListContains(BoundClass))
            return;

        button.AddToClassList(BoundClass);

        var parent = button.parent;
        if (parent == null) return;

        int index = parent.IndexOf(button);
        var host = new VisualElement();
        host.AddToClassList(HostClass);
        host.pickingMode = PickingMode.Position;
        host.style.overflow = Overflow.Visible;

        parent.Insert(index, host);
        host.Add(button);

        host.generateVisualContent += mgc => OnGenerate(mgc, host, button);

        button.RegisterCallback<PointerEnterEvent>(_ =>
        {
            button.AddToClassList(HoverClass);
            host.MarkDirtyRepaint();
        });
        button.RegisterCallback<PointerLeaveEvent>(_ =>
        {
            button.RemoveFromClassList(HoverClass);
            host.MarkDirtyRepaint();
        });
        button.RegisterCallback<GeometryChangedEvent>(_ => host.MarkDirtyRepaint());
        host.schedule.Execute(() => host.MarkDirtyRepaint()).StartingIn(0);
    }

    private static void OnGenerate(MeshGenerationContext mgc, VisualElement host, Button button)
    {
        var rect = button.layout;
        if (rect.width < 2f || rect.height < 2f) return;

        bool hot = button.ClassListContains(HoverClass);
        bool muted = button.ClassListContains("btn-outline-muted");
        Color glow = ResolveGlowColor(button);

        float maxExpand = muted
            ? (hot ? 4f : 2.2f)
            : (hot ? 8f : 5.5f);
        float peakAlpha = muted
            ? (hot ? 0.16f : 0.08f)
            : (hot ? 0.32f : 0.18f);

        for (int i = 1; i <= 3; i++)
        {
            float t = i / 3f;
            float expand = maxExpand * t;
            float alpha = peakAlpha * (1f - t * 0.55f);
            DrawSoftFrame(mgc, rect, expand, WithAlpha(glow, alpha));
        }

        // 科技切角：四角 L 形 bracket + 小方块
        float accentA = muted ? (hot ? 0.55f : 0.32f) : (hot ? 0.95f : 0.7f);
        DrawTechCorners(mgc, rect, WithAlpha(glow, accentA), hot ? 12f : 10f);
    }

    private static void DrawTechCorners(MeshGenerationContext mgc, Rect rect, Color color, float arm)
    {
        float t = 1.6f;
        float inset = 3f;
        float x0 = rect.xMin + inset;
        float y0 = rect.yMin + inset;
        float x1 = rect.xMax - inset;
        float y1 = rect.yMax - inset;

        // TL
        DrawSolidRect(mgc, new Rect(x0, y0, arm, t), color);
        DrawSolidRect(mgc, new Rect(x0, y0, t, arm), color);
        DrawSolidRect(mgc, new Rect(x0 + arm * 0.55f, y0 - 1.2f, 3f, 3f), color);

        // TR
        DrawSolidRect(mgc, new Rect(x1 - arm, y0, arm, t), color);
        DrawSolidRect(mgc, new Rect(x1 - t, y0, t, arm), color);
        DrawSolidRect(mgc, new Rect(x1 - arm * 0.55f - 3f, y0 - 1.2f, 3f, 3f), color);

        // BL
        DrawSolidRect(mgc, new Rect(x0, y1 - t, arm, t), color);
        DrawSolidRect(mgc, new Rect(x0, y1 - arm, t, arm), color);
        DrawSolidRect(mgc, new Rect(x0 + arm * 0.55f, y1 - 1.8f, 3f, 3f), color);

        // BR
        DrawSolidRect(mgc, new Rect(x1 - arm, y1 - t, arm, t), color);
        DrawSolidRect(mgc, new Rect(x1 - t, y1 - arm, t, arm), color);
        DrawSolidRect(mgc, new Rect(x1 - arm * 0.55f - 3f, y1 - 1.8f, 3f, 3f), color);

        // 侧边小缺口线（仿生图 > < 机械感）
        float midY = rect.yMin + rect.height * 0.5f;
        float notch = 5f;
        DrawSolidRect(mgc, new Rect(x0 - 1f, midY - notch * 0.5f, t, notch), WithAlpha(color, color.a * 0.65f));
        DrawSolidRect(mgc, new Rect(x1 - t + 1f, midY - notch * 0.5f, t, notch), WithAlpha(color, color.a * 0.65f));
    }

    private static Color ResolveGlowColor(Button button)
    {
        if (button.ClassListContains("btn-outline-magenta") || button.ClassListContains("campaign-btn"))
            return new Color(1f, 0.15f, 1f, 1f);
        if (button.ClassListContains("btn-outline-yellow") || button.ClassListContains("shop-btn"))
            return new Color(1f, 0.88f, 0.2f, 1f);
        if (button.ClassListContains("btn-outline-muted"))
            return new Color(0.75f, 0.8f, 0.85f, 1f);
        return new Color(0.15f, 1f, 1f, 1f);
    }

    private static Color WithAlpha(Color c, float a)
    {
        c.a = Mathf.Clamp01(a);
        return c;
    }

    private static void DrawSolidRect(MeshGenerationContext mgc, Rect rect, Color color)
    {
        if (rect.width < 0.5f || rect.height < 0.5f) return;
        var mesh = mgc.Allocate(4, 6);
        float z = Vertex.nearZ;
        mesh.SetNextVertex(new Vertex { position = new Vector3(rect.xMin, rect.yMin, z), tint = color });
        mesh.SetNextVertex(new Vertex { position = new Vector3(rect.xMax, rect.yMin, z), tint = color });
        mesh.SetNextVertex(new Vertex { position = new Vector3(rect.xMax, rect.yMax, z), tint = color });
        mesh.SetNextVertex(new Vertex { position = new Vector3(rect.xMin, rect.yMax, z), tint = color });
        mesh.SetNextIndex(0);
        mesh.SetNextIndex(1);
        mesh.SetNextIndex(2);
        mesh.SetNextIndex(0);
        mesh.SetNextIndex(2);
        mesh.SetNextIndex(3);
    }

    private static void DrawSoftFrame(MeshGenerationContext mgc, Rect inner, float expand, Color color)
    {
        if (expand < 0.35f) return;

        var outer = new Rect(
            inner.x - expand,
            inner.y - expand,
            inner.width + expand * 2f,
            inner.height + expand * 2f);

        Color innerC = color;
        Color outerC = WithAlpha(color, 0f);

        DrawGradQuad(mgc,
            new Vector2(outer.xMin, outer.yMin), new Vector2(outer.xMax, outer.yMin),
            new Vector2(inner.xMax, inner.yMin), new Vector2(inner.xMin, inner.yMin),
            outerC, outerC, innerC, innerC);
        DrawGradQuad(mgc,
            new Vector2(inner.xMin, inner.yMax), new Vector2(inner.xMax, inner.yMax),
            new Vector2(outer.xMax, outer.yMax), new Vector2(outer.xMin, outer.yMax),
            innerC, innerC, outerC, outerC);
        DrawGradQuad(mgc,
            new Vector2(outer.xMin, outer.yMin), new Vector2(inner.xMin, inner.yMin),
            new Vector2(inner.xMin, inner.yMax), new Vector2(outer.xMin, outer.yMax),
            outerC, innerC, innerC, outerC);
        DrawGradQuad(mgc,
            new Vector2(inner.xMax, inner.yMin), new Vector2(outer.xMax, outer.yMin),
            new Vector2(outer.xMax, outer.yMax), new Vector2(inner.xMax, inner.yMax),
            innerC, outerC, outerC, innerC);
    }

    private static void DrawGradQuad(
        MeshGenerationContext mgc,
        Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3,
        Color c0, Color c1, Color c2, Color c3)
    {
        var mesh = mgc.Allocate(4, 6);
        float z = Vertex.nearZ;
        mesh.SetNextVertex(new Vertex { position = new Vector3(p0.x, p0.y, z), tint = c0 });
        mesh.SetNextVertex(new Vertex { position = new Vector3(p1.x, p1.y, z), tint = c1 });
        mesh.SetNextVertex(new Vertex { position = new Vector3(p2.x, p2.y, z), tint = c2 });
        mesh.SetNextVertex(new Vertex { position = new Vector3(p3.x, p3.y, z), tint = c3 });
        mesh.SetNextIndex(0);
        mesh.SetNextIndex(1);
        mesh.SetNextIndex(2);
        mesh.SetNextIndex(0);
        mesh.SetNextIndex(2);
        mesh.SetNextIndex(3);
    }
}
