using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 底部外道 / 内道引导 + 落球口几何，并生成左右 SpringBoard。
/// 侧墙内缘 ±4.25；外道在墙与分隔条之间；内道回流挡板；中央为落球口。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class PlayfieldBottomLanes : MonoBehaviour
{
    [Header("Lane Geometry")]
    public float wallInnerX = 4.25f;
    public float dividerTopY = -5.2f;
    public float dividerMidY = -6.55f;
    public float dividerBottomY = -7.45f;
    [Tooltip("分隔条顶部 X（绝对值），外道宽约 wallInner−此值")]
    public float dividerTopX = 3.55f;
    public float dividerMidX = 3.42f;
    public float dividerBottomX = 3.58f;
    public float laneThickness = 0.16f;

    [Header("Inlane Shelf")]
    [Tooltip("外端近墙；内端指向挡板接球区（喂球）")]
    public float shelfOuterX = 3.45f;
    public float shelfInnerX = 1.85f;
    public float shelfY = -6.5f;
    public float shelfTipY = -6.95f;

    [Header("SpringBoard")]
    public float springboardX = 3.9f;
    public float springboardY = -7.2f;
    public Vector2 springboardSize = new Vector2(0.85f, 0.32f);
    public float springLaunchSpeed = 16f;
    public Vector2 springLaunchDirLeft = new Vector2(0.55f, 1f);

    [Header("Center Drain")]
    [Tooltip("落球口视觉半宽（对齐挡板间隙）")]
    public float drainHalfWidth = 1.05f;
    [Tooltip("底部死亡触发半宽；外道漏球也要能接到，建议接近半台宽")]
    public float drainCatchHalfWidth = 4.75f;
    public float drainY = -8.1f;
    public Vector2 drainSize = new Vector2(2.1f, 0.4f);

    [Header("Refs / Style")]
    public PhysicsMaterial2D physicsMaterial;
    public Material wallMaterial;
    public Color laneColor = new Color(0.08f, 0.75f, 0.95f, 0.9f);
    public bool disableLegacyRails = true;

    private Transform _root;

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

    private void Awake()
    {
        // 运行时若场景已烘焙过车道，勿重建（避免清掉 SpringBoard 充能状态）
        var existing = transform.Find("LanePieces");
        if (Application.isPlaying && existing != null && existing.childCount > 0)
        {
            _root = existing;
            if (disableLegacyRails) SetLegacyRailsActive(false);
            return;
        }
        Rebuild();
    }

    [ContextMenu("Rebuild Bottom Lanes")]
    public void Rebuild()
    {
        EnsureRoot();
        ClearChildren(_root);
        ResolveAssets();
        if (disableLegacyRails) SetLegacyRailsActive(false);

        BuildDivider(-1f, "OutlaneDivider_L");
        BuildDivider(1f, "OutlaneDivider_R");
        BuildShelf(-1f, "InlaneShelf_L");
        BuildShelf(1f, "InlaneShelf_R");
        BuildSpringBoard(-1f, "SpringBoard_L");
        BuildSpringBoard(1f, "SpringBoard_R");
        ConfigureDrain();
    }

    private void EnsureRoot()
    {
        _root = transform.Find("LanePieces");
        if (_root == null)
        {
            var go = new GameObject("LanePieces");
            go.transform.SetParent(transform, false);
            _root = go.transform;
        }
    }

    private void ResolveAssets()
    {
#if UNITY_EDITOR
        if (wallMaterial == null)
            wallMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TronWall.mat");
        if (physicsMaterial == null)
        {
            var left = GameObject.Find("Wall_Left");
            var col = left != null ? left.GetComponent<Collider2D>() : null;
            if (col != null) physicsMaterial = col.sharedMaterial;
        }
#endif
    }

    private static void SetLegacyRailsActive(bool active)
    {
        foreach (var n in new[] { "Rail_LeftOuter", "Rail_RightOuter" })
        {
            var go = GameObject.Find(n);
            if (go != null) go.SetActive(active);
        }
    }

    private void BuildDivider(float sign, string name)
    {
        var pts = new List<Vector2>
        {
            new Vector2(sign * dividerTopX, dividerTopY),
            new Vector2(sign * dividerMidX, dividerMidY),
            new Vector2(sign * dividerBottomX, dividerBottomY),
        };
        CreateRibbonCollider(name, pts, laneThickness, false);
    }

    private void BuildShelf(float sign, string name)
    {
        var pts = new List<Vector2>
        {
            new Vector2(sign * shelfOuterX, shelfY),
            new Vector2(sign * shelfInnerX, shelfTipY),
        };
        CreateRibbonCollider(name, pts, laneThickness * 0.85f, false);
    }

    private void BuildSpringBoard(float sign, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(_root, false);
        go.transform.position = new Vector3(sign * springboardX, springboardY, 0f);

        var box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = springboardSize;

        var sr = go.AddComponent<SpriteRenderer>();
        Color neon = new Color(0.15f, 1.6f, 0.55f, 1f);
        sr.sprite = CyberVisualFactory.CreateSpringBoardSprite(neon);
        sr.material = CyberVisualFactory.UnlitMaterial;
        sr.color = Color.white;
        sr.sortingOrder = 3;

        var sb = go.AddComponent<SpringBoard>();
        sb.launchDirection = new Vector2(sign < 0 ? springLaunchDirLeft.x : -springLaunchDirLeft.x, springLaunchDirLeft.y);
        sb.launchSpeed = springLaunchSpeed;
        sb.chargesPerWave = 1;
        sb.chargedColor = neon;
        sb.depletedColor = new Color(0.08f, 0.22f, 0.12f, 1f);
    }

    private void ConfigureDrain()
    {
        var drain = GameObject.Find("BottomBoundary");
        if (drain == null)
        {
            drain = new GameObject("BottomBoundary");
            drain.transform.position = new Vector3(0f, drainY, 0f);
            drain.AddComponent<BoxCollider2D>().isTrigger = true;
            drain.AddComponent<BottomBoundary>();
        }

        drain.transform.position = new Vector3(0f, drainY, 0f);
        var box = drain.GetComponent<BoxCollider2D>();
        if (box == null) box = drain.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        float catchHalf = Mathf.Max(drainHalfWidth, drainCatchHalfWidth);
        box.size = new Vector2(Mathf.Max(0.5f, catchHalf * 2f), drainSize.y);
        if (drain.GetComponent<BottomBoundary>() == null)
            drain.AddComponent<BottomBoundary>();

        // 视觉标记：落球口霓虹槽（只标中缝，不表示整条死亡触发）
        Transform marker = _root.Find("DrainMarker");
        GameObject markerGo;
        if (marker == null)
        {
            markerGo = new GameObject("DrainMarker");
            markerGo.transform.SetParent(_root, false);
        }
        else markerGo = marker.gameObject;

        markerGo.transform.position = new Vector3(0f, drainY + 0.15f, 0f);
        var lr = markerGo.GetComponent<LineRenderer>();
        if (lr == null) lr = markerGo.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.positionCount = 2;
        lr.SetPosition(0, new Vector3(-drainHalfWidth, 0f, 0f));
        lr.SetPosition(1, new Vector3(drainHalfWidth, 0f, 0f));
        lr.startWidth = 0.08f;
        lr.endWidth = 0.08f;
        lr.numCapVertices = 2;
        lr.sortingOrder = 2;
        var c = new Color(1f, 0.25f, 0.45f, 0.75f);
        lr.startColor = c;
        lr.endColor = c;
        if (lr.sharedMaterial == null)
        {
            var sh = Shader.Find("Sprites/Default");
            if (sh != null) lr.sharedMaterial = new Material(sh);
        }
    }

    private void CreateRibbonCollider(string name, List<Vector2> centerline, float thickness, bool isTrigger)
    {
        var go = new GameObject(name);
        go.transform.SetParent(_root, false);

        var edge = go.AddComponent<EdgeCollider2D>();
        edge.isTrigger = isTrigger;
        edge.edgeRadius = thickness * 0.35f;
        edge.SetPoints(centerline);
        if (physicsMaterial != null) edge.sharedMaterial = physicsMaterial;

        // 细带状网格视觉（与顶弧同属霓虹框语言）
        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        mf.sharedMesh = BuildRibbonMesh(centerline, thickness * 0.5f);
        if (wallMaterial != null) mr.sharedMaterial = wallMaterial;
        else
        {
            mr.sharedMaterial = CyberVisualFactory.UnlitMaterial;
            // Unlit 无顶点色时靠材质默认；再加一条 LineRenderer 作描边
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = centerline.Count;
            for (int i = 0; i < centerline.Count; i++)
                lr.SetPosition(i, centerline[i]);
            lr.startWidth = thickness;
            lr.endWidth = thickness;
            lr.startColor = laneColor;
            lr.endColor = laneColor;
            lr.numCornerVertices = 3;
            lr.sortingOrder = 2;
            lr.sharedMaterial = CyberVisualFactory.UnlitMaterial;
        }
        mr.sortingOrder = 2;
    }

    private static Mesh BuildRibbonMesh(List<Vector2> center, float halfThick)
    {
        int n = center.Count;
        var mesh = new Mesh { name = "LaneRibbon" };
        if (n < 2) return mesh;

        var verts = new Vector3[n * 2];
        var uvs = new Vector2[n * 2];
        var tris = new int[(n - 1) * 6];
        float len = 0f;
        var dist = new float[n];
        for (int i = 1; i < n; i++)
        {
            len += Vector2.Distance(center[i - 1], center[i]);
            dist[i] = len;
        }
        if (len < 0.001f) len = 1f;

        for (int i = 0; i < n; i++)
        {
            Vector2 tan = i == 0 ? (center[1] - center[0]).normalized
                : i == n - 1 ? (center[n - 1] - center[n - 2]).normalized
                : (center[i + 1] - center[i - 1]).normalized;
            Vector2 nrm = new Vector2(-tan.y, tan.x).normalized;
            verts[i * 2] = center[i] - nrm * halfThick;
            verts[i * 2 + 1] = center[i] + nrm * halfThick;
            float v = dist[i] / len;
            uvs[i * 2] = new Vector2(0f, v);
            uvs[i * 2 + 1] = new Vector2(1f, v);
        }

        int ti = 0;
        for (int i = 0; i < n - 1; i++)
        {
            int i0 = i * 2, i1 = i * 2 + 1, i2 = (i + 1) * 2, i3 = (i + 1) * 2 + 1;
            tris[ti++] = i0; tris[ti++] = i2; tris[ti++] = i1;
            tris[ti++] = i1; tris[ti++] = i2; tris[ti++] = i3;
        }

        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void ClearChildren(Transform root)
    {
        if (root == null) return;
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            var c = root.GetChild(i).gameObject;
                if (Application.isPlaying) UnityEngine.Object.Destroy(c);
            else UnityEngine.Object.DestroyImmediate(c);
        }
    }
}
