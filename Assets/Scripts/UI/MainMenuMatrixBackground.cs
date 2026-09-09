using UnityEngine;

/// <summary>
/// 主菜单代码雨搅动：Stable Fluids + Vorticity Confinement。
/// 算法思路对齐 https://github.com/MagicStones23/Unity-Fluid-Simulation
/// （Blit 实现，用速度场扭曲字雨，不做径向吸附）。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public class MainMenuMatrixBackground : MonoBehaviour
{
    private static readonly int RainTimeId = Shader.PropertyToID("_RainTime");
    private static readonly int WakeMapId = Shader.PropertyToID("_WakeMap");
    private static readonly int WakeOriginId = Shader.PropertyToID("_WakeOrigin");
    private static readonly int WakeSizeId = Shader.PropertyToID("_WakeSize");
    private static readonly int WakeScaleId = Shader.PropertyToID("_WakeScale");
    private static readonly int WakeEnabledId = Shader.PropertyToID("_WakeEnabled");

    private static readonly int DtId = Shader.PropertyToID("_Dt");
    private static readonly int DissipationId = Shader.PropertyToID("_Dissipation");
    private static readonly int VorticityId = Shader.PropertyToID("_Vorticity");
    private static readonly int DampingId = Shader.PropertyToID("_Damping");
    private static readonly int SplatUVId = Shader.PropertyToID("_SplatUV");
    private static readonly int SplatVelId = Shader.PropertyToID("_SplatVel");
    private static readonly int CurlTexId = Shader.PropertyToID("_CurlTex");
    private static readonly int DivTexId = Shader.PropertyToID("_DivTex");
    private static readonly int PressureTexId = Shader.PropertyToID("_PressureTex");

    private const string MidRainShaderName = "Custom/TronArenaMidRain";
    private const string MidRainShaderPath = "Assets/Shaders/TronArenaMidRain.shader";
    private const string FluidShaderName = "Hidden/MenuWakeField";
    private const string FluidShaderPath = "Assets/Shaders/MenuWakeField.shader";

    private const int PassAdvect = 0;
    private const int PassCurl = 1;
    private const int PassVorticity = 2;
    private const int PassDivergence = 3;
    private const int PassJacobi = 4;
    private const int PassProject = 5;
    private const int PassDamp = 6;
    private const int PassSplat = 7;

    [SerializeField] private Camera targetCamera;
    [SerializeField] private float padding = 0.5f;
    [SerializeField] private Shader midRainShader;
    [SerializeField] private Shader fluidShader;
    [SerializeField] private int resolution = 256;
    [SerializeField] [Range(4, 40)] private int pressureIterations = 24;
    [SerializeField] [Range(0f, 40f)] private float vorticity = 8f;
    [SerializeField] [Range(0.9f, 1f)] private float velocityDissipation = 0.982f;
    [SerializeField] [Range(0.9f, 1f)] private float velocityDamping = 0.993f;
    [SerializeField] private float splatRadius = 0.042f;
    [SerializeField] private float splatForce = 0.7f;
    [SerializeField] private float moveSpeedThreshold = 0.55f;
    [SerializeField] private float wakeDisplaceScale = 0.28f;
    [SerializeField] private float velocitySmooth = 18f;

    private MeshFilter _rainFilter;
    private Material _midMat;
    private Material _fluidMat;
    private Texture2D _glyphAtlas;
    private SpriteRenderer _vignetteSr;
    private float _worldWidth;
    private float _worldHeight;
    private float _lastAspect = -1f;
    private Vector2 _lastMouseWorld;
    private Vector2 _smoothVelocity;
    private bool _hasMouseSample;
    private bool _built;

    private RenderTexture _velA;
    private RenderTexture _velB;
    private RenderTexture _pressureA;
    private RenderTexture _pressureB;
    private RenderTexture _divRT;
    private RenderTexture _curlRT;

    private void Awake()
    {
        TryBuild();
    }

    private void Start()
    {
        TryBuild();
    }

    private void OnDestroy()
    {
        if (_midMat != null) Destroy(_midMat);
        if (_fluidMat != null) Destroy(_fluidMat);
        if (_glyphAtlas != null) Destroy(_glyphAtlas);
        ReleaseRTs();
    }

    private void LateUpdate()
    {
        if (!_built)
            TryBuild();

        if (!_built)
            return;

        if (_midMat != null)
        {
            _midMat.SetFloat(RainTimeId, Time.unscaledTime);
            UpdateFluid();
        }

        if (targetCamera != null && !Mathf.Approximately(targetCamera.aspect, _lastAspect))
            ResizeToCamera();
    }

    private void TryBuild()
    {
        if (_built)
            return;

        if (targetCamera == null)
            targetCamera = Camera.main;
        if (targetCamera == null)
            targetCamera = FindAnyObjectByType<Camera>();
        if (targetCamera == null)
            return;

        BuildBackground();
        _built = transform.childCount > 0;
    }

    private void UpdateFluid()
    {
        if (targetCamera == null || _midMat == null || _fluidMat == null || _velA == null)
            return;

        float dt = Mathf.Clamp(Time.unscaledDeltaTime, 0.001f, 0.033f);

        Vector3 screen = Input.mousePosition;
        screen.z = Mathf.Abs(targetCamera.transform.position.z);
        Vector2 mouseWorld = targetCamera.ScreenToWorldPoint(screen);

        if (_hasMouseSample)
        {
            Vector2 rawVel = (mouseWorld - _lastMouseWorld) / dt;
            float t = 1f - Mathf.Exp(-velocitySmooth * dt);
            _smoothVelocity = Vector2.Lerp(_smoothVelocity, rawVel, t);
        }
        else
        {
            _hasMouseSample = true;
            _smoothVelocity = Vector2.zero;
        }

        _lastMouseWorld = mouseWorld;

        Vector2 origin = targetCamera.transform.position;
        Vector2 uv = new Vector2(
            (mouseWorld.x - origin.x) / Mathf.Max(_worldWidth, 0.001f) + 0.5f,
            (mouseWorld.y - origin.y) / Mathf.Max(_worldHeight, 0.001f) + 0.5f);

        // 鼠标速度 → UV 空间速度（与 Advect 一致）
        Vector2 velUv = new Vector2(
            _smoothVelocity.x / Mathf.Max(_worldWidth, 0.001f),
            _smoothVelocity.y / Mathf.Max(_worldHeight, 0.001f));

        float speed = _smoothVelocity.magnitude;
        float splatStrength = 0f;
        Vector2 injectVel = Vector2.zero;
        if (speed >= moveSpeedThreshold)
        {
            injectVel = velUv;
            float uvSpeed = injectVel.magnitude;
            if (uvSpeed > 1e-5f)
            {
                float capped = Mathf.Min(uvSpeed, 2.5f);
                injectVel = injectVel / uvSpeed * capped;
                splatStrength = splatForce * Mathf.Clamp01(speed / 8f);
            }
        }

        _fluidMat.SetFloat(DtId, dt);
        _fluidMat.SetFloat(DissipationId, velocityDissipation);
        _fluidMat.SetFloat(VorticityId, vorticity);
        _fluidMat.SetFloat(DampingId, velocityDamping);

        // 1) Advect
        Graphics.Blit(_velA, _velB, _fluidMat, PassAdvect);
        SwapVel();

        // 2) Curl + Vorticity confinement（MagicStones 里让漩涡“活”起来的关键）
        Graphics.Blit(_velA, _curlRT, _fluidMat, PassCurl);
        _fluidMat.SetTexture(CurlTexId, _curlRT);
        Graphics.Blit(_velA, _velB, _fluidMat, PassVorticity);
        SwapVel();

        // 3) Pressure projection
        Graphics.Blit(_velA, _divRT, _fluidMat, PassDivergence);
        ClearRT(_pressureA);
        ClearRT(_pressureB);
        _fluidMat.SetTexture(DivTexId, _divRT);
        int iters = Mathf.Clamp(pressureIterations, 4, 40);
        for (int i = 0; i < iters; i++)
        {
            Graphics.Blit(_pressureA, _pressureB, _fluidMat, PassJacobi);
            (_pressureA, _pressureB) = (_pressureB, _pressureA);
        }

        _fluidMat.SetTexture(PressureTexId, _pressureA);
        Graphics.Blit(_velA, _velB, _fluidMat, PassProject);
        SwapVel();

        // 4) Damping
        Graphics.Blit(_velA, _velB, _fluidMat, PassDamp);
        SwapVel();

        // 5) Inject：沿滑动方向推水
        _fluidMat.SetVector(SplatUVId, new Vector4(uv.x, uv.y, splatRadius, splatStrength));
        _fluidMat.SetVector(SplatVelId, new Vector4(injectVel.x, injectVel.y, 0f, 0f));
        Graphics.Blit(_velA, _velB, _fluidMat, PassSplat);
        SwapVel();

        _midMat.SetTexture(WakeMapId, _velA);
        _midMat.SetVector(WakeOriginId, new Vector4(origin.x, origin.y, 0f, 0f));
        float invRes = 1f / Mathf.Max(_velA.width, 1);
        _midMat.SetVector(WakeSizeId, new Vector4(_worldWidth, _worldHeight, invRes, invRes));
        _midMat.SetFloat(WakeScaleId, wakeDisplaceScale);
        _midMat.SetFloat(WakeEnabledId, 1f);
    }

    private void SwapVel() => (_velA, _velB) = (_velB, _velA);

    private void BuildBackground()
    {
        if (targetCamera == null)
        {
            Debug.LogWarning("MainMenuMatrixBackground: No camera found.");
            return;
        }

        targetCamera.clearFlags = CameraClearFlags.SolidColor;
        // HTML stage: #07080f
        targetCamera.backgroundColor = new Color(0.027f, 0.031f, 0.059f, 1f);

        _glyphAtlas = MatrixGlyphAtlas.CreateAtlas();

        if (midRainShader == null)
            midRainShader = LoadShader(MidRainShaderName, MidRainShaderPath);
        if (fluidShader == null)
            fluidShader = LoadShader(FluidShaderName, FluidShaderPath);

        CreateRainLayer();
        CreateFluid();
        CreateVignette();
        ResizeToCamera();
    }

    private void CreateFluid()
    {
        if (fluidShader == null)
        {
            Debug.LogWarning("MainMenuMatrixBackground: Fluid shader missing.");
            return;
        }

        _fluidMat = new Material(fluidShader);
        EnsureRTs();
        ClearAllFluidRTs();
    }

    private void EnsureRTs()
    {
        int res = Mathf.ClosestPowerOfTwo(Mathf.Clamp(resolution, 64, 512));
        if (_velA != null && _velA.width == res) return;

        ReleaseRTs();
        _velA = CreateRT(res, RenderTextureFormat.RGHalf, "MenuFluidVel");
        _velB = CreateRT(res, RenderTextureFormat.RGHalf, "MenuFluidVelTmp");
        _pressureA = CreateRT(res, RenderTextureFormat.RHalf, "MenuFluidPressure");
        _pressureB = CreateRT(res, RenderTextureFormat.RHalf, "MenuFluidPressureTmp");
        _divRT = CreateRT(res, RenderTextureFormat.RHalf, "MenuFluidDiv");
        _curlRT = CreateRT(res, RenderTextureFormat.RHalf, "MenuFluidCurl");
    }

    private static RenderTexture CreateRT(int res, RenderTextureFormat format, string name)
    {
        var desc = new RenderTextureDescriptor(res, res, format, 0)
        {
            msaaSamples = 1,
            useMipMap = false,
            autoGenerateMips = false,
            sRGB = false
        };
        var rt = new RenderTexture(desc)
        {
            name = name,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        rt.Create();
        return rt;
    }

    private void ClearAllFluidRTs()
    {
        ClearRT(_velA);
        ClearRT(_velB);
        ClearRT(_pressureA);
        ClearRT(_pressureB);
        ClearRT(_divRT);
        ClearRT(_curlRT);
    }

    private static void ClearRT(RenderTexture rt)
    {
        if (rt == null) return;
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        GL.Clear(true, true, Color.clear);
        RenderTexture.active = prev;
    }

    private void ReleaseRTs()
    {
        ReleaseRT(ref _velA);
        ReleaseRT(ref _velB);
        ReleaseRT(ref _pressureA);
        ReleaseRT(ref _pressureB);
        ReleaseRT(ref _divRT);
        ReleaseRT(ref _curlRT);
    }

    private static void ReleaseRT(ref RenderTexture rt)
    {
        if (rt == null) return;
        rt.Release();
        Object.Destroy(rt);
        rt = null;
    }

    private void ResizeToCamera()
    {
        if (targetCamera == null) return;

        _lastAspect = targetCamera.aspect;
        float halfH = targetCamera.orthographicSize;
        float halfW = halfH * _lastAspect;
        _worldWidth = halfW * 2f + padding;
        _worldHeight = halfH * 2f + padding;

        if (_rainFilter != null)
        {
            if (_rainFilter.sharedMesh != null)
                Destroy(_rainFilter.sharedMesh);
            _rainFilter.sharedMesh = BuildQuadMesh(_worldWidth, _worldHeight);
        }

        if (_vignetteSr != null && _vignetteSr.sprite != null)
        {
            var tex = _vignetteSr.sprite.texture;
            float ppu = tex.width / _worldWidth;
            _vignetteSr.sprite = Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                ppu);
        }

        transform.position = new Vector3(
            targetCamera.transform.position.x,
            targetCamera.transform.position.y,
            0f);

        EnsureRTs();
    }

    private void CreateRainLayer()
    {
        var go = new GameObject("MenuRain");
        go.transform.SetParent(transform, false);

        _rainFilter = go.AddComponent<MeshFilter>();
        _rainFilter.sharedMesh = BuildQuadMesh(1f, 1f);

        var meshRenderer = go.AddComponent<MeshRenderer>();
        meshRenderer.sortingLayerName = "Default";
        meshRenderer.sortingOrder = -200;

        if (midRainShader == null)
        {
            Debug.LogError("MainMenuMatrixBackground: Matrix rain shader missing.");
            midRainShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        }

        _midMat = new Material(midRainShader);
        meshRenderer.sharedMaterial = _midMat;
        MatrixGlyphAtlas.ApplyToMaterial(_midMat, _glyphAtlas);

        // HTML stage #07080f; rain readable but still below UI
        var bg = new Color(0.027f, 0.031f, 0.059f, 1f);
        _midMat.SetColor("_BgColor", bg);
        _midMat.SetColor("_BandColor", new Color(0f, 0.04f, 0.07f, 1f));
        _midMat.SetFloat("_BandStrength", 0.035f);
        _midMat.SetColor("_HeadColor", new Color(0.35f, 1f, 0.92f, 1f));
        _midMat.SetColor("_TrailColor", new Color(0f, 0.22f, 0.26f, 1f));
        _midMat.SetFloat("_ColumnWidth", 0.4f);
        _midMat.SetFloat("_CharHeight", 0.32f);
        _midMat.SetFloat("_ColumnDensity", 0.88f);
        _midMat.SetFloat("_FallSpeedMin", 0.4f);
        _midMat.SetFloat("_FallSpeedMax", 1.1f);
        _midMat.SetFloat("_HeadBright", 1.05f);
        _midMat.SetFloat("_TrailBright", 0.28f);
        _midMat.SetFloat("_GlyphChangeRate", 6f);
        _midMat.SetFloat("_RainStrength", 0.88f);
        // Floor stays quiet under rain
        _midMat.SetColor("_GridColor", new Color(0f, 1f, 1f, 0.035f));
        _midMat.SetFloat("_GridSize", 0.9f);
        _midMat.SetFloat("_LineWidth", 0.035f);
        _midMat.SetFloat("_GridOverlay", 0f);
        _midMat.SetFloat("_GridDriftSpeed", 0.04f);
        _midMat.SetFloat("_PerspectiveFloor", 1f);
        _midMat.SetFloat("_FloorHorizon", 0.64f);
        _midMat.SetFloat("_FloorCellScale", 8f);
        _midMat.SetFloat("_FloorScroll", 0f);
        _midMat.SetFloat("_FloorLineWidth", 0.011f);
        _midMat.SetFloat("_FloorSideExtend", 1.25f);
        _midMat.SetFloat("_ScanSpeed", 0.25f);
        _midMat.SetFloat("_ScanlineStr", 0.03f);
        _midMat.SetFloat("_Brightness", 1f);
        _midMat.SetFloat("_RainBoost", 1f);
        _midMat.SetFloat("_TrailFadePower", 2.4f);
        _midMat.SetFloat("_ComboBoost", 0f);
        _midMat.SetFloat("_ScanBandActive", 0f);
        _midMat.SetFloat(WakeEnabledId, 0f);
        _midMat.SetFloat(WakeScaleId, 0f);
    }

    private void CreateVignette()
    {
        var go = new GameObject("MenuRain_Vignette");
        go.transform.SetParent(transform, false);

        _vignetteSr = go.AddComponent<SpriteRenderer>();
        _vignetteSr.sortingLayerName = "Default";
        _vignetteSr.sortingOrder = -199;

        var tex = GenerateVignetteTexture(128, 256);
        _vignetteSr.sprite = Sprite.Create(
            tex,
            new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f),
            100f);
        _vignetteSr.sharedMaterial = new Material(
            Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
    }

    private static Shader LoadShader(string shaderName, string assetPath)
    {
        var shader = Shader.Find(shaderName);
#if UNITY_EDITOR
        if (shader == null)
            shader = UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
#endif
        return shader;
    }

    private static Texture2D GenerateVignetteTexture(int width, int height)
    {
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "MenuRainVignette"
        };

        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            float v = (float)y / (height - 1);
            for (int x = 0; x < width; x++)
            {
                float u = (float)x / (width - 1);
                // Soft edge/corner only — center clear so rain reads.
                // NOTE: Mathf.SmoothStep(from,to,t) ≠ HLSL smoothstep(edge0,edge1,x)
                float dx = (u - 0.5f) * 2f;
                float dy = (v - 0.5f) * 2.1f;
                float radial = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Hermite01(Mathf.InverseLerp(1.05f, 1.55f, radial)) * 0.5f;
                a = Mathf.Max(a, Hermite01(Mathf.InverseLerp(0.92f, 1f, v)) * 0.22f);
                a = Mathf.Max(a, Hermite01(Mathf.InverseLerp(0.08f, 0f, v)) * 0.3f);
                pixels[y * width + x] = new Color32(0, 0, 0, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, false);
        return tex;
    }

    private static float Hermite01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private static Mesh BuildQuadMesh(float width, float height)
    {
        var mesh = new Mesh { name = "MenuRainQuad" };
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
}
