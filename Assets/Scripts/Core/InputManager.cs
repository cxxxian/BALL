using UnityEngine;

[DefaultExecutionOrder(-50)]
public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    public bool LeftFlipperPressed  { get; private set; }
    public bool RightFlipperPressed { get; private set; }
    public bool SkillPressed         { get; private set; }
    public bool LaunchPressed        { get; private set; }
    /// <summary>Boss 导弹弹刀窗：左键 / 触屏按下 / F（非发球等待时）。</summary>
    public bool ParryPressed         { get; private set; }

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
        ParryPressed         = false;

        // 终端说明拍：吞掉全部玩法输入，避免空格/点击误发球或误跳过
        if (TutorialInputGate.BlocksGameplay)
            return;

#if UNITY_EDITOR || UNITY_STANDALONE
        HandleMouseInput();
#else
        HandleTouchInput();
#endif
        HandleKeyboardFallback();
        ApplyTutorialMask();
    }

    private void ApplyTutorialMask()
    {
        if (!TutorialInputGate.Active) return;

        if (!TutorialInputGate.Allows(TutorialInputMask.FlipperLeft))
            LeftFlipperPressed = false;
        if (!TutorialInputGate.Allows(TutorialInputMask.FlipperRight))
            RightFlipperPressed = false;
        if (!TutorialInputGate.Allows(TutorialInputMask.Launch))
            LaunchPressed = false;
        if (!TutorialInputGate.Allows(TutorialInputMask.Parry))
            ParryPressed = false;
        if (!TutorialInputGate.Allows(TutorialInputMask.Skill0)
            && !TutorialInputGate.Allows(TutorialInputMask.Skill1))
            SkillPressed = false;
    }

    private void HandleMouseInput()
    {
        bool waiting = BallController.Instance != null && BallController.Instance.IsWaitingForLaunch;
        // 炮塔瞄准中：左键归 EnergyCannon，不当作弹刀
        if (EnergyCannon.IsPlayerAiming)
            return;
        if (Input.GetMouseButtonDown(0) && waiting)
            LaunchPressed = true;
        else if (Input.GetMouseButtonDown(0) && !waiting)
            ParryPressed = true;

        if (Input.GetMouseButtonDown(1) && !waiting)
        {
            if (!TutorialInputGate.Allows(TutorialInputMask.Skill0)) return;
            SkillPressed = true;
            SkillManager.Instance?.TryActivate(0);
        }
    }

    private void HandleTouchInput()
    {
        if (EnergyCannon.IsPlayerAiming) return;

        bool  waiting = BallController.Instance != null && BallController.Instance.IsWaitingForLaunch;
        float botZone = GameManager.Instance?.config?.skillBottomZoneRatio ?? 0.22f;

        foreach (Touch touch in Input.touches)
        {
            if (waiting)
            {
                if (touch.phase == TouchPhase.Began) LaunchPressed = true;
                continue;
            }

            if (SkillManager.Instance != null
                && (SkillManager.Instance.IsAiming || SkillManager.Instance.IsGroundAiming)) continue;

            float yRatio = touch.position.y / Screen.height;
            if (yRatio < botZone &&
                touch.phase != TouchPhase.Ended &&
                touch.phase != TouchPhase.Canceled)
            {
                if (touch.position.x / Screen.width < 0.5f) LeftFlipperPressed  = true;
                else                                          RightFlipperPressed = true;
            }
            else if (touch.phase == TouchPhase.Began)
            {
                ParryPressed = true;
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

        if (Input.GetKeyDown(KeyCode.Q))
        {
            if (!TutorialInputGate.Allows(TutorialInputMask.Skill0)) return;
            SkillPressed = true;
            SkillManager.Instance?.TryActivate(0);
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (!TutorialInputGate.Allows(TutorialInputMask.Skill1)) return;
            SkillPressed = true;
            SkillManager.Instance?.TryActivate(1);
        }

        if (!waiting && (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.LeftControl)))
            ParryPressed = true;
    }
}
