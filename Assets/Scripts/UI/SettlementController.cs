using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// 局末结算 UITK：Score → 协议币展示，再开一局 / 回主菜单。
/// 风格：Notes/UIStyle/CyberpunkNeon_HardPrompt.md
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class SettlementController : MonoBehaviour
{
    public static SettlementController Instance { get; private set; }

    private UIDocument _doc;
    private VisualElement _overlay;
    private Label _waveLabel;
    private Label _scoreLabel;
    private Label _creditsEarnedLabel;
    private Label _creditsTotalLabel;
    private Label _creditsHintLabel;
    private bool _bound;

    public bool IsVisible =>
        _overlay != null && _overlay.style.display == DisplayStyle.Flex;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        _doc = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        if (Instance == null)
            Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        BindUi();
        Hide();
    }

    private void BindUi()
    {
        if (_doc == null)
            _doc = GetComponent<UIDocument>();
        if (_doc == null || _doc.rootVisualElement == null) return;
        var root = _doc.rootVisualElement;

        _overlay = root.Q<VisualElement>("SettlementOverlay");
        _waveLabel = root.Q<Label>("SettleWave");
        _scoreLabel = root.Q<Label>("SettleScore");
        _creditsEarnedLabel = root.Q<Label>("SettleCreditsEarned");
        _creditsTotalLabel = root.Q<Label>("SettleCreditsTotal");
        _creditsHintLabel = root.Q<Label>("SettleCreditsHint");

        if (_bound) return;
        _bound = true;

        root.Q<Button>("BtnSettleRetry")?.RegisterCallback<ClickEvent>(_ => OnRetry());
        root.Q<Button>("BtnSettleMenu")?.RegisterCallback<ClickEvent>(_ => OnMainMenu());
        NeonHoverGlow.AttachAll(root);
    }

    public void Show(SettlementResult result)
    {
        if (_overlay == null)
            BindUi();
        if (_overlay == null) return;

        if (_waveLabel != null)
            _waveLabel.text = result.Wave.ToString("00");
        if (_scoreLabel != null)
            _scoreLabel.text = result.Score.ToString();
        if (_creditsEarnedLabel != null)
            _creditsEarnedLabel.text = $"+{result.CreditsEarned} CR";
        if (_creditsTotalLabel != null)
            _creditsTotalLabel.text = $"{result.TotalCredits} CR";

        if (_creditsHintLabel != null)
        {
            if (RunSession.IsTutorial)
            {
                _creditsHintLabel.text = "> PROTOCOL CALIBRATION COMPLETE · TRY ENDLESS NEXT";
            }
            else
            {
                _creditsHintLabel.text = PlayerProfile.UnlimitedCreditsForTesting
                    ? "> TEST MODE · SHOP DISPLAY UNLIMITED · TOP-RIGHT IS REAL SAVE"
                    : "";
            }
        }

        var retry = _doc?.rootVisualElement?.Q<Button>("BtnSettleRetry");
        if (retry != null)
            retry.text = RunSession.IsTutorial ? "再校准一次" : "再次协议";

        _overlay.style.display = DisplayStyle.Flex;
        Time.timeScale = 0f;
    }

    public void Hide()
    {
        if (_overlay != null)
            _overlay.style.display = DisplayStyle.None;
    }

    private void OnRetry()
    {
        Hide();
        Time.timeScale = 1f;
        if (RunSession.IsTutorial)
        {
            RunSession.BeginTutorial();
            SceneManager.LoadScene("SampleScene");
            return;
        }

        if (HUDController.Instance != null)
            HUDController.Instance.OnRestartButtonClicked();
        else if (GameManager.Instance != null)
            GameManager.Instance.StartGame();
    }

    private void OnMainMenu()
    {
        Hide();
        Time.timeScale = 1f;
        RunSession.Clear();
        SceneManager.LoadScene("MainMenu");
    }
}
