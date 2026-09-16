using UnityEngine;

/// <summary>
/// 外框墙撞击脉冲：把撞击点投影到 PlayfieldTopArc 弧长，驱动 SpriteNeonHDR 的双向扩散高亮。
/// 挂在 PlayfieldTopArc 同物体上（或场景中任意处，会自动找外框）。
/// </summary>
[DisallowMultipleComponent]
public class PlayfieldWallPulse : MonoBehaviour
{
    public static PlayfieldWallPulse Instance { get; private set; }

    [Header("Refs")]
    public PlayfieldTopArc frame;
    public MeshRenderer frameRenderer;

    [Header("Pulse Feel")]
    [Tooltip("沿墙扩散速度（世界单位/秒）")]
    public float spreadSpeed = 16f;
    [Tooltip("高亮带宽")]
    public float bandWidth = 0.7f;
    [Tooltip("单次脉冲寿命")]
    public float duration = 0.65f;
    [Tooltip("最低撞击强度才触发（相对速度倍率后）")]
    public float minIntensity = 0.12f;

    private MaterialPropertyBlock _mpb;
    private readonly float[] _pulseS = new float[4];
    private readonly float[] _pulseAge = { -1f, -1f, -1f, -1f };
    private int _write;

    private static readonly int Pulse0Id = Shader.PropertyToID("_WallPulse0");
    private static readonly int Pulse1Id = Shader.PropertyToID("_WallPulse1");
    private static readonly int SpreadId = Shader.PropertyToID("_PulseSpread");
    private static readonly int WidthId = Shader.PropertyToID("_PulseWidth");
    private static readonly int DurationId = Shader.PropertyToID("_PulseDuration");
    private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");

    private void Awake()
    {
        Instance = this;
        _mpb = new MaterialPropertyBlock();
        ResolveRefs();
        ClearPulses();
        ApplyMpb();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnEnable()
    {
        ResolveRefs();
        if (frame != null)
            frame.Rebuild();
        ApplyMpb();
    }

    private void Update()
    {
        bool any = false;
        for (int i = 0; i < 4; i++)
        {
            if (_pulseAge[i] < 0f) continue;
            _pulseAge[i] += Time.deltaTime;
            if (_pulseAge[i] >= duration)
                _pulseAge[i] = -1f;
            else
                any = true;
        }

        if (any || _write != 0)
            ApplyMpb();
    }

    public void TriggerAtWorld(Vector2 worldPos, float intensity = 1f)
    {
        ResolveRefs();
        if (frame == null) return;
        if (!frame.TryProjectWorldToArcLength(worldPos, out float s, out _))
            return;

        intensity = Mathf.Clamp01(intensity);
        if (intensity < minIntensity) return;

        int slot = _write % 4;
        _write++;
        _pulseS[slot] = s;
        // 强度弱则略缩短可视寿命：用负偏移挤出寿命前端
        _pulseAge[slot] = 0f;
        ApplyMpb();
    }

    public static void NotifyHit(Vector2 worldPos, float intensity)
    {
        if (Instance == null)
        {
                var existing = Object.FindObjectOfType<PlayfieldWallPulse>();
                if (existing != null)
                    Instance = existing;
            else
            {
                var arc = Object.FindObjectOfType<PlayfieldTopArc>();
                if (arc == null) return;
                Instance = arc.GetComponent<PlayfieldWallPulse>();
                if (Instance == null)
                    Instance = arc.gameObject.AddComponent<PlayfieldWallPulse>();
            }
        }

        Instance.TriggerAtWorld(worldPos, intensity);
    }

    private void ResolveRefs()
    {
        if (frame == null)
            frame = GetComponent<PlayfieldTopArc>();
        if (frame == null)
            frame = Object.FindObjectOfType<PlayfieldTopArc>();
        if (frameRenderer == null && frame != null)
            frameRenderer = frame.FrameRenderer;
        if (frameRenderer == null)
            frameRenderer = GetComponent<MeshRenderer>();
    }

    private void ClearPulses()
    {
        for (int i = 0; i < 4; i++)
            _pulseAge[i] = -1f;
    }

    private void ApplyMpb()
    {
        if (frameRenderer == null) return;
        if (_mpb == null) _mpb = new MaterialPropertyBlock();

        frameRenderer.GetPropertyBlock(_mpb);
        _mpb.SetVector(Pulse0Id, new Vector4(
            _pulseS[0], _pulseAge[0],
            _pulseS[1], _pulseAge[1]));
        _mpb.SetVector(Pulse1Id, new Vector4(
            _pulseS[2], _pulseAge[2],
            _pulseS[3], _pulseAge[3]));
        _mpb.SetFloat(SpreadId, spreadSpeed);
        _mpb.SetFloat(WidthId, bandWidth);
        _mpb.SetFloat(DurationId, duration);
        // 青白 HDR，贴霓虹区
        _mpb.SetColor(FlashColorId, new Color(1.15f, 1.4f, 1.7f, 1f));
        frameRenderer.SetPropertyBlock(_mpb);
    }
}
