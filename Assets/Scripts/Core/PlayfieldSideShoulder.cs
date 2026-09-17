using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Boss 左右侧墙小导流肩：只在 Boss 高度带做轻微缓坡凸入。
/// EdgeCollider2D 替换该侧整面 Box；有美术贴图时用 Sprite，右件 Flip X。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class PlayfieldSideShoulder : MonoBehaviour
{
    public enum Side { Left, Right }

    [Header("Boss-side bump (both walls)")]
    [Tooltip("突起顶边 Y（略高于 Boss 中心）")]
    public float bumpTopY = 7.0f;
    [Tooltip("突起底边 Y（略低于 Boss 下沿）")]
    public float bumpBottomY = 5.2f;
    [Tooltip("向场内最大凸入（宜小）")]
    public float depth = 0.28f;
    [Tooltip("上下竖直短段占总高比例（对齐贴图帽头）")]
    [Range(0.05f, 0.25f)]
    public float endFlatRatio = 0.12f;
    [Tooltip("峰顶竖直段占总高比例（对齐贴图中段立面）")]
    [Range(0.1f, 0.4f)]
    public float peakFlatRatio = 0.22f;

    [Header("Full side wall span")]
    public float wallBottomY = -10f;
    public float wallTopY = 7.1f;
    public float straightSampleStep = 1.0f;
    public float wallCenterX = 4.5f;
    public float wallThickness = 0.5f;

    [Header("Enable")]
    public bool enableLeft = true;
    public bool enableRight = true;

    [Header("Physics")]
    public PhysicsMaterial2D physicsMaterial;
    public float edgeRadius = 0.08f;

    [Header("Art")]
    [Tooltip("左件精灵；右件自动 Flip X")]
    public Sprite shoulderSprite;
    public Material artMaterial;
    [Tooltip("精灵相对碰撞包围盒的放大系数")]
    public float artFitPadding = 1.02f;
    public int sortingOrder = 3;

    private const string LeftChildName = "WallShoulder_Left";
    private const string RightChildName = "WallShoulder_Right";

    private void Awake() => Rebuild();
    private void OnEnable() => Rebuild();

    private void OnDisable()
    {
        // 退出 Play / Domain Reload / 场景卸载时 Find+GetComponent 会触发 go.IsActive() 断言
        if (ShouldSkipWallRestore()) return;
        if (!gameObject.scene.IsValid() || !gameObject.scene.isLoaded) return;
        TrySetWallBoxEnabled("Wall_Left", true);
        TrySetWallBoxEnabled("Wall_Right", true);
    }

    private static bool ShouldSkipWallRestore()
    {
#if UNITY_EDITOR
        // 编辑模式脚本重载 / 已离开 Play
        if (!Application.isPlaying)
            return true;
        // 正在停止 Play（isPlaying 可能仍为 true 的过渡帧）
        if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode
            && !UnityEditor.EditorApplication.isPlaying)
            return true;
#endif
        return false;
    }

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

    [ContextMenu("Rebuild Side Shoulders")]
    public void Rebuild()
    {
        ResolvePhysics();
        ResolveArt();
        EnsureChild(LeftChildName, Side.Left, enableLeft);
        EnsureChild(RightChildName, Side.Right, enableRight);
        TrySetWallBoxEnabled("Wall_Left", !enableLeft);
        TrySetWallBoxEnabled("Wall_Right", !enableRight);
    }

    private void ResolvePhysics()
    {
        if (physicsMaterial != null) return;
        var wall = GameObject.Find("Wall_Left");
        var col = wall != null ? wall.GetComponent<Collider2D>() : null;
        if (col != null) physicsMaterial = col.sharedMaterial;
    }

    private void ResolveArt()
    {
#if UNITY_EDITOR
        if (shoulderSprite == null)
        {
            shoulderSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Art/Table/wall_shoulder_left.png");
        }
        if (artMaterial == null)
        {
            artMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Materials/SpriteNeonHDR_Corner.mat");
            if (artMaterial == null)
            {
                artMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/Materials/SpriteNeonHDR_Wall.mat");
            }
        }
#endif
    }

    private static void TrySetWallBoxEnabled(string wallName, bool enabled)
    {
        var go = GameObject.Find(wallName);
        if (go == null || !go.activeInHierarchy) return;
        if (!go.TryGetComponent(out BoxCollider2D box)) return;
        box.enabled = enabled;
    }

    private void EnsureChild(string name, Side side, bool enabled)
    {
        var t = transform.Find(name);
        if (!enabled)
        {
            if (t != null)
            {
                if (Application.isPlaying) Destroy(t.gameObject);
                else DestroyImmediate(t.gameObject);
            }
            return;
        }

        GameObject go = t != null ? t.gameObject : new GameObject(name);
        if (t == null) go.transform.SetParent(transform, false);
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        go.transform.localScale = Vector3.one;

        float innerX = side == Side.Left
            ? -(wallCenterX - wallThickness * 0.5f)
            : (wallCenterX - wallThickness * 0.5f);
        var points = BuildFullSideWithBossBump(side, innerX);
        if (points.Count < 2) return;

        var edge = go.GetComponent<EdgeCollider2D>();
        if (edge == null) edge = go.AddComponent<EdgeCollider2D>();
        edge.SetPoints(points);
        edge.edgeRadius = Mathf.Max(0.01f, edgeRadius);
        if (physicsMaterial != null) edge.sharedMaterial = physicsMaterial;

        var box = go.GetComponent<BoxCollider2D>();
        if (box != null)
        {
            if (Application.isPlaying) Destroy(box);
            else DestroyImmediate(box);
        }

        BuildVisual(go, points, innerX, side);
    }

    /// <summary>
    /// 整面直墙 + Boss 高度折线肩（贴图轮廓）：
    /// 上竖直短段 → 斜出 → 峰顶立面 → 斜回 → 下竖直短段。
    /// </summary>
    private List<Vector2> BuildFullSideWithBossBump(Side side, float innerX)
    {
        float wallTop = Mathf.Max(wallTopY, wallBottomY);
        float wallBot = Mathf.Min(wallTopY, wallBottomY);
        float bandTop = Mathf.Clamp(Mathf.Max(bumpTopY, bumpBottomY), wallBot, wallTop);
        float bandBot = Mathf.Clamp(Mathf.Min(bumpTopY, bumpBottomY), wallBot, wallTop);
        float bandH = Mathf.Max(0.2f, bandTop - bandBot);
        float usedDepth = Mathf.Max(0.05f, depth);
        float inwardSign = side == Side.Left ? 1f : -1f;

        float endFlat = bandH * Mathf.Clamp(endFlatRatio, 0.05f, 0.25f);
        float peakFlat = bandH * Mathf.Clamp(peakFlatRatio, 0.1f, 0.4f);
        float midRemain = Mathf.Max(0.05f, bandH - endFlat * 2f - peakFlat);
        float ramp = midRemain * 0.5f;

        float yTopFlatEnd = bandTop - endFlat;
        float yPeakTop = yTopFlatEnd - ramp;
        float yPeakBot = yPeakTop - peakFlat;
        float yBotFlatStart = yPeakBot - ramp;

        var pts = new List<Vector2>(48);
        float step = Mathf.Max(0.25f, straightSampleStep);

        for (float y = wallBot; y < bandBot; y += step)
            pts.Add(new Vector2(innerX, y));

        // 折线肩（自下而上写入，便于与贴图阅读一致也可；碰撞无方向要求）
        // 自下→上：贴墙底 → 斜出 → 峰面 → 斜收 → 贴墙顶
        pts.Add(new Vector2(innerX, bandBot));
        pts.Add(new Vector2(innerX, yBotFlatStart));
        pts.Add(new Vector2(innerX + inwardSign * usedDepth, yPeakBot));
        pts.Add(new Vector2(innerX + inwardSign * usedDepth, yPeakTop));
        pts.Add(new Vector2(innerX, yTopFlatEnd));
        pts.Add(new Vector2(innerX, bandTop));

        for (float y = bandTop + step; y <= wallTop; y += step)
            pts.Add(new Vector2(innerX, y));
        if (pts.Count == 0 || Mathf.Abs(pts[pts.Count - 1].y - wallTop) > 0.01f)
            pts.Add(new Vector2(innerX, wallTop));

        return pts;
    }

    private void BuildVisual(GameObject go, List<Vector2> points, float innerX, Side side)
    {
        float bandTop = Mathf.Max(bumpTopY, bumpBottomY);
        float bandBot = Mathf.Min(bumpTopY, bumpBottomY);

        var bandPts = new List<Vector2>();
        for (int i = 0; i < points.Count; i++)
        {
            if (points[i].y >= bandBot - 0.02f && points[i].y <= bandTop + 0.02f)
                bandPts.Add(points[i]);
        }
        if (bandPts.Count < 2) return;

        // 台面包围：墙内缘 ↔ 引导折线
        var poly = new List<Vector2>(bandPts.Count + 2);
        poly.Add(new Vector2(innerX, bandBot));
        poly.AddRange(bandPts);
        poly.Add(new Vector2(innerX, bandTop));

        // 关掉旧程序化可视
        var lr = go.GetComponent<LineRenderer>();
        if (lr != null) lr.enabled = false;
        var mf = go.GetComponent<MeshFilter>();
        var mr = go.GetComponent<MeshRenderer>();
        if (mf != null) mf.sharedMesh = null;
        if (mr != null) mr.enabled = false;

        EnsureVisual(go, out var visual, out var sr);
        if (shoulderSprite != null)
        {
            sr.enabled = true;
            sr.sprite = shoulderSprite;
            sr.flipX = side == Side.Right;
            sr.color = Color.white;
            sr.sharedMaterial = artMaterial != null ? artMaterial : CyberVisualFactory.UnlitMaterial;
            sr.sortingOrder = sortingOrder;
            FitChildSprite(visual, sr, LocalAabb(poly.ToArray()), artFitPadding);
        }
        else
        {
            sr.enabled = false;
        }
    }

    private static void EnsureVisual(GameObject root, out Transform visual, out SpriteRenderer sr)
    {
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
        float sx = localAabb.width / Mathf.Max(0.001f, sb.x);
        float sy = localAabb.height / Mathf.Max(0.001f, sb.y);
        // Tight mesh 后按包围盒均匀缩放，略放大盖住碰撞边
        float s = Mathf.Max(sx, sy) * Mathf.Max(1f, pad);
        visual.localScale = new Vector3(s, s, 1f);
        visual.localPosition = localAabb.center;
    }
}

