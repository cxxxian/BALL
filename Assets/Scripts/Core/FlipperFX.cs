using UnityEngine;

/// <summary>
/// 挂在 Flipper_Left / Flipper_Right 上。
/// 当挡板挥击（激活）时，在子物体 SpriteRenderer 上通过 MaterialPropertyBlock
/// 写入 _HitFlash 属性，触发 TronFlipper shader 的白色闪光效果，无需实例化 Material。
/// </summary>
[RequireComponent(typeof(FlipperController))]
public class FlipperFX : MonoBehaviour
{
    [Header("闪光参数")]
    [Tooltip("激活时的初始闪光强度")]
    public float flashPeak     = 1.0f;
    [Tooltip("闪光衰减时间 (秒)")]
    public float flashDuration = 0.12f;

    private SpriteRenderer     _sr;
    private MaterialPropertyBlock _mpb;
    private FlipperController  _flipper;

    private float _flashValue  = 0f;
    private bool  _wasActive   = false;

    private static readonly int HitFlashID = Shader.PropertyToID("_HitFlash");

    private void Awake()
    {
        _flipper = GetComponent<FlipperController>();
        // 找子物体的 SpriteRenderer（Body）
        _sr = GetComponentInChildren<SpriteRenderer>();
        _mpb = new MaterialPropertyBlock();
    }

    private void Update()
    {
        bool isActive = _flipper != null && _flipper.IsActivated;

        // 激活瞬间触发闪光
        if (isActive && !_wasActive)
            _flashValue = flashPeak;

        _wasActive = isActive;

        // 衰减
        if (_flashValue > 0f)
        {
            _flashValue -= Time.deltaTime / Mathf.Max(flashDuration, 0.01f);
            _flashValue  = Mathf.Clamp01(_flashValue);
        }

        // 写入 MaterialPropertyBlock（不创建材质实例）
        if (_sr != null)
        {
            _sr.GetPropertyBlock(_mpb);
            _mpb.SetFloat(HitFlashID, _flashValue);
            _sr.SetPropertyBlock(_mpb);
        }
    }
}
