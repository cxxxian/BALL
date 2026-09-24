using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Tesla discharge rendered with the same shared ribbon shader, palette,
/// layered widths, and short jagged path as enemy charge arcs.
/// </summary>
public class TeslaArcFX : MonoBehaviour
{
    public static TeslaArcFX Instance { get; private set; }

    private const int PoolSize = 12;
    private const int PointCount = 6;
    private const float ArcDuration = 0.18f;
    private const float BodyWidthPixels = 3f;

    private static readonly Color GlowColor = new Color(0.02f, 0.52f, 0.78f, 0.28f);
    private static readonly Color BodyColor = new Color(0f, 0.91f, 1f, 0.95f);
    private static readonly Color CoreColor = new Color(0.84f, 0.98f, 1f, 1f);
    private static Material _sharedLineMaterial;

    private readonly Queue<ArcInstance> _pool = new Queue<ArcInstance>();
    private Camera _camera;

    private sealed class ArcInstance
    {
        public GameObject Root;
        public LineRenderer Glow;
        public LineRenderer Body;
        public LineRenderer Core;
        public readonly Vector3[] Points = new Vector3[PointCount];
        public Coroutine Routine;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        _camera = Camera.main;
        EnsureLineMaterial();

        for (int i = 0; i < PoolSize; i++)
            _pool.Enqueue(CreateArcInstance());
    }

    public void SpawnArc(Vector2 from, Vector2 to, int jitterSeed)
    {
        if (_pool.Count == 0) return;

        ArcInstance arc = _pool.Dequeue();
        arc.Root.SetActive(true);
        SetWidths(arc);
        arc.Routine = StartCoroutine(FlashAndReturn(arc, from, to, jitterSeed));
    }

    private IEnumerator FlashAndReturn(ArcInstance arc, Vector2 from, Vector2 to, int seed)
    {
        float elapsed = 0f;

        while (elapsed < ArcDuration)
        {
            float life = 1f - elapsed / ArcDuration;
            float alpha = Mathf.Clamp01(life * life);
            SetLayerColor(arc.Glow, GlowColor, alpha);
            SetLayerColor(arc.Body, BodyColor, alpha);
            SetLayerColor(arc.Core, CoreColor, alpha);
            FillJaggedBolt(arc, from, to, seed, Mathf.FloorToInt(Time.time * 30f));

            elapsed += Time.deltaTime;
            yield return null;
        }

        arc.Routine = null;
        arc.Root.SetActive(false);
        _pool.Enqueue(arc);
    }

    private static void SetLayerColor(LineRenderer line, Color color, float alpha)
    {
        color.a *= alpha;
        line.startColor = color;
        line.endColor = color;
    }

    private static void FillJaggedBolt(ArcInstance arc, Vector2 from, Vector2 to,
        int seed, int tick)
    {
        Vector2 direction = to - from;
        Vector2 side = direction.sqrMagnitude > 0.0001f
            ? new Vector2(-direction.y, direction.x).normalized
            : Vector2.up;
        const float maxJitter = 0.10f;

        arc.Points[0] = from;
        arc.Points[PointCount - 1] = to;
        for (int i = 1; i < PointCount - 1; i++)
        {
            float t = i / (float)(PointCount - 1);
            float envelope = Mathf.Sin(t * Mathf.PI);
            float offset = (Hash01(seed, i, tick) * 2f - 1f) * maxJitter * envelope;
            float along = (Hash01(seed + 71, i, tick) * 2f - 1f) * 0.025f * envelope;
            arc.Points[i] = Vector2.Lerp(from, to, t)
                + side * offset + direction.normalized * along;
        }

        SetPoints(arc.Glow, arc.Points);
        SetPoints(arc.Body, arc.Points);
        SetPoints(arc.Core, arc.Points);
    }

    private static void SetPoints(LineRenderer line, Vector3[] points)
    {
        line.positionCount = PointCount;
        line.SetPositions(points);
    }

    private static float Hash01(int seed, int point, int tick)
    {
        float value = Mathf.Sin(seed * 12.9898f + point * 78.233f + tick * 37.719f) * 43758.5453f;
        return value - Mathf.Floor(value);
    }

    private ArcInstance CreateArcInstance()
    {
        var root = new GameObject("TeslaArc");
        root.transform.SetParent(transform, false);

        var glow = CreateLine(root.transform, "Glow", 21, GlowColor);
        var body = CreateLine(root.transform, "Body", 22, BodyColor);
        var core = CreateLine(root.transform, "Core", 23, CoreColor);
        var arc = new ArcInstance { Root = root, Glow = glow, Body = body, Core = core };
        SetWidths(arc);

        root.SetActive(false);
        return arc;
    }

    private LineRenderer CreateLine(Transform parent, string objectName, int sortingOrder, Color color)
    {
        var go = new GameObject(objectName);
        go.transform.SetParent(parent, false);

        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.textureMode = LineTextureMode.Stretch;
        line.alignment = LineAlignment.View;
        line.loop = false;
        line.positionCount = PointCount;
        line.numCapVertices = 2;
        line.numCornerVertices = 1;
        line.sortingOrder = sortingOrder;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.allowOcclusionWhenDynamic = false;
        line.sharedMaterial = EnsureLineMaterial();
        line.startColor = color;
        line.endColor = color;
        return line;
    }

    private void SetWidths(ArcInstance arc)
    {
        if (_camera == null) _camera = Camera.main;
        float worldPerPixel = _camera != null && _camera.orthographic
            ? 2f * _camera.orthographicSize / Mathf.Max(1, Screen.height)
            : 0.025f;
        float bodyHalfWidth = worldPerPixel * BodyWidthPixels * 0.5f;

        SetWidth(arc.Glow, bodyHalfWidth * 3.8f);
        SetWidth(arc.Body, bodyHalfWidth * 2f);
        SetWidth(arc.Core, bodyHalfWidth * 0.96f);
    }

    private static void SetWidth(LineRenderer line, float fullWidth)
    {
        line.startWidth = fullWidth;
        line.endWidth = fullWidth;
    }

    private static Material EnsureLineMaterial()
    {
        if (_sharedLineMaterial != null) return _sharedLineMaterial;

        Shader shader = Shader.Find("Custom/ChargeArcUnlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");

        _sharedLineMaterial = new Material(shader != null
            ? shader
            : Shader.Find("Hidden/InternalErrorShader"))
        {
            name = "ElectricArc_Shared"
        };
        return _sharedLineMaterial;
    }

    public static void EnsureInstance()
    {
        if (Instance != null) return;
        var go = new GameObject("TeslaArcFX");
        go.AddComponent<TeslaArcFX>();
    }
}
