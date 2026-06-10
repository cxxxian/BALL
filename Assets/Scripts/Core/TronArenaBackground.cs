using UnityEngine;

/// <summary>
/// 三层电路竞技场背景：远层母线 / 中层扫描带 / 近层纯渐变暗角。
/// </summary>
public class TronArenaBackground : MonoBehaviour
{
    public static TronArenaBackground Instance { get; private set; }

    private static readonly int ScanBandYId = Shader.PropertyToID("_ScanBandY");
    private static readonly int ScanBandActiveId = Shader.PropertyToID("_ScanBandActive");

    [Header("World (defaults overridden by GameConfig)")]
    public float worldWidth  = 9f;
    public float worldHeight = 16f;

    [Header("Parallax — 球上下飞时远/中层反向微移")]
    [Tooltip("战场中心 Y；球相对此值的位移驱动视差")]
    public float battlefieldCenterY = 0f;
    [Tooltip("远层：球位移 × 系数（越小越「远」）")]
    public float farParallaxFactor = 0.12f;
    [Tooltip("中层：系数更大，层差更明显")]
    public float midParallaxFactor = 0.28f;
    [Tooltip("单层最大偏移（世界单位），防止球贴底时错位过大")]
    public float maxParallaxOffset = 0.65f;

    [Header("Combo → Mid Layer")]
    public int   comboBoostStart  = 3;
    public int   comboBoostFull   = 15;
    public float comboSmoothTime  = 0.5f;

    [Header("Flow")]
    public float farDriftSpeed     = 0.045f;
    public float midGridDriftSpeed = 0.09f;
    public float midScanSpeed      = 0.75f;

    [Header("Near Layer — Pure Vignette")]
    public float topVignetteStart  = 0.78f;
    public float bottomVignetteEnd = 0.28f;
    public Color vignetteColor     = new Color(0f, 0.015f, 0.04f, 1f);

    private Transform    _farLayer;
    private Transform    _midLayer;
    private Material     _farMat;
    private Material     _midMat;
    private float        _comboBoost;
    private float        _targetComboBoost;
    private float        _comboBoostVel;
    private float        _scanBandY = -999f;
    private float        _scanBandActive;
    private float        _scanBandPulseTimer;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ApplyConfigDefaults();
    }

    private void Start()
    {
        BuildLayers();

        if (ComboSystem.Instance != null)
            ComboSystem.Instance.onComboChanged.AddListener(OnComboChanged);
    }

    private void OnDestroy()
    {
        if (ComboSystem.Instance != null)
            ComboSystem.Instance.onComboChanged.RemoveListener(OnComboChanged);
        if (_farMat != null) Destroy(_farMat);
        if (_midMat != null) Destroy(_midMat);
        if (Instance == this) Instance = null;
    }

    public void SetScanBand(float worldY, float strength)
    {
        _scanBandY = worldY;
        _scanBandActive = Mathf.Clamp01(strength);
        if (strength > 0f)
            _scanBandPulseTimer = 0f;
    }

    public void PulseGridScanBoost(float strength, float duration)
    {
        _scanBandActive = Mathf.Clamp01(strength);
        _scanBandPulseTimer = Mathf.Max(_scanBandPulseTimer, duration);
    }

    public void ClearScanBand()
    {
        _scanBandActive = 0f;
        _scanBandY = -999f;
        _scanBandPulseTimer = 0f;
    }

    private void ApplyConfigDefaults()
    {
        var cfg = GameManager.Instance != null ? GameManager.Instance.config : null;
        if (cfg == null) return;
        worldWidth  = cfg.worldWidth;
        worldHeight = cfg.worldHeight;
    }

    private void BuildLayers()
    {
        _farLayer = CreateShaderLayer("Layer_Far", -100, "Custom/TronArenaFar", out _farMat);
        _midLayer = CreateShaderLayer("Layer_Mid", -99,  "Custom/TronArenaMid", out _midMat);
        CreateNearLayer();
    }

    private Transform CreateShaderLayer(string layerName, int sortingOrder, string shaderName, out Material mat)
    {
        var go = new GameObject(layerName);
        go.transform.SetParent(transform, false);

        var meshFilter = go.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = BuildQuadMesh(worldWidth, worldHeight);

        var meshRenderer = go.AddComponent<MeshRenderer>();
        meshRenderer.sortingLayerName = "Default";
        meshRenderer.sortingOrder = sortingOrder;

        var shader = Shader.Find(shaderName);
        mat = shader != null
            ? new Material(shader)
            : new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));

        meshRenderer.sharedMaterial = mat;
        return go.transform;
    }

    private void CreateNearLayer()
    {
        var go = new GameObject("Layer_Near");
        go.transform.SetParent(transform, false);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = "Default";
        sr.sortingOrder = -98;

        var tex = GenerateNearTexture(128, 256);
        float ppu = tex.width / worldWidth;
        sr.sprite = Sprite.Create(tex,
            new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f),
            ppu);

        sr.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
        sr.color = Color.white;
    }

    private void Update()
    {
        _comboBoost = Mathf.SmoothDamp(_comboBoost, _targetComboBoost, ref _comboBoostVel, comboSmoothTime);

        if (_farMat != null)
            _farMat.SetFloat("_DriftSpeed", farDriftSpeed);

        if (_midMat != null)
        {
            _midMat.SetFloat("_ComboBoost", _comboBoost);
            _midMat.SetFloat("_GridDriftSpeed", midGridDriftSpeed);
            _midMat.SetFloat("_ScanSpeed", midScanSpeed);

            if (_scanBandPulseTimer > 0f)
            {
                _scanBandPulseTimer -= Time.unscaledDeltaTime;
                if (_scanBandPulseTimer <= 0f)
                    _scanBandActive = 0f;
            }

            _midMat.SetFloat(ScanBandYId, _scanBandY);
            _midMat.SetFloat(ScanBandActiveId, _scanBandActive);
        }

        ApplyParallax();
    }

    private void OnComboChanged(int combo)
    {
        _targetComboBoost = combo <= 0
            ? 0f
            : Mathf.SmoothStep(0f, 1f, (combo - comboBoostStart) / (float)(comboBoostFull - comboBoostStart));
    }

    private void ApplyParallax()
    {
        float ballY = battlefieldCenterY;
        var ball = BallController.Instance;
        if (ball != null && !ball.IsWaitingForLaunch)
            ballY = ball.transform.position.y;

        float delta = ballY - battlefieldCenterY;

        if (_farLayer != null)
        {
            float y = Mathf.Clamp(-delta * farParallaxFactor, -maxParallaxOffset, maxParallaxOffset);
            _farLayer.localPosition = new Vector3(0f, y, 0f);
        }

        if (_midLayer != null)
        {
            float y = Mathf.Clamp(-delta * midParallaxFactor, -maxParallaxOffset, maxParallaxOffset);
            _midLayer.localPosition = new Vector3(0f, y, 0f);
        }
    }

    private static Mesh BuildQuadMesh(float width, float height)
    {
        var mesh = new Mesh { name = "ArenaQuad" };
        float hw = width * 0.5f;
        float hh = height * 0.5f;
        mesh.vertices = new[]
        {
            new Vector3(-hw, -hh, 0f),
            new Vector3( hw, -hh, 0f),
            new Vector3(-hw,  hh, 0f),
            new Vector3( hw,  hh, 0f),
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0f, 1f), new Vector2(1f, 1f),
        };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        mesh.RecalculateBounds();
        return mesh;
    }

    private Texture2D GenerateNearTexture(int width, int height)
    {
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;

        for (int y = 0; y < height; y++)
        {
            float v = (float)y / (height - 1);
            float alpha = 0f;

            if (v > topVignetteStart)
                alpha = Mathf.InverseLerp(topVignetteStart, 1f, v) * 0.5f;
            if (v < bottomVignetteEnd)
                alpha = Mathf.Max(alpha, Mathf.InverseLerp(bottomVignetteEnd, 0f, v) * 0.65f);

            var c = vignetteColor;
            c.a = alpha;

            for (int x = 0; x < width; x++)
                tex.SetPixel(x, y, c);
        }

        tex.Apply();
        return tex;
    }
}
