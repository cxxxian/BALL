using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 协议锁 / 过载状态的轻量 HUD。无场景引用时会在 Canvas 下自建文本。
/// </summary>
public class ProtocolLockHud : MonoBehaviour
{
    public static ProtocolLockHud Instance { get; private set; }

    [SerializeField] private Text lockText;
    [SerializeField] private Vector2 anchorPos = new Vector2(24f, -120f);

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        EnsureLabel();
        var director = ProtocolFieldDirector.EnsureExists();
        director.onLocksChanged.AddListener(RefreshLocks);
        director.onOverloadStarted.AddListener(() => SetBanner("PROTOCOL OVERLOAD", new Color(2f, 1.4f, 0.2f)));
        director.onOverloadEnded.AddListener(() => RefreshLocks(director.CurrentLocks));
        director.onRewardGranted.AddListener((type, _) =>
        {
            if (type == ProtocolRewardType.ProtocolLock) return;
            FlashReward(type);
        });
        RefreshLocks(director.CurrentLocks);

        if (GameManager.Instance != null)
            GameManager.Instance.onGameStart.AddListener(() => RefreshLocks(0));
    }

    private void EnsureLabel()
    {
        if (lockText != null) return;

        var canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        var go = new GameObject("ProtocolLockText");
        go.transform.SetParent(canvas.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = anchorPos;
        rt.sizeDelta = new Vector2(420f, 40f);

        lockText = go.AddComponent<Text>();
        lockText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (lockText.font == null)
            lockText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        lockText.fontSize = 22;
        lockText.alignment = TextAnchor.UpperLeft;
        lockText.color = new Color(0.55f, 1.8f, 2.2f, 1f);
        lockText.raycastTarget = false;
    }

    private void RefreshLocks(int locks)
    {
        if (lockText == null) return;
        int need = 3;
        if (ProtocolFieldDirector.Instance != null && ProtocolFieldDirector.Instance.IsOverloading)
        {
            SetBanner("PROTOCOL OVERLOAD", new Color(2f, 1.4f, 0.2f));
            return;
        }
        lockText.color = new Color(0.55f, 1.8f, 2.2f, 1f);
        lockText.text = $"LOCK  {locks}/{need}";
    }

    private void SetBanner(string msg, Color c)
    {
        if (lockText == null) return;
        lockText.color = c;
        lockText.text = msg;
    }

    private void FlashReward(ProtocolRewardType type)
    {
        if (lockText == null) return;
        string label = type switch
        {
            ProtocolRewardType.ScoreBurst => "CACHE · SCORE",
            ProtocolRewardType.SkillCooldown => "CACHE · SKILL CD",
            ProtocolRewardType.BumperPulse => "CACHE · PULSE",
            ProtocolRewardType.ComboBoost => "CACHE · COMBO",
            ProtocolRewardType.Heal => "CACHE · HEAL",
            ProtocolRewardType.DamageBuff => "CACHE · DAMAGE",
            _ => "CACHE · REWARD"
        };
        SetBanner(label, new Color(1.6f, 2f, 0.4f));
        CancelInvoke(nameof(RestoreLocks));
        Invoke(nameof(RestoreLocks), 1.1f);
    }

    private void RestoreLocks()
    {
        if (ProtocolFieldDirector.Instance != null)
            RefreshLocks(ProtocolFieldDirector.Instance.CurrentLocks);
    }
}
