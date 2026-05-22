using UnityEngine;

public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    public enum Preset { Light, Medium, Heavy }

    private GameConfig _config;
    private GameConfig Config
    {
        get
        {
            if (_config == null && GameManager.Instance != null)
                _config = GameManager.Instance.config;
            return _config;
        }
    }

    private Vector3 _originPos;
    private float _trauma;       // 0-1，衰减值
    private float _noiseSeed;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        _originPos = transform.localPosition;
        _noiseSeed = Random.Range(0f, 100f);
    }

    private void LateUpdate()
    {
        if (_trauma <= 0f || Config == null)
        {
            transform.localPosition = _originPos;
            return;
        }

        // 平方曲线：低 trauma 时几乎无抖动，高 trauma 时强烈
        float shake = _trauma * _trauma;
        float t = Time.time * 22f;
        float ox = (Mathf.PerlinNoise(_noiseSeed + t, 0f) * 2f - 1f) * Config.shakeMaxOffset * shake;
        float oy = (Mathf.PerlinNoise(0f, _noiseSeed + t) * 2f - 1f) * Config.shakeMaxOffset * shake;
        transform.localPosition = _originPos + new Vector3(ox, oy, 0f);

        _trauma = Mathf.Max(0f, _trauma - Config.shakeDecaySpeed * Time.deltaTime);
    }

    public void Shake(Preset preset)
    {
        if (Config == null) return;
        float trauma;
        switch (preset)
        {
            case Preset.Light:  trauma = Config.shakeTraumaLight;  break;
            case Preset.Medium: trauma = Config.shakeTraumaMedium; break;
            default:            trauma = Config.shakeTraumaHeavy;  break;
        }
        AddTrauma(trauma);
    }

    public void AddTrauma(float amount)
    {
        _trauma = Mathf.Clamp01(_trauma + amount);
    }
}
