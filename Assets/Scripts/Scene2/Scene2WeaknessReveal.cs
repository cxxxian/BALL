using DG.Tweening;
using UnityEngine;

// Scene2 presentation only: never changes collision, damage, input or cooldowns.
[DisallowMultipleComponent]
public sealed class Scene2WeaknessReveal : MonoBehaviour
{
    [SerializeField, Range(0.8f, 1.8f)] private float radius = 1.12f;
    private const float RippleDuration = 0.48f;
    private readonly Vector4[] _impacts = new Vector4[3];
    private static readonly int[] ImpactIds = { Shader.PropertyToID("_Impact0"), Shader.PropertyToID("_Impact1"), Shader.PropertyToID("_Impact2") };
    private Transform _root;
    private MeshRenderer _renderer;
    private Material _material;
    private Mesh _mesh;
    private MaterialPropertyBlock _properties;
    private Scene2Boss _boss;
    private int _nextImpact;
    private float _clock, _formation = 1f;
    private bool _exposed;
    public bool IsPlaying { get; private set; }
    public float Progress { get; private set; }
    public bool ShieldVisible => _renderer != null && _renderer.enabled;
    public int CollisionResponses { get; private set; }
    public void ConfigurePresentationRadius(float value) => radius = Mathf.Max(.1f,value);
    public Vector2 LastImpactWorld { get; private set; }
    public int ActiveRipples {
        get { int n = 0; foreach (var p in _impacts) if (p.z >= 0f && p.z < RippleDuration) n++; return n; }
    }

    private void Awake()
    {
        _boss = GetComponent<Scene2Boss>();
        var shader = Resources.Load<Shader>("Scene2/Shield/ReactiveShield");
        if (shader == null) { Debug.LogError("[Scene2] Reactive shield shader missing."); enabled = false; return; }
        _material = new Material(shader);
        _material.SetTexture("_DetailTex", Resources.Load<Texture2D>("Scene2/Shield/ShieldDetail"));
        _root = new GameObject("Scene2ReactiveShield").transform;
        _root.SetParent(transform, false);
        _mesh = Scene2ShieldGeometry.Create();
        _root.gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
        _renderer = _root.gameObject.AddComponent<MeshRenderer>();
        _renderer.sharedMaterial = _material;
        _renderer.sortingOrder = 6;
        _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _renderer.receiveShadows = false;
        _properties = new MaterialPropertyBlock();
        ClearImpacts();
        WriteProperties();
    }

    private bool CanAnimate() => Time.timeScale > 0f &&
        (GameManager.Instance == null || GameManager.Instance.IsWaveSimActive()) &&
        (PauseMenuController.Instance == null || !PauseMenuController.Instance.IsOpen);

    private void LateUpdate()
    {
        if (_root == null) return;
        if (_boss != null && _boss.IsDead) { _renderer.enabled = false; return; }
        if (CanAnimate())
        {
            _clock += Time.deltaTime;
            _formation = Mathf.Min(1f, _formation + Time.deltaTime / 0.25f);
            for (int i = 0; i < _impacts.Length; i++)
                if (_impacts[i].z >= 0f) _impacts[i].z += Time.deltaTime;
        }
        var scale = transform.lossyScale;
        float size = radius;
        _root.localScale = new Vector3(size / Mathf.Max(0.001f, Mathf.Abs(scale.x)),
            size / Mathf.Max(0.001f, Mathf.Abs(scale.y)), size / Mathf.Max(0.001f, Mathf.Abs(scale.z)));
        WriteProperties();
    }

    // Separate MonoBehaviour receiver preserves EnemyBase's collision damage handler.
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag("Ball") && collision.gameObject.GetComponent<SplitPhantomBall>() == null) return;
        if (_boss != null && _boss.IsDead) return;
        Vector2 point = collision.contactCount > 0 ? collision.GetContact(0).point : (Vector2)collision.transform.position;
        if (PulseAt(point)) CollisionResponses++;
    }

    public bool PulseAt(Vector2 worldPoint)
    {
        if (_renderer == null || _exposed || IsPlaying || !CanAnimate()) return false;
        Vector2 p = (worldPoint - (Vector2)transform.position) / radius;
        p = Vector2.ClampMagnitude(p, 0.97f);
        _impacts[_nextImpact] = new Vector4(p.x, p.y, 0f, 1f);
        _nextImpact = (_nextImpact + 1) % _impacts.Length;
        LastImpactWorld = worldPoint;
        WriteProperties();
        return true;
    }

    public void Begin()
    {
        if (_renderer == null) return;
        ClearImpacts();
        _exposed = false;
        IsPlaying = true;
        Show(0f);
    }

    public void Show(float progress)
    {
        Progress = Mathf.Clamp01(progress);
        WriteProperties();
    }

    public void End()
    {
        if (IsPlaying) _exposed = true;
        IsPlaying = false;
        ClearImpacts();
        WriteProperties();
    }

    public void SetExposed(bool exposed)
    {
        if (_exposed && !exposed) _formation = 0f;
        _exposed = exposed;
        if (exposed) ClearImpacts();
        WriteProperties();
    }

    private void ClearImpacts()
    {
        for (int i = 0; i < _impacts.Length; i++) _impacts[i] = new Vector4(0f,0f,-1f,0f);
    }

    private void WriteProperties()
    {
        if (_renderer == null) return;
        _renderer.enabled = enabled && !_exposed && (_boss == null || !_boss.IsDead) &&
            (GameManager.Instance == null || GameManager.Instance.State != GameState.GameOver);
        _properties.SetFloat("_Clock", _clock);
        _properties.SetFloat("_IdleMotion", IsPlaying ? 0f : 1f);
        _properties.SetFloat("_Formation", DOVirtual.EasedValue(0f, 1f, _formation, Ease.OutCubic));
        // Sample DOTween easing from the director's pause-aware reveal clock.
        float dissolve = IsPlaying ? DOVirtual.EasedValue(0f, 1f,
            Mathf.Clamp01((Progress - 0.2f) / 0.8f), Ease.InOutSine) : 0f;
        _properties.SetFloat("_Dissolve", dissolve);
        _properties.SetFloat("_Charge", IsPlaying ? Mathf.Sin(Progress * Mathf.PI) : 0f);
        for (int i = 0; i < _impacts.Length; i++) _properties.SetVector(ImpactIds[i], _impacts[i]);
        _renderer.SetPropertyBlock(_properties);
    }

    private void OnDisable()
    {
        IsPlaying = false;
        ClearImpacts();
        if (_renderer != null) _renderer.enabled = false;
    }

    private void OnDestroy()
    {
        if (_material != null) Destroy(_material);
        if (_mesh != null) Destroy(_mesh);
    }
}
