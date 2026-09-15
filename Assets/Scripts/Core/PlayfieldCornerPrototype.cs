using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 挡板角程序化原型：三角 Slingshot + 折线外道轨。
/// 仅用于试手感；定形后再换成美术贴图。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class PlayfieldCornerPrototype : MonoBehaviour
{
    [Header("Slingshot Triangle (left world pts; right mirrored)")]
    public Vector2 slingTopOuter = new Vector2(-3.95f, -4.5f);
    public Vector2 slingBottomOuter = new Vector2(-3.95f, -5.7f);
    public Vector2 slingInnerTip = new Vector2(-2.55f, -5.2f);
    public Color slingColor = new Color(1f, 0.55f, 0.05f, 0.95f);

    [Header("Outlane Rail Polyline (left; right mirrored)")]
    // 轨内侧喂向挡板（inlane），墙侧外道保留风险；railTop 贴近弹垫封死红圈口袋
    // 相对 inlane 基准整体外移 0.4（靠墙），再略下移
    public Vector2 railTop = new Vector2(-3.95f, -6.00f);
    public Vector2 railBend = new Vector2(-3.60f, -6.65f);
    public Vector2 railBottom = new Vector2(-3.15f, -7.10f);
    public float railHalfWidth = 0.14f;
    public Color railColor = new Color(0.05f, 0.55f, 0.7f, 0.9f);

    [Header("Table Art (optional)")]
    [Tooltip("左三角垫；右件自动 Flip X")]
    public Sprite slingshotSprite;
    [Tooltip("左外道轨；右件自动 Flip X")]
    public Sprite railGuideSprite;
    public Material artMaterial;
    [Tooltip("精灵相对碰撞包围盒的放大系数，略大于 1 可盖住碰撞")]
    public float artFitPadding = 1.0f;

    [Header("Refs")]
    public PhysicsMaterial2D physicsMaterial;
    public bool hideLegacyRails = true;

    [ContextMenu("Rebuild Corner Prototype")]
    public void Rebuild()
    {
        ResolvePhysics();
        if (hideLegacyRails)
        {
            SetActiveIfExists("Rail_LeftOuter", false);
            SetActiveIfExists("Rail_RightOuter", false);
        }

        ApplySlingshot("Slingshot_Left", false);
        ApplySlingshot("Slingshot_Right", true);
        ApplyRail("RailProto_Left", false);
        ApplyRail("RailProto_Right", true);
    }

    private void Awake() => Rebuild();
    private void OnEnable() => Rebuild();

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!isActiveAndEnabled) return;
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            Rebuild();
        };
    }
#endif

    private void ResolvePhysics()
    {
#if UNITY_EDITOR
        if (physicsMaterial == null)
        {
            var wall = GameObject.Find("Wall_Left");
            var col = wall != null ? wall.GetComponent<Collider2D>() : null;
            if (col != null) physicsMaterial = col.sharedMaterial;
        }
#endif
    }

    private void ApplySlingshot(string name, bool mirrorX)
    {
        var go = GameObject.Find(name);
        if (go == null)
        {
            go = new GameObject(name);
            go.AddComponent<Slingshot>();
        }

        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        go.transform.localScale = Vector3.one;

        var pts = new[]
        {
            Mirror(slingTopOuter, mirrorX),
            Mirror(slingBottomOuter, mirrorX),
            Mirror(slingInnerTip, mirrorX),
        };

        var box = go.GetComponent<BoxCollider2D>();
        if (box != null)
        {
            if (Application.isPlaying) Destroy(box);
            else DestroyImmediate(box);
        }

        Vector2 center = (pts[0] + pts[1] + pts[2]) / 3f;
        for (int i = 0; i < pts.Length; i++)
            pts[i] -= center;

        var poly = go.GetComponent<PolygonCollider2D>();
        if (poly == null) poly = go.AddComponent<PolygonCollider2D>();
        poly.pathCount = 1;
        poly.SetPath(0, pts);
        if (physicsMaterial != null) poly.sharedMaterial = physicsMaterial;

        go.transform.position = center;
        go.transform.localScale = Vector3.one;

        EnsureVisual(go, out var visual, out var sr);
        if (slingshotSprite != null)
        {
            sr.sprite = slingshotSprite;
            sr.flipX = mirrorX;
            sr.color = Color.white;
            sr.sharedMaterial = artMaterial != null ? artMaterial : CyberVisualFactory.UnlitMaterial;
            FitChildSprite(visual, sr, LocalAabb(pts), artFitPadding);
        }
        else
        {
            sr.flipX = false;
            visual.localScale = Vector3.one;
            visual.localPosition = Vector3.zero;
            sr.sprite = BuildPolygonSprite(pts, slingColor, name + "_Proto");
            sr.color = Color.white;
            sr.material = CyberVisualFactory.UnlitMaterial;
        }
        sr.sortingOrder = 2;

        if (go.GetComponent<Slingshot>() == null)
            go.AddComponent<Slingshot>();
    }

    private void ApplyRail(string name, bool mirrorX)
    {
        var t = transform.Find(name);
        GameObject go;
        if (t == null)
        {
            go = new GameObject(name);
            go.transform.SetParent(transform, false);
        }
        else go = t.gameObject;

        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        var center = new List<Vector2>
        {
            Mirror(railTop, mirrorX),
            Mirror(railBend, mirrorX),
            Mirror(railBottom, mirrorX),
        };
        var polyPts = BuildRibbonPolygon(center, railHalfWidth);

        var box = go.GetComponent<BoxCollider2D>();
        if (box != null)
        {
            if (Application.isPlaying) Destroy(box);
            else DestroyImmediate(box);
        }

        var poly = go.GetComponent<PolygonCollider2D>();
        if (poly == null) poly = go.AddComponent<PolygonCollider2D>();

        // 锚到包围中心，方便对齐贴图
        Vector2 c = Vector2.zero;
        for (int i = 0; i < polyPts.Count; i++) c += polyPts[i];
        c /= polyPts.Count;
        for (int i = 0; i < polyPts.Count; i++)
            polyPts[i] -= c;
        for (int i = 0; i < center.Count; i++)
            center[i] -= c;

        poly.pathCount = 1;
        poly.SetPath(0, polyPts.ToArray());
        if (physicsMaterial != null) poly.sharedMaterial = physicsMaterial;
        go.transform.position = c;

        var mf = go.GetComponent<MeshFilter>();
        var mr = go.GetComponent<MeshRenderer>();
        var lr = go.GetComponent<LineRenderer>();

        // 根节点上的旧 SpriteRenderer 关掉（改用 Visual 子物体）
        var rootSr = go.GetComponent<SpriteRenderer>();
        if (rootSr != null) rootSr.enabled = false;

        if (railGuideSprite != null)
        {
            if (mf != null) mf.sharedMesh = null;
            if (mr != null) mr.enabled = false;
            if (lr != null) lr.enabled = false;

            EnsureVisual(go, out var visual, out var sr);
            sr.enabled = true;
            sr.sprite = railGuideSprite;
            sr.flipX = mirrorX;
            sr.color = Color.white;
            sr.sharedMaterial = artMaterial != null ? artMaterial : CyberVisualFactory.UnlitMaterial;
            sr.sortingOrder = 2;
            FitChildSprite(visual, sr, LocalAabb(polyPts.ToArray()), artFitPadding);
        }
        else
        {
            var visualT = go.transform.Find("Visual");
            if (visualT != null)
            {
                var vsr = visualT.GetComponent<SpriteRenderer>();
                if (vsr != null) vsr.enabled = false;
            }

            go.transform.localScale = Vector3.one;
            if (mf == null) mf = go.AddComponent<MeshFilter>();
            if (mr == null) mr = go.AddComponent<MeshRenderer>();
            mr.enabled = true;
            mf.sharedMesh = BuildFilledMesh(polyPts, name + "Mesh");
            mr.sharedMaterial = CyberVisualFactory.UnlitMaterial;
            mr.sortingOrder = 2;

            if (lr == null) lr = go.AddComponent<LineRenderer>();
            lr.enabled = true;
            lr.useWorldSpace = false;
            lr.positionCount = center.Count;
            for (int i = 0; i < center.Count; i++)
                lr.SetPosition(i, center[i]);
            lr.startWidth = railHalfWidth * 1.6f;
            lr.endWidth = railHalfWidth * 1.6f;
            lr.numCornerVertices = 3;
            lr.numCapVertices = 2;
            lr.startColor = railColor;
            lr.endColor = railColor;
            lr.sharedMaterial = CyberVisualFactory.UnlitMaterial;
            lr.sortingOrder = 3;
        }
    }

    private static void EnsureVisual(GameObject root, out Transform visual, out SpriteRenderer sr)
    {
        // 根上旧 SR 关闭，避免叠两层
        var rootSr = root.GetComponent<SpriteRenderer>();
        if (rootSr != null) rootSr.enabled = false;

        visual = root.transform.Find("Visual");
        if (visual == null)
        {
            var vgo = new GameObject("Visual");
            visual = vgo.transform;
            visual.SetParent(root.transform, false);
        }
        sr = visual.GetComponent<SpriteRenderer>();
        if (sr == null) sr = visual.gameObject.AddComponent<SpriteRenderer>();
        sr.enabled = true;
    }

    private static Rect LocalAabb(Vector2[] pts)
    {
        float minX = pts[0].x, maxX = pts[0].x, minY = pts[0].y, maxY = pts[0].y;
        for (int i = 1; i < pts.Length; i++)
        {
            minX = Mathf.Min(minX, pts[i].x);
            maxX = Mathf.Max(maxX, pts[i].x);
            minY = Mathf.Min(minY, pts[i].y);
            maxY = Mathf.Max(maxY, pts[i].y);
        }
        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    private static void FitChildSprite(Transform visual, SpriteRenderer sr, Rect localAabb, float pad)
    {
        if (sr.sprite == null) return;
        var sb = sr.sprite.bounds.size;
        if (sb.x < 0.001f || sb.y < 0.001f) return;
        float sx = localAabb.width / sb.x;
        float sy = localAabb.height / sb.y;
        float s = Mathf.Max(sx, sy) * Mathf.Max(1f, pad);
        visual.localScale = new Vector3(s, s, 1f);
        visual.localPosition = localAabb.center;
    }

    private static Vector2 Mirror(Vector2 leftPt, bool mirrorX)
        => mirrorX ? new Vector2(-leftPt.x, leftPt.y) : leftPt;

    private static List<Vector2> BuildRibbonPolygon(List<Vector2> center, float halfW)
    {
        int n = center.Count;
        var left = new List<Vector2>(n);
        var right = new List<Vector2>(n);
        for (int i = 0; i < n; i++)
        {
            Vector2 tan = i == 0 ? (center[1] - center[0]).normalized
                : i == n - 1 ? (center[n - 1] - center[n - 2]).normalized
                : (center[i + 1] - center[i - 1]).normalized;
            Vector2 nrm = new Vector2(-tan.y, tan.x).normalized;
            left.Add(center[i] - nrm * halfW);
            right.Add(center[i] + nrm * halfW);
        }

        var poly = new List<Vector2>(n * 2);
        poly.AddRange(left);
        for (int i = n - 1; i >= 0; i--)
            poly.Add(right[i]);
        return poly;
    }

    private static Mesh BuildFilledMesh(List<Vector2> poly, string meshName)
    {
        var mesh = new Mesh { name = meshName };
        if (poly == null || poly.Count < 3) return mesh;

        var verts = new Vector3[poly.Count];
        for (int i = 0; i < poly.Count; i++)
            verts[i] = poly[i];

        var tris = new int[(poly.Count - 2) * 3];
        int ti = 0;
        for (int i = 1; i < poly.Count - 1; i++)
        {
            tris[ti++] = 0;
            tris[ti++] = i;
            tris[ti++] = i + 1;
        }

        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Sprite BuildPolygonSprite(Vector2[] localPts, Color fill, string texName)
    {
        float minX = localPts[0].x, maxX = localPts[0].x, minY = localPts[0].y, maxY = localPts[0].y;
        for (int i = 1; i < localPts.Length; i++)
        {
            minX = Mathf.Min(minX, localPts[i].x);
            maxX = Mathf.Max(maxX, localPts[i].x);
            minY = Mathf.Min(minY, localPts[i].y);
            maxY = Mathf.Max(maxY, localPts[i].y);
        }

        float pad = 0.05f;
        minX -= pad; maxX += pad; minY -= pad; maxY += pad;
        float wWorld = Mathf.Max(0.05f, maxX - minX);
        float hWorld = Mathf.Max(0.05f, maxY - minY);
        const float ppu = 64f;
        int tw = Mathf.Clamp(Mathf.CeilToInt(wWorld * ppu), 8, 256);
        int th = Mathf.Clamp(Mathf.CeilToInt(hWorld * ppu), 8, 256);

        var tex = new Texture2D(tw, th, TextureFormat.RGBA32, false)
        {
            name = texName,
            filterMode = FilterMode.Bilinear
        };
        var px = new Color[tw * th];
        Color edge = Color.Lerp(fill, Color.white, 0.35f);

        for (int y = 0; y < th; y++)
        {
            for (int x = 0; x < tw; x++)
            {
                float wx = minX + (x + 0.5f) / ppu;
                float wy = minY + (y + 0.5f) / ppu;
                var p = new Vector2(wx, wy);
                if (PointInTriangle(p, localPts[0], localPts[1], localPts[2]))
                {
                    bool nearEdge =
                        !PointInTriangle(p + new Vector2(0.03f, 0f), localPts[0], localPts[1], localPts[2]) ||
                        !PointInTriangle(p + new Vector2(-0.03f, 0f), localPts[0], localPts[1], localPts[2]) ||
                        !PointInTriangle(p + new Vector2(0f, 0.03f), localPts[0], localPts[1], localPts[2]) ||
                        !PointInTriangle(p + new Vector2(0f, -0.03f), localPts[0], localPts[1], localPts[2]);
                    px[y * tw + x] = nearEdge ? edge : fill;
                }
                else px[y * tw + x] = Color.clear;
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        var pivot = new Vector2(-minX / wWorld, -minY / hWorld);
        return Sprite.Create(tex, new Rect(0, 0, tw, th), pivot, ppu);
    }

    private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float s = a.y * c.x - a.x * c.y + (c.y - a.y) * p.x + (a.x - c.x) * p.y;
        float t = a.x * b.y - a.y * b.x + (a.y - b.y) * p.x + (b.x - a.x) * p.y;
        if ((s < 0) != (t < 0) && s != 0 && t != 0) return false;
        float a2 = -b.y * c.x + a.y * (c.x - b.x) + a.x * (b.y - c.y) + b.x * c.y;
        return a2 < 0 ? (s <= 0 && s + t >= a2) : (s >= 0 && s + t <= a2);
    }

    private static void SetActiveIfExists(string name, bool active)
    {
        foreach (var tr in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (!tr.gameObject.scene.IsValid()) continue;
            if (tr.name != name) continue;
            tr.gameObject.SetActive(active);
        }
    }
}
