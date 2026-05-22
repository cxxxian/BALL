using UnityEngine;

public class BulletTimeAim : MonoBehaviour
{
    private Rigidbody2D _ballRb;
    private float       _savedSpeed;
    private Vector2     _aimDir = Vector2.up;
    private bool        _isAiming;
    private Camera      _cam;

    private GameConfig Config => GameManager.Instance?.config;

    private void Awake() { _cam = Camera.main; }

    private void Start()
    {
        if (SkillManager.Instance != null)
        {
            SkillManager.Instance.onActivated.AddListener(OnActivated);
            SkillManager.Instance.onFired.AddListener(OnFired);
        }
        if (GameManager.Instance != null)
            GameManager.Instance.onGameStart.AddListener(OnGameStart);
    }

    private void Update()
    {
        if (!_isAiming) return;
        UpdateAimDir();
        // 实时更新引导线（球在缓慢漂移，起点跟随）
        LaunchGuide.Instance?.UpdateDirection(_ballRb.position, _aimDir);
        CheckFireInput();
    }

    // ── 激活：开启时缓，复用 LaunchGuide 可视化 ─────────────────────────
    private void OnActivated()
    {
        var ball = BallController.Instance;
        if (ball == null) return;
        _ballRb = ball.Rb;
        if (_ballRb == null) return;

        float spd = Mathf.Max(_ballRb.velocity.magnitude, Config?.ballMinSpeed ?? 5f);
        _savedSpeed = spd;

        // 默认方向 = 当前速度方向，若接近零则朝上
        _aimDir = _ballRb.velocity.sqrMagnitude > 0.01f
            ? _ballRb.velocity.normalized : Vector2.up;

        _isAiming = true;
        LaunchGuide.Instance?.Show(_ballRb.position, _aimDir);

        // 时缓 + 视觉特效（委托给 SlowMoFX）
        float scale = Config?.skillSlowMoScale ?? 0.12f;
        SlowMoFX.Instance?.Activate(scale);
    }

    // ── 发射 / 取消（direction==zero 表示取消） ──────────────────────────
    private void OnFired(Vector2 direction)
    {
        _isAiming = false;
        LaunchGuide.Instance?.Hide();
        SlowMoFX.Instance?.Deactivate();  // 平滑恢复时间

        if (_ballRb == null) return;
        _ballRb.velocity = direction.sqrMagnitude > 0.001f
            ? direction * _savedSpeed
            : _ballRb.velocity.normalized * _savedSpeed;
    }

    private void OnGameStart()
    {
        if (!_isAiming) return;
        _isAiming = false;
        LaunchGuide.Instance?.Hide();
        SlowMoFX.Instance?.ForceRestore();
    }

    // ── 每帧根据鼠标/触摸位置更新瞄准方向（无需按下） ───────────────────
    private void UpdateAimDir()
    {
        Vector2? screenPos = GetCursorScreenPos();
        if (screenPos == null) return;

        Vector3 world = _cam.ScreenToWorldPoint(
            new Vector3(screenPos.Value.x, screenPos.Value.y, -_cam.transform.position.z));
        Vector2 delta = (Vector2)world - _ballRb.position;
        if (delta.sqrMagnitude > 0.04f)   // 至少 0.2 世界单位距离才更新方向
            _aimDir = delta.normalized;
    }

    // ── 点击 / 抬手确认发射 ──────────────────────────────────────────────
    private void CheckFireInput()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetMouseButtonDown(0))
            SkillManager.Instance?.Fire(_aimDir);
        if (Input.GetKeyDown(KeyCode.Escape))
            SkillManager.Instance?.Fire(Vector2.zero);
#else
        float botZone = Config?.skillBottomZoneRatio ?? 0.22f;
        foreach (Touch t in Input.touches)
        {
            if (t.position.y / Screen.height <= botZone) continue;
            if (t.phase == TouchPhase.Ended)
            {
                SkillManager.Instance?.Fire(_aimDir);
                return;
            }
        }
#endif
    }

    // ── 获取鼠标/触摸屏幕坐标（始终返回，不需要按下） ───────────────────
    private Vector2? GetCursorScreenPos()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        return Input.mousePosition;
#else
        float botZone = Config?.skillBottomZoneRatio ?? 0.22f;
        foreach (Touch t in Input.touches)
        {
            if (t.position.y / Screen.height > botZone)
                return t.position;
        }
        return null;
#endif
    }
}
