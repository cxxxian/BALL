using UnityEngine;

public class MainMenuCanvasView : MenuCanvasPanel
{
    [SerializeField] private UnityEngine.UI.Button startButton, loadoutButton, shopButton, tutorialButton, settingsButton, quitButton, editButton, headerSettingsButton;
    [SerializeField] private UnityEngine.UI.Text balance, loadoutSummary, telemetry, tutorialLabel, subtitle, startLabel;
    protected override void Awake()
    {
        base.Awake();
        startButton.onClick.AddListener(() => MainMenuController.Instance?.LaunchEndless());
        loadoutButton.onClick.AddListener(() => MainMenuController.Instance?.OpenLoadout());
        shopButton.onClick.AddListener(() => MainMenuController.Instance?.OpenShop());
        tutorialButton.onClick.AddListener(() => MainMenuController.Instance?.LaunchTutorial());
        settingsButton.onClick.AddListener(() => MainMenuController.Instance?.OpenSettings());
        headerSettingsButton.onClick.AddListener(() => MainMenuController.Instance?.OpenSettings());
        editButton.onClick.AddListener(() => MainMenuController.Instance?.OpenLoadout());
        quitButton.onClick.AddListener(Application.Quit);
    }
    public override void RefreshPresentation()
    {
        PlayerProfile.Load(); RunLoadout.Load();
        var catalog = RunCatalog.Load();
        var ball = RunLoadout.GetSelectedBall(catalog);
        var weapon = RunLoadout.GetSelectedFlipperWeapon();
        balance.text = $"{PlayerProfile.Credits:N0}";
        var first = RunLoadout.GetSkillInSlot(0, catalog); var second = RunLoadout.GetSkillInSlot(1, catalog);
        loadoutSummary.text = $"LOADOUT  {(ball != null ? ball.displayName : "—")}  |  Q:{(first != null ? first.displayName : "—")}  E:{(second != null ? second.displayName : "—")}  |  FLIP:{(weapon != null ? weapon.displayName : "—")}";
        telemetry.text = $"WAVE —  ·  COMBO —  ·  BOSS —  ·  PROTO {Mathf.Max(PlayerProfile.TotalCrateOpens, 1)}";
        tutorialLabel.text = PlayerProfile.HasCompletedTutorial ? "协议校准（重玩）" : "协议校准";
    }
    private void Update()
    {
        if (!Visible) return;
        if (Input.GetKeyDown(KeyCode.Space)) MainMenuController.Instance?.LaunchEndless();
        // Preserve the terminal's original restrained CTA pulse and subtitle colour cycle.
        float time = Time.unscaledTime;
        var c = startLabel.color; c.a = 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(time * 3.2f)); startLabel.color = c;
        float u = 0.5f + 0.5f * Mathf.Sin(time * 2.4f);
        var first = Color.Lerp(new Color(0,0.9f,1), new Color(0.72f,0.95f,1), Mathf.Clamp01(u*1.2f));
        subtitle.color = Color.Lerp(first,new Color(1,0.28f,0.78f),Mathf.Clamp01((u-0.35f)*1.4f));
    }
}
