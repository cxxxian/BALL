using UnityEngine;

/// <summary>
/// 挡板受击闪光 + 武器积能辉光（左右挡板共享能量，辉光同步）。
/// </summary>
[RequireComponent(typeof(FlipperController))]
public class FlipperFX : MonoBehaviour
{
    [Header("闪光参数")]
    [Tooltip("激活/接球时的峰值")]
    public float flashPeak = 1.0f;
    [Tooltip("峰值保持时长后再衰减")]
    public float holdDuration = 0.05f;
    [Tooltip("闪光衰减时间 (秒)")]
    public float flashDuration = 0.14f;

    [Header("积能辉光")]
    [Tooltip("满能时相对基础强度的倍率")]
    public float chargeIntensityMult = 2.4f;
    [Tooltip("READY 时额外脉冲强度")]
    public float readyPulseAmp = 0.55f;

    private SpriteRenderer[] _renderers;
    private MaterialPropertyBlock _mpb;
    private FlipperController _flipper;

    private float _flashValue;
    private float _holdLeft;
    private bool _wasActive;
    private float _baseIntensity = 2.35f;
    private Color _baseTint = Color.white;
    private bool _hasNeonProps;

    private static readonly int HitFlashID = Shader.PropertyToID("_HitFlash");
    private static readonly int NeonIntensityID = Shader.PropertyToID("_NeonIntensity");
    private static readonly int NeonTintID = Shader.PropertyToID("_NeonTint");

    public void TriggerCatchFlash() => Pulse(flashPeak);

    public void TriggerContactFlash() => Pulse(flashPeak);

    public void Pulse(float peak)
    {
        _flashValue = Mathf.Max(_flashValue, Mathf.Clamp01(peak));
        _holdLeft = Mathf.Max(_holdLeft, holdDuration);
        ApplyVisuals();
    }

    private void Awake()
    {
        _flipper = GetComponent<FlipperController>();
        _renderers = GetComponentsInChildren<SpriteRenderer>(true);
        _mpb = new MaterialPropertyBlock();
        DisableSwingTrail();
        CacheBaseNeon();
        ApplyVisuals();
    }

    /// <summary>关掉挡板尖端 Trail（挥动拉光）；教学/正式局共用本组件。</summary>
    private void DisableSwingTrail()
    {
        var trails = GetComponentsInChildren<TrailRenderer>(true);
        for (int i = 0; i < trails.Length; i++)
        {
            var trail = trails[i];
            if (trail == null) continue;
            trail.Clear();
            trail.enabled = false;
            trail.emitting = false;
        }
    }

    private void CacheBaseNeon()
    {
        if (_renderers == null) return;
        for (int i = 0; i < _renderers.Length; i++)
        {
            var sr = _renderers[i];
            if (sr == null || sr.sharedMaterial == null) continue;
            var mat = sr.sharedMaterial;
            if (!mat.HasProperty(NeonIntensityID)) continue;
            _hasNeonProps = true;
            _baseIntensity = mat.GetFloat(NeonIntensityID);
            if (mat.HasProperty(NeonTintID))
                _baseTint = mat.GetColor(NeonTintID);
            return;
        }
    }

    private void Update()
    {
        bool isActive = _flipper != null && _flipper.IsActivated;
        if (isActive && !_wasActive)
            Pulse(flashPeak);
        _wasActive = isActive;

        if (_holdLeft > 0f)
        {
            _holdLeft -= Time.deltaTime;
            _flashValue = Mathf.Max(_flashValue, flashPeak * 0.85f);
        }
        else if (_flashValue > 0f)
        {
            _flashValue -= Time.deltaTime / Mathf.Max(flashDuration, 0.01f);
            _flashValue = Mathf.Clamp01(_flashValue);
        }

        ApplyVisuals();
    }

    private void ApplyVisuals()
    {
        if (_renderers == null) return;

        float flash = Mathf.Clamp01(_flashValue);
        float charge = 0f;
        bool ready = false;
        Color weaponColor = _baseTint;

        var weapon = FlipperWeaponController.Instance;
        if (weapon != null)
        {
            charge = Mathf.Clamp01(weapon.EnergyRatio);
            ready = weapon.IsReady;
            if (weapon.EquippedWeapon != null)
                weaponColor = weapon.EquippedWeapon.effectColor;
        }

        // 低能量几乎不亮，后半段加速点亮，满能明显可读
        float chargeCurve = charge * charge;
        float intensity = _baseIntensity;
        if (_hasNeonProps)
        {
            intensity = Mathf.Lerp(_baseIntensity, _baseIntensity * chargeIntensityMult, chargeCurve);
            if (ready)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7f);
                intensity += _baseIntensity * readyPulseAmp * pulse;
            }
        }

        Color tint = _baseTint;
        if (_hasNeonProps && charge > 0.02f)
        {
            Color chargeTint = Color.Lerp(_baseTint, weaponColor, 0.35f + chargeCurve * 0.55f);
            if (ready)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7f);
                chargeTint = Color.Lerp(chargeTint, Color.white, pulse * 0.25f);
            }
            tint = chargeTint;
        }

        for (int i = 0; i < _renderers.Length; i++)
        {
            var sr = _renderers[i];
            if (sr == null) continue;
            sr.GetPropertyBlock(_mpb);
            _mpb.SetFloat(HitFlashID, flash);
            if (_hasNeonProps)
            {
                _mpb.SetFloat(NeonIntensityID, intensity);
                _mpb.SetColor(NeonTintID, tint);
            }
            sr.SetPropertyBlock(_mpb);
        }
    }
}
