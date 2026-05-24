using UnityEngine;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    public bool LeftFlipperPressed  { get; private set; }
    public bool RightFlipperPressed { get; private set; }
    public bool SkillPressed         { get; private set; }
    public bool LaunchPressed        { get; private set; }

    private Camera _cam;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _cam = Camera.main;
    }

    private void Update()
    {
        LeftFlipperPressed  = false;
        RightFlipperPressed = false;
        SkillPressed         = false;
        LaunchPressed        = false;

#if UNITY_EDITOR || UNITY_STANDALONE
        HandleMouseInput();
#else
        HandleTouchInput();
#endif
        HandleKeyboardFallback();
    }

    private void HandleMouseInput()
    {
        bool waiting = BallController.Instance != null && BallController.Instance.IsWaitingForLaunch;
        if (Input.GetMouseButtonDown(0) && waiting)
            LaunchPressed = true;

        // 右键激活斩杀连锁（slot 0）
        if (Input.GetMouseButtonDown(1) && !waiting)
        {
            SkillPressed = true;
            SkillManager.Instance?.TryActivate(0);
        }
    }

    private void HandleTouchInput()
    {
        bool  waiting = BallController.Instance != null && BallController.Instance.IsWaitingForLaunch;
        float botZone = GameManager.Instance?.config?.skillBottomZoneRatio ?? 0.22f;

        foreach (Touch touch in Input.touches)
        {
            if (waiting)
            {
                if (touch.phase == TouchPhase.Began) LaunchPressed = true;
                continue;
            }

            // 技能瞄准中：BulletTimeAim 自行消费触摸
            if (SkillManager.Instance != null && SkillManager.Instance.IsAiming) continue;

            // 底部区域：左半=左挡板，右半=右挡板
            float yRatio = touch.position.y / Screen.height;
            if (yRatio < botZone &&
                touch.phase != TouchPhase.Ended &&
                touch.phase != TouchPhase.Canceled)
            {
                if (touch.position.x / Screen.width < 0.5f) LeftFlipperPressed  = true;
                else                                          RightFlipperPressed = true;
            }
        }
    }

    private void HandleKeyboardFallback()
    {
        if (Input.GetKey(KeyCode.LeftArrow)  || Input.GetKey(KeyCode.Z)) LeftFlipperPressed  = true;
        if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.X)) RightFlipperPressed = true;

        var  gm      = GameManager.Instance;
        bool waiting = BallController.Instance != null && BallController.Instance.IsWaitingForLaunch;
        bool confirm = Input.GetKeyDown(KeyCode.Space) ||
                       Input.GetKeyDown(KeyCode.Return) ||
                       Input.GetKeyDown(KeyCode.KeypadEnter);
        if (confirm && gm != null)
        {
            if (gm.State == GameState.Idle || gm.State == GameState.GameOver)
                gm.StartGame();
            else if (waiting)
                LaunchPressed = true;
        }

        // 键盘备用：E 键激活护盾格挡（slot 1）
        if (Input.GetKeyDown(KeyCode.E))
        {
            SkillPressed = true;
            SkillManager.Instance?.TryActivate(1);
        }
    }
}
