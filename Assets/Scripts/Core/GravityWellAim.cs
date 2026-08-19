using UnityEngine;

public class GravityWellAim : MonoBehaviour
{
    public static GravityWellAim Instance { get; private set; }

    private bool     _isAiming;
    private Camera   _cam;
    private Vector2  _aimPos;
    private bool     _placementValid;
    private LineRenderer _previewRing;

    private GameConfig Config => GameManager.Instance?.config;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _cam = Camera.main;
        BuildPreviewRing();
    }

    private void Start()
    {
        GravityWellAim.EnsureInstance();

        if (SkillManager.Instance != null)
        {
            SkillManager.Instance.onGravityWellAimStarted.AddListener(OnAimStarted);
            SkillManager.Instance.onGroundAimAborted.AddListener(OnAimAborted);
            SkillManager.Instance.onAimingEnded.AddListener(OnAimingEnded);
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.onGameStart.AddListener(OnInterrupt);
            GameManager.Instance.onBallLost.AddListener(OnInterrupt);
            GameManager.Instance.onGameOver.AddListener(OnInterrupt);
        }
    }

    private void OnDestroy()
    {
        if (SkillManager.Instance != null)
        {
            SkillManager.Instance.onGravityWellAimStarted.RemoveListener(OnAimStarted);
            SkillManager.Instance.onGroundAimAborted.RemoveListener(OnAimAborted);
            SkillManager.Instance.onAimingEnded.RemoveListener(OnAimingEnded);
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.onGameStart.RemoveListener(OnInterrupt);
            GameManager.Instance.onBallLost.RemoveListener(OnInterrupt);
            GameManager.Instance.onGameOver.RemoveListener(OnInterrupt);
        }

        if (Instance == this) Instance = null;
    }

    public static GravityWellAim EnsureInstance()
    {
        if (Instance != null) return Instance;
        var go = new GameObject(nameof(GravityWellAim));
        return go.AddComponent<GravityWellAim>();
    }

    public static bool IsValidPlacement(Vector2 worldPos)
    {
        var cfg = GameManager.Instance?.config;
        float minY = cfg != null
            ? cfg.minionAttackLineY + cfg.gravityWellMinPlaceOffset
            : -3.2f;
        if (worldPos.y <= minY) return false;

        float halfW  = cfg != null ? cfg.worldWidth * 0.5f : 4.5f;
        float margin = cfg != null ? cfg.gravityWellPlaceMarginX : 0.5f;
        float maxX   = halfW - margin;
        return worldPos.x >= -maxX && worldPos.x <= maxX;
    }

    private void Update()
    {
        if (!_isAiming) return;

        UpdateAimPos();
        UpdatePreview();
        CheckInput();
    }

    private void OnAimStarted()
    {
        _isAiming = true;
        UpdateAimPos();
        ShowPreview();
    }

    private void OnAimAborted() => EndAimState();

    private void OnAimingEnded(int _) => EndAimState();

    private void OnInterrupt() => EndAimState();

    private void EndAimState()
    {
        _isAiming = false;
        HidePreview();
    }

    private void UpdateAimPos()
    {
        Vector2? screenPos = GetCursorScreenPos();
        if (screenPos == null) return;

        Vector3 world = _cam.ScreenToWorldPoint(
            new Vector3(screenPos.Value.x, screenPos.Value.y, -_cam.transform.position.z));
        _aimPos = world;
        _placementValid = IsValidPlacement(_aimPos);
    }

    private void CheckInput()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetMouseButtonDown(0) && _placementValid)
        {
            SkillManager.Instance?.ConfirmGravityWell(_aimPos);
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            SkillManager.Instance?.CancelGroundAim();
#else
        float botZone = Config?.skillBottomZoneRatio ?? 0.22f;
        foreach (Touch t in Input.touches)
        {
            if (t.position.y / Screen.height <= botZone) continue;
            if (t.phase == TouchPhase.Ended)
            {
                if (_placementValid)
                    SkillManager.Instance?.ConfirmGravityWell(_aimPos);
                return;
            }
        }
#endif
    }

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

    private void BuildPreviewRing()
    {
        var ringGo = new GameObject("PreviewRing");
        ringGo.transform.SetParent(transform, false);

        _previewRing = ringGo.AddComponent<LineRenderer>();
        _previewRing.useWorldSpace = false;
        _previewRing.loop = true;
        const int sides = 48;
        _previewRing.positionCount = sides + 1;
        _previewRing.sortingOrder = 9;
        _previewRing.startWidth = 0.07f;
        _previewRing.endWidth = 0.07f;
        _previewRing.material = CyberVisualFactory.UnlitMaterial;

        for (int i = 0; i <= sides; i++)
        {
            float a = (float)i / sides * Mathf.PI * 2f;
            _previewRing.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f));
        }

        _previewRing.enabled = false;
    }

    private void ShowPreview()
    {
        if (_previewRing == null) return;
        float radius = Config != null ? Config.gravityWellRadius : 2.2f;
        _previewRing.transform.localScale = Vector3.one * radius;
        _previewRing.enabled = true;
        UpdatePreview();
    }

    private void HidePreview()
    {
        if (_previewRing != null) _previewRing.enabled = false;
    }

    private void UpdatePreview()
    {
        if (_previewRing == null || !_isAiming) return;

        transform.position = _aimPos;
        var validColor   = new Color(0.35f, 1f, 0.55f, 0.75f);
        var invalidColor = new Color(1f, 0.25f, 0.25f, 0.75f);
        var c = _placementValid ? validColor : invalidColor;
        _previewRing.startColor = c;
        _previewRing.endColor = c;
    }
}
