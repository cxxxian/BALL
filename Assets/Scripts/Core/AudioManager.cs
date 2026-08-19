using UnityEngine;

[DefaultExecutionOrder(-200)]
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    private const int BouncePoolSize = 8;
    private const int SpectrumSize = 512;

    [Header("音效设置")]
    public AudioClip bounceClip;
    [Range(0f, 1f)] public float volume = 0.8f;

    [Header("背景音乐")]
    public AudioClip bgmClip;
    [Range(0f, 1f)] public float bgmVolume = 0.55f;
    public bool playBgmOnStart = true;

    [Header("Combo 音高增强（爽感核心）")]
    public float basePitch = 1.0f;
    public float pitchStep = 0.035f;
    public float maxPitch = 1.5f;

    [Header("性能/去噪")]
    public int maxPlaysPerFrame = 6;

    [Header("频谱")]
    [SerializeField] private FFTWindow spectrumWindow = FFTWindow.BlackmanHarris;
    [SerializeField] private float spectrumSmooth = 12f;

    private AudioSource[] _bouncePool;
    private AudioSource _bgmSource;
    private int _poolCursor;
    private int _playsThisFrame;
    private int _lastFrameCount = -1;

    private readonly float[] _rawSpectrum = new float[SpectrumSize];
    private readonly float[] _smoothedBands = new float[64];
    private int _bandCountCached;
    private float _rms;

    /// <summary>最近一帧平滑后的频带能量（0~1+），长度由 GetBand 调用方决定上限。</summary>
    public float Rms => _rms;
    public bool IsBgmPlaying => _bgmSource != null && _bgmSource.isPlaying;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _bouncePool = new AudioSource[BouncePoolSize];
        for (int i = 0; i < BouncePoolSize; i++)
        {
            var go = new GameObject("BouncePool_" + i);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 0f;
            src.bypassEffects = true;
            src.bypassListenerEffects = true;
            src.bypassReverbZones = true;
            if (bounceClip != null)
                src.clip = bounceClip;
            _bouncePool[i] = src;
        }

        var bgmGo = new GameObject("BgmSource");
        bgmGo.transform.SetParent(transform, false);
        _bgmSource = bgmGo.AddComponent<AudioSource>();
        _bgmSource.playOnAwake = false;
        _bgmSource.loop = true;
        _bgmSource.spatialBlend = 0f;
        _bgmSource.priority = 32;
        _bgmSource.volume = bgmVolume;

        if (bounceClip != null && bounceClip.loadState != AudioDataLoadState.Loaded)
            bounceClip.LoadAudioData();
        if (bgmClip != null && bgmClip.loadState != AudioDataLoadState.Loaded)
            bgmClip.LoadAudioData();
    }

    private void Start()
    {
        if (playBgmOnStart && bgmClip != null)
            PlayBgm(bgmClip);
    }

    private void Update()
    {
        UpdateSpectrum();
    }

    public void PlayBgm(AudioClip clip = null)
    {
        if (_bgmSource == null) return;
        if (clip != null) bgmClip = clip;
        if (bgmClip == null) return;

        if (bgmClip.loadState != AudioDataLoadState.Loaded)
            bgmClip.LoadAudioData();

        if (_bgmSource.clip != bgmClip)
            _bgmSource.clip = bgmClip;

        _bgmSource.volume = bgmVolume;
        if (!_bgmSource.isPlaying)
            _bgmSource.Play();
    }

    public void StopBgm()
    {
        if (_bgmSource != null && _bgmSource.isPlaying)
            _bgmSource.Stop();
    }

    public void SetBgmVolume(float v)
    {
        bgmVolume = Mathf.Clamp01(v);
        if (_bgmSource != null)
            _bgmSource.volume = bgmVolume;
    }

    /// <summary>
    /// 取第 index 根可视化柱对应的频带能量（0~1 左右）。
    /// barCount 用于把低频偏左、高频偏右地切分。
    /// </summary>
    public float GetBand(int index, int barCount)
    {
        if (barCount <= 0) return 0f;
        EnsureBandBuffer(barCount);
        index = Mathf.Clamp(index, 0, barCount - 1);
        return _smoothedBands[index];
    }

    public void PlayBounce()
    {
        if (bounceClip == null || _bouncePool == null) return;

        if (Time.frameCount != _lastFrameCount)
        {
            _lastFrameCount = Time.frameCount;
            _playsThisFrame = 0;
        }
        if (_playsThisFrame >= maxPlaysPerFrame) return;
        _playsThisFrame++;

        int combo = ComboSystem.Instance != null ? ComboSystem.Instance.CurrentCombo : 0;
        float pitch = Mathf.Min(basePitch + combo * pitchStep, maxPitch);

        AudioSource src = GetPooledSource();
        src.pitch = pitch;
        src.PlayOneShot(bounceClip, volume);
    }

    private void UpdateSpectrum()
    {
        if (_bgmSource == null || !_bgmSource.isPlaying || _bgmSource.clip == null)
        {
            DecayBands();
            _rms = 0f;
            return;
        }

        _bgmSource.GetSpectrumData(_rawSpectrum, 0, spectrumWindow);

        float sumSq = 0f;
        for (int i = 0; i < SpectrumSize; i++)
            sumSq += _rawSpectrum[i] * _rawSpectrum[i];
        _rms = Mathf.Sqrt(sumSq / SpectrumSize);

        if (_bandCountCached <= 0) return;

        float dt = Time.unscaledDeltaTime;
        float lerp = 1f - Mathf.Exp(-spectrumSmooth * dt);

        for (int b = 0; b < _bandCountCached; b++)
        {
            float raw = SampleLogBand(b, _bandCountCached);
            // 压缩动态范围，让灯带更跟手
            float shaped = Mathf.Clamp01(Mathf.Pow(raw * 8f, 0.55f));
            _smoothedBands[b] = Mathf.Lerp(_smoothedBands[b], shaped, lerp);
        }
    }

    private void EnsureBandBuffer(int barCount)
    {
        barCount = Mathf.Clamp(barCount, 1, _smoothedBands.Length);
        if (_bandCountCached == barCount) return;
        _bandCountCached = barCount;
        for (int i = 0; i < _smoothedBands.Length; i++)
            _smoothedBands[i] = 0f;
    }

    private float SampleLogBand(int band, int bandCount)
    {
        // 对数切分：低频占更多 bin，视觉更有「鼓点」
        float t0 = (float)band / bandCount;
        float t1 = (float)(band + 1) / bandCount;
        float minF = 1f;
        float maxF = SpectrumSize - 1;
        int i0 = Mathf.Clamp(Mathf.FloorToInt(minF * Mathf.Pow(maxF / minF, t0)), 0, SpectrumSize - 1);
        int i1 = Mathf.Clamp(Mathf.CeilToInt(minF * Mathf.Pow(maxF / minF, t1)), i0 + 1, SpectrumSize);

        float peak = 0f;
        for (int i = i0; i < i1; i++)
        {
            if (_rawSpectrum[i] > peak)
                peak = _rawSpectrum[i];
        }
        return peak;
    }

    private void DecayBands()
    {
        float dt = Time.unscaledDeltaTime;
        float lerp = 1f - Mathf.Exp(-spectrumSmooth * 0.6f * dt);
        for (int i = 0; i < _smoothedBands.Length; i++)
            _smoothedBands[i] = Mathf.Lerp(_smoothedBands[i], 0f, lerp);
    }

    private AudioSource GetPooledSource()
    {
        for (int i = 0; i < BouncePoolSize; i++)
        {
            int idx = (_poolCursor + i) % BouncePoolSize;
            if (!_bouncePool[idx].isPlaying)
            {
                _poolCursor = (idx + 1) % BouncePoolSize;
                return _bouncePool[idx];
            }
        }

        var steal = _bouncePool[_poolCursor];
        _poolCursor = (_poolCursor + 1) % BouncePoolSize;
        steal.Stop();
        return steal;
    }
}
