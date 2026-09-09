using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 台面外框视觉 + 顶弧碰撞：左右墙与顶弧画成同一条 TronWall 带状网格，
/// 避免「侧墙材质 / 顶弧 LineRenderer」风格割裂。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(EdgeCollider2D))]
public class PlayfieldTopArc : MonoBehaviour
{
    [Header("Geometry (world)")]
    [Tooltip("墙中心线半宽（对齐 Wall_Left/Right 的 X）")]
    public float wallCenterX = 4.5f;
    [Tooltip("墙厚度（与侧墙 Box 一致：0.5）")]
    public float wallThickness = 0.5f;
    [Tooltip("侧墙可视底边")]
    public float wallBottomY = -10f;
    [Tooltip("弧与侧墙衔接高度")]
    public float shoulderY = 7.1f;
    [Tooltip("中心线拱顶高度")]
    public float apexY = 9.25f;
    [Range(4, 32)]
    public int sideSegments = 12;
    [Range(8, 96)]
    public int arcSegments = 40;

    [Header("Visual")]
    public Material wallMaterial;
    public int sortingOrder = 0;
    [Tooltip("关闭侧墙 Sprite，改由本网格绘制，风格才连续")]
    public bool hideSideWallSprites = true;

    [Header("Physics")]
    public PhysicsMaterial2D physicsMaterial;
    public float edgeRadius = 0.08f;

    private MeshFilter _filter;
    private MeshRenderer _renderer;
    private EdgeCollider2D _edge;
    private Mesh _mesh;

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

    [ContextMenu("Rebuild Frame")]
    public void Rebuild()
    {
        EnsureComponents();
        ResolveMaterial();
        ApplySideWallSpriteVisibility();

        float halfThick = Mathf.Max(0.05f, wallThickness * 0.5f);
        var center = BuildFrameCenterline();
        if (center.Count < 3) return;

        BuildRibbonMesh(center, halfThick);
        BuildInnerEdgeCollider(center, halfThick);
    }

    private void EnsureComponents()
    {
        _filter = GetComponent<MeshFilter>();
        _renderer = GetComponent<MeshRenderer>();
        _edge = GetComponent<EdgeCollider2D>();

        // 清掉旧 LineRenderer 视觉
        var lrs = GetComponentsInChildren<LineRenderer>(true);
        for (int i = lrs.Length - 1; i >= 0; i--)
        {
            var lr = lrs[i];
            if (lr == null) continue;
            if (lr.gameObject == gameObject)
            {
                if (Application.isPlaying) Destroy(lr);
                else DestroyImmediate(lr);
            }
            else
            {
                if (Application.isPlaying) Destroy(lr.gameObject);
                else DestroyImmediate(lr.gameObject);
            }
        }

        if (_mesh == null)
        {
            _mesh = new Mesh { name = "PlayfieldFrame" };
            _mesh.MarkDynamic();
        }
        _filter.sharedMesh = _mesh;
    }

    private void ResolveMaterial()
    {
        if (wallMaterial == null)
            wallMaterial = Resources.Load<Material>("TronWall");

#if UNITY_EDITOR
        if (wallMaterial == null)
            wallMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TronWall.mat");
#endif

        if (wallMaterial != null)
            _renderer.sharedMaterial = wallMaterial;

        _renderer.sortingOrder = sortingOrder;
    }

    private void ApplySideWallSpriteVisibility()
    {
        SetWallSprite("Wall_Left", !hideSideWallSprites);
        SetWallSprite("Wall_Right", !hideSideWallSprites);
    }

    private static void SetWallSprite(string name, bool enabled)
    {
        var go = GameObject.Find(name);
        if (go == null) return;
        var sr = go.GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = enabled;
    }

    /// <summary>左下 → 左肩 → 顶弧 → 右肩 → 右下（绕场内逆时针）。</summary>
    private List<Vector2> BuildFrameCenterline()
    {
        var pts = new List<Vector2>(sideSegments * 2 + arcSegments + 4);
        float cx = Mathf.Max(0.5f, wallCenterX);
        float rise = Mathf.Max(0.05f, apexY - shoulderY);
        int sides = Mathf.Max(2, sideSegments);
        int arcs = Mathf.Max(8, arcSegments);

        for (int i = 0; i <= sides; i++)
        {
            float t = i / (float)sides;
            float y = Mathf.Lerp(wallBottomY, shoulderY, t);
            pts.Add(new Vector2(-cx, y));
        }

        // 弧：θ = π → 0，跳过与左肩重复的首点
        for (int i = 1; i <= arcs; i++)
        {
            float t = i / (float)arcs;
            float theta = Mathf.PI * (1f - t);
            float x = cx * Mathf.Cos(theta);
            float y = shoulderY + rise * Mathf.Sin(theta);
            pts.Add(new Vector2(x, y));
        }

        for (int i = 1; i <= sides; i++)
        {
            float t = i / (float)sides;
            float y = Mathf.Lerp(shoulderY, wallBottomY, t);
            pts.Add(new Vector2(cx, y));
        }

        return pts;
    }

    private void BuildRibbonMesh(List<Vector2> center, float halfThick)
    {
        int n = center.Count;
        var verts = new Vector3[n * 2];
        var uvs = new Vector2[n * 2];
        var tris = new int[(n - 1) * 6];

        float totalLen = 0f;
        var dist = new float[n];
        dist[0] = 0f;
        for (int i = 1; i < n; i++)
        {
            totalLen += Vector2.Distance(center[i - 1], center[i]);
            dist[i] = totalLen;
        }
        if (totalLen < 0.001f) totalLen = 1f;

        for (int i = 0; i < n; i++)
        {
            Vector2 tangent = SampleTangent(center, i);
            // 沿行进方向顺时针 90° = 指向场内
            Vector2 inward = new Vector2(tangent.y, -tangent.x).normalized;
            Vector2 outer = center[i] - inward * halfThick;
            Vector2 inner = center[i] + inward * halfThick;

            verts[i * 2] = outer;
            verts[i * 2 + 1] = inner;

            float v = dist[i] / totalLen;
            uvs[i * 2] = new Vector2(0f, v);
            uvs[i * 2 + 1] = new Vector2(1f, v);
        }

        int ti = 0;
        for (int i = 0; i < n - 1; i++)
        {
            int i0 = i * 2;
            int i1 = i * 2 + 1;
            int i2 = (i + 1) * 2;
            int i3 = (i + 1) * 2 + 1;
            tris[ti++] = i0;
            tris[ti++] = i2;
            tris[ti++] = i1;
            tris[ti++] = i1;
            tris[ti++] = i2;
            tris[ti++] = i3;
        }

        _mesh.Clear();
        _mesh.vertices = verts;
        _mesh.uv = uvs;
        _mesh.triangles = tris;
        _mesh.RecalculateBounds();
        _mesh.RecalculateNormals();
    }

    private void BuildInnerEdgeCollider(List<Vector2> center, float halfThick)
    {
        // 只取顶弧段内缘做碰撞（侧墙仍用 BoxCollider）
        int sides = Mathf.Max(2, sideSegments);
        int arcs = Mathf.Max(8, arcSegments);
        int arcStart = sides;       // 左肩
        int arcEnd = sides + arcs;  // 右肩

        var edge = new List<Vector2>(arcs + 2);
        for (int i = arcStart; i <= arcEnd && i < center.Count; i++)
        {
            Vector2 tangent = SampleTangent(center, i);
            Vector2 inward = new Vector2(tangent.y, -tangent.x).normalized;
            edge.Add(center[i] + inward * halfThick);
        }

        _edge.SetPoints(edge);
        _edge.edgeRadius = Mathf.Max(0f, edgeRadius);
        if (physicsMaterial != null)
            _edge.sharedMaterial = physicsMaterial;
    }

    private static Vector2 SampleTangent(List<Vector2> pts, int i)
    {
        if (pts.Count < 2) return Vector2.up;
        if (i <= 0) return (pts[1] - pts[0]).normalized;
        if (i >= pts.Count - 1) return (pts[pts.Count - 1] - pts[pts.Count - 2]).normalized;
        return (pts[i + 1] - pts[i - 1]).normalized;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        var center = BuildFrameCenterline();
        Gizmos.color = new Color(0.1f, 0.85f, 1f, 0.9f);
        for (int i = 1; i < center.Count; i++)
            Gizmos.DrawLine(transform.TransformPoint(center[i - 1]), transform.TransformPoint(center[i]));
    }
#endif
}
