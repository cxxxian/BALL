using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Short, reusable charge discharges. EnemyElectricState owns the actual stacks.</summary>
[DisallowMultipleComponent]
public class EnemyChargeArcVisual : MonoBehaviour
{
    private const int MaxSlots = 5;
    private const int PointCount = 6;
    private const float TwoPi = Mathf.PI * 2f;

    [Header("Charge Appearance")]
    [SerializeField] private Color glowColor = new Color(0.02f, 0.52f, 0.78f, 0.28f);
    [SerializeField] private Color bodyColor = new Color(0f, 0.91f, 1f, 0.95f);
    [SerializeField] private Color coreColor = new Color(0.84f, 0.98f, 1f, 1f);
    [SerializeField, Range(2f, 5f)] private float bodyWidthPixels = 3f;

    private sealed class ArcSlot
    {
        public readonly Vector2[] points = new Vector2[PointCount];
        public readonly Vector2[] branch = new Vector2[3];
        public float elapsed, duration, cooldown, seed;
        public bool active, hasBranch;
    }

    private static Material _sharedMaterial;
    private readonly ArcSlot[] _slots = new ArcSlot[MaxSlots];
    private readonly List<Vector3> _vertices = new List<Vector3>(512);
    private readonly List<Vector2> _uvs = new List<Vector2>(512);
    private readonly List<Color> _colors = new List<Color>(512);
    private readonly List<int> _triangles = new List<int>(768);
    private SpriteRenderer _sprite;
    private EnemyElectricState _state;
    private MeshFilter _filter;
    private MeshRenderer _renderer;
    private Mesh _mesh;
    private Transform _root;
    private Camera _camera;
    private int _level;
    private float _nodeAngle, _pulseWait, _pulseLeft;

    public static EnemyChargeArcVisual EnsureOn(EnemyBase enemy)
    {
        if (enemy == null) return null;
        if (!enemy.TryGetComponent(out EnemyChargeArcVisual visual))
            visual = enemy.gameObject.AddComponent<EnemyChargeArcVisual>();
        visual.EnsureSetup();
        return visual;
    }

private void Awake()
    {
        EnsureSlots();
        _nodeAngle = Random.Range(0f, TwoPi);
        EnsureSetup();
    }

private void EnsureSlots()
    {
        for (int i = 0; i < MaxSlots; i++)
            if (_slots[i] == null) _slots[i] = new ArcSlot();
    }


    private void OnEnable() => EnsureSetup();

    private void OnDestroy()
    {
        if (_mesh != null) Destroy(_mesh);
    }

    private void EnsureSetup()
    {
        if (_sprite == null)
            _sprite = GetComponent<SpriteRenderer>() ?? GetComponentInChildren<SpriteRenderer>();
        if (_sprite == null) return;
        if (_root == null)
        {
            _root = _sprite.transform.Find("ChargeOrbitTrack");
            if (_root == null)
            {
                var go = new GameObject("ChargeOrbitTrack");
                go.transform.SetParent(_sprite.transform, false);
                _root = go.transform;
            }
        }
        if (!_root.TryGetComponent(out _filter))
            _filter = _root.gameObject.AddComponent<MeshFilter>();
        if (!_root.TryGetComponent(out _renderer))
            _renderer = _root.gameObject.AddComponent<MeshRenderer>();
        if (_mesh == null)
        {
            _mesh = new Mesh { name = "EnemyChargeDischarges" };
            _mesh.MarkDynamic();
        }
        _filter.sharedMesh = _mesh;
        if (_sharedMaterial == null)
        {
            Shader shader = Shader.Find("Custom/ChargeArcUnlit");
            if (shader != null)
                _sharedMaterial = new Material(shader) { name = "EnemyChargeDischarge_Shared" };
        }
        _renderer.sharedMaterial = _sharedMaterial;
        _renderer.shadowCastingMode = ShadowCastingMode.Off;
        _renderer.receiveShadows = false;
        _camera = Camera.main;
        SyncSorting();
    }

private void LateUpdate()
    {
        if (_renderer == null || _sprite == null) EnsureSetup();
        if (_renderer == null || _sprite == null) return;
        EnsureSlots();
        if (_state == null) TryGetComponent(out _state);
        int stacks = _state != null ? _state.ChargeStacks : 0;
        if (stacks != _level) SetElectricLevel(stacks);
        _renderer.enabled = _level > 0 && _sharedMaterial != null;
        if (!_renderer.enabled) return;
        SyncSorting();
        UpdateSlots(Time.deltaTime);
        BuildMesh();
    }

    /// <summary>Visual level only. The status component remains the source of truth.</summary>
    public void SetElectricLevel(int level)
    {
        _level = Mathf.Clamp(level, 0, 3);
        _pulseLeft = 0f;
        _pulseWait = Random.Range(0.7f, 1.2f);
        for (int i = 0; i < MaxSlots; i++)
        {
            _slots[i].active = false;
            _slots[i].cooldown = 0f;
        }
        if (_level == 0)
        {
            if (_mesh != null) _mesh.Clear();
            if (_renderer != null) _renderer.enabled = false;
            return;
        }
        int first = _level == 1 ? 1 : _level == 2 ? 2 : 4;
        for (int i = 0; i < SlotCount(_level); i++)
        {
            if (i < first) Spawn(_slots[i]);
            else _slots[i].cooldown = Random.Range(0.05f, 0.25f);
        }
    }

    private static int SlotCount(int level) => level == 1 ? 1 : level == 2 ? 3 : 5;

    private void SyncSorting()
    {
        _renderer.sortingLayerID = _sprite.sortingLayerID;
        _renderer.sortingOrder = _sprite.sortingOrder + 2;
    }

    private void UpdateSlots(float dt)
    {
        if (_level == 3)
        {
            _pulseWait -= dt;
            _pulseLeft = Mathf.Max(0f, _pulseLeft - dt);
            if (_pulseWait <= 0f)
            {
                _pulseLeft = 0.075f;
                _pulseWait = Random.Range(0.7f, 1.2f);
            }
        }
        for (int i = 0; i < SlotCount(_level); i++)
        {
            ArcSlot s = _slots[i];
            if (s.active)
            {
                s.elapsed += dt;
                if (s.elapsed < s.duration) continue;
                s.active = false;
                s.cooldown = _level == 1 ? Random.Range(0.20f, 0.38f)
                    : _level == 2 ? Random.Range(0.06f, 0.20f)
                    : Random.Range(0.03f, 0.14f);
            }
            else
            {
                s.cooldown -= dt;
                if (s.cooldown <= 0f) Spawn(s);
            }
        }
    }

    private void Spawn(ArcSlot s)
    {
        Bounds bounds = SpriteBounds();
        Vector2 center = bounds.center;
        float hx = Mathf.Max(bounds.extents.x, 0.15f);
        float hy = Mathf.Max(bounds.extents.y, 0.15f);
        float unit = Mathf.Min(hx, hy);
        float angle = Random.Range(0f, TwoPi);
        float span = _level == 1 ? Random.Range(0.28f, 0.44f)
            : _level == 2 ? Random.Range(0.36f, 0.58f)
            : Random.Range(0.42f, 0.66f);
        if (Random.value < 0.5f) span = -span;
        bool crossing = _level == 3 && Random.value < 0.12f;
        if (crossing)
        {
            Vector2 a = OrbitPoint(center, hx * 0.83f, hy * 0.83f, angle);
            Vector2 b = OrbitPoint(center, hx * 0.83f, hy * 0.83f,
                angle + Random.Range(0.9f, 1.35f));
            Vector2 side = new Vector2(-(b.y - a.y), b.x - a.x).normalized;
            for (int j = 0; j < PointCount; j++)
            {
                float t = j / (float)(PointCount - 1);
                float kink = j == 0 || j == PointCount - 1 ? 0f
                    : Random.Range(-0.07f, 0.07f) * unit;
                s.points[j] = Vector2.Lerp(a, b, t) + side * kink;
            }
        }
        else
        {
            float radius = _level == 1 ? Random.Range(0.94f, 1.07f)
                : Random.Range(0.84f, 1.09f);
            for (int j = 0; j < PointCount; j++)
            {
                float a = angle + span * j / (PointCount - 1);
                Vector2 radial = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                float kink = j == 0 || j == PointCount - 1 ? 0f
                    : Random.Range(-0.10f, 0.10f) * unit;
                s.points[j] = OrbitPoint(center, hx * radius, hy * radius, a)
                    + radial * kink;
            }
        }
        s.hasBranch = _level >= 2 && Random.value < (_level == 2 ? 0.12f : 0.27f);
        if (s.hasBranch)
        {
            Vector2 root = s.points[3];
            Vector2 outward = (root - center).normalized;
            Vector2 tangent = new Vector2(-outward.y, outward.x);
            s.branch[0] = root;
            s.branch[1] = root + (outward + tangent * 0.45f) * unit * 0.10f;
            s.branch[2] = root + (outward + tangent * 0.30f) * unit * 0.21f;
        }
        s.active = true;
        s.elapsed = 0f;
        s.duration = Random.Range(0.30f, 0.44f);
        s.seed = Random.Range(0f, 100f);
        _nodeAngle = angle;
    }

    private Bounds SpriteBounds() => _sprite.sprite != null
        ? _sprite.sprite.bounds
        : new Bounds(Vector3.zero, new Vector3(0.9f, 0.9f, 0f));

    private static Vector2 OrbitPoint(Vector2 center, float rx, float ry, float angle) =>
        center + new Vector2(Mathf.Cos(angle) * rx, Mathf.Sin(angle) * ry);

    private void BuildMesh()
    {
        _vertices.Clear();
        _uvs.Clear();
        _colors.Clear();
        _triangles.Clear();

        if (_camera == null) _camera = Camera.main;
        float worldPerPixel = _camera != null && _camera.orthographic
            ? 2f * _camera.orthographicSize / Mathf.Max(1, Screen.height)
            : 0.025f;
        Vector3 scale = _root.lossyScale;
        float objectScale = Mathf.Max(0.001f,
            Mathf.Min(Mathf.Abs(scale.x), Mathf.Abs(scale.y)));
        float bodyHalf = worldPerPixel * bodyWidthPixels * 0.5f / objectScale;
        if (_level == 3) bodyHalf *= 1.14f;
        float pulse = _pulseLeft > 0f ? 1.45f : 1f;

        for (int i = 0; i < SlotCount(_level); i++)
        {
            ArcSlot s = _slots[i];
            if (!s.active) continue;
            float opacity = Mathf.Min(Mathf.Clamp01(s.elapsed / 0.045f),
                Mathf.Clamp01((s.duration - s.elapsed) / 0.15f)) * pulse;
            if (opacity < 0.01f) continue;
            Color glow = glowColor, body = bodyColor, core = coreColor;
            glow.a *= opacity;
            body.a *= opacity;
            core.a *= opacity;
            AddPolyline(s.points, PointCount, bodyHalf * 1.9f, glow, s.seed);
            AddPolyline(s.points, PointCount, bodyHalf, body, s.seed);
            AddPolyline(s.points, PointCount, bodyHalf * 0.48f, core, s.seed);
            AddNode(s.points[PointCount - 1], bodyHalf, opacity * 0.85f);
            if (s.hasBranch)
            {
                body.a *= 0.65f;
                core.a *= 0.65f;
                AddPolyline(s.branch, 3, bodyHalf * 0.65f, body, s.seed);
                AddPolyline(s.branch, 3, bodyHalf * 0.31f, core, s.seed);
            }
        }

        Bounds bounds = SpriteBounds();
        Vector2 idle = OrbitPoint(bounds.center, bounds.extents.x * 1.02f,
            bounds.extents.y * 1.02f, _nodeAngle);
        AddNode(idle, bodyHalf * 0.90f, 0.70f);

        _mesh.Clear();
        _mesh.SetVertices(_vertices);
        _mesh.SetUVs(0, _uvs);
        _mesh.SetColors(_colors);
        _mesh.SetTriangles(_triangles, 0);
        _mesh.RecalculateBounds();
    }

    private void AddPolyline(Vector2[] points, int count, float halfWidth, Color color, float seed)
    {
        for (int i = 0; i < count - 1; i++)
        {
            Vector2 a = Jitter(points[i], i, count, halfWidth, seed);
            Vector2 b = Jitter(points[i + 1], i + 1, count, halfWidth, seed);
            AddSegment(a, b, halfWidth, color,
                i == 0 ? 0.35f : 1f, i == count - 2 ? 0.35f : 1f);
        }
    }

    private static Vector2 Jitter(Vector2 p, int i, int count, float width, float seed)
    {
        if (i == 0 || i == count - 1) return p;
        return p + new Vector2(Mathf.Sin(Time.time * 39f + seed + i * 3.7f),
            Mathf.Cos(Time.time * 33f + seed + i * 2.3f)) * width * 0.12f;
    }

    private void AddNode(Vector2 p, float halfWidth, float opacity)
    {
        Color c = coreColor;
        c.a *= Mathf.Clamp01(opacity);
        float arm = halfWidth * 1.80f;
        AddSegment(p + Vector2.left * arm, p + Vector2.right * arm,
            halfWidth * 0.42f, c, 0.55f, 0.55f);
        AddSegment(p + Vector2.down * arm, p + Vector2.up * arm,
            halfWidth * 0.42f, c, 0.55f, 0.55f);
    }

    private void AddSegment(Vector2 a, Vector2 b, float halfWidth, Color color,
        float startFade, float endFade)
    {
        Vector2 tangent = (b - a).normalized;
        if (tangent.sqrMagnitude < 0.001f) return;
        Vector2 normal = new Vector2(-tangent.y, tangent.x) * halfWidth;
        int first = _vertices.Count;
        _vertices.Add(a + normal);
        _vertices.Add(b + normal);
        _vertices.Add(b - normal);
        _vertices.Add(a - normal);
        _uvs.Add(new Vector2(0f, 1f));
        _uvs.Add(new Vector2(1f, 1f));
        _uvs.Add(new Vector2(1f, 0f));
        _uvs.Add(new Vector2(0f, 0f));
        Color ca = color, cb = color;
        ca.a *= startFade;
        cb.a *= endFade;
        _colors.Add(ca);
        _colors.Add(cb);
        _colors.Add(cb);
        _colors.Add(ca);
        _triangles.Add(first);
        _triangles.Add(first + 1);
        _triangles.Add(first + 2);
        _triangles.Add(first);
        _triangles.Add(first + 2);
        _triangles.Add(first + 3);
    }
}
