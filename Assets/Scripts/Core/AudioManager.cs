using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("音效设置")]
    public AudioClip bounceClip;
    [Range(0f, 1f)] public float volume = 0.8f;

    [Header("Combo 音高增强（爽感核心）")]
    [Tooltip("基础 Pitch (无 Combo)")]
    public float basePitch = 1.0f;
    [Tooltip("每次 Combo 增加的 Pitch 分量")]
    public float pitchStep = 0.035f;
    [Tooltip("最大 Pitch 限制，防止破音或太刺耳")]
    public float maxPitch = 1.5f;

    [Header("性能/去噪")]
    [Tooltip("同帧内最大允许播放的碰撞音效数，防极速贴撞破音")]
    public int maxPlaysPerFrame = 3;

    private AudioSource _audioSource;
    private int         _playsThisFrame;
    private int         _lastFrameCount;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // 动态创建并配置 AudioSource，无需手动在场景里挂载，即建即用
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();

        _audioSource.playOnAwake = false;
        _audioSource.loop        = false;
        _audioSource.spatialBlend = 0f; // 2D 声音，全场清晰可见
    }

    private void LateUpdate()
    {
        // 跨帧时重置计数器
        _playsThisFrame = 0;
    }

    /// <summary>
    /// 触发反弹音效，支持基于当前 Combo 级的 Pitch 递增，以及同帧防破音拦截
    /// </summary>
    public void PlayBounce()
    {
        if (bounceClip == null) return;

        // 同帧防爆音
        if (Time.frameCount == _lastFrameCount)
        {
            if (_playsThisFrame >= maxPlaysPerFrame) return;
            _playsThisFrame++;
        }
        else
        {
            _lastFrameCount = Time.frameCount;
            _playsThisFrame = 1;
        }

        // 爽感：计算动态 Pitch
        int combo = 0;
        if (ComboSystem.Instance != null)
        {
            combo = ComboSystem.Instance.CurrentCombo;
        }

        // 算得当前 Pitch（线性增加并截断）
        float targetPitch = basePitch + (combo * pitchStep);
        targetPitch = Mathf.Min(targetPitch, maxPitch);

        // 使用临时 AudioSource 播放或动态调整主 AudioSource
        // 因为 PlayOneShot 不能修改单个声轨的 pitch，但为了音轨重叠且音高独立，
        // 我们在极短反弹时，可以通过动态生成轻量级的多音源池或动态调整 pitch 播放
        PlaySoundWithPitch(bounceClip, targetPitch, volume);
    }

    private void PlaySoundWithPitch(AudioClip clip, float pitch, float vol)
    {
        // 创建独立 AudioSource 播一次，支持重叠且 Pitch 相互独立不冲突
        // 预创建一个隐藏的轻量临时 Source，播完自动释放
        GameObject tempGO = new GameObject("TempAudioSource");
        tempGO.transform.SetParent(this.transform);
        AudioSource tempSource = tempGO.AddComponent<AudioSource>();
        tempSource.clip = clip;
        tempSource.pitch = pitch;
        tempSource.volume = vol;
        tempSource.spatialBlend = 0f;
        tempSource.Play();
        
        // 播放完毕后自动销毁
        Destroy(tempGO, clip.length + 0.1f);
    }
}
