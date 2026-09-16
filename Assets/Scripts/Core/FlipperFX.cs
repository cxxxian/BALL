using UnityEngine;

/// <summary>
/// 挡板受击/挥击闪光：只改 _HitFlash 颜色亮度，不做外形缩放。
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

    private SpriteRenderer[] _renderers;
    private MaterialPropertyBlock _mpb;
    private FlipperController _flipper;

    private float _flashValue;
    private float _holdLeft;
    private bool _wasActive;

    private static readonly int HitFlashID = Shader.PropertyToID("_HitFlash");

    public void TriggerCatchFlash() => Pulse(flashPeak);

    public void TriggerContactFlash() => Pulse(flashPeak);

    public void Pulse(float peak)
    {
        _flashValue = Mathf.Max(_flashValue, Mathf.Clamp01(peak));
        _holdLeft = Mathf.Max(_holdLeft, holdDuration);
        ApplyFlash();
    }

    private void Awake()
    {
        _flipper = GetComponent<FlipperController>();
        _renderers = GetComponentsInChildren<SpriteRenderer>(true);
        _mpb = new MaterialPropertyBlock();
        ApplyFlash();
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

        ApplyFlash();
    }

    private void ApplyFlash()
    {
        if (_renderers == null) return;
        float v = Mathf.Clamp01(_flashValue);
        for (int i = 0; i < _renderers.Length; i++)
        {
            var sr = _renderers[i];
            if (sr == null) continue;
            sr.GetPropertyBlock(_mpb);
            _mpb.SetFloat(HitFlashID, v);
            sr.SetPropertyBlock(_mpb);
        }
    }
}
