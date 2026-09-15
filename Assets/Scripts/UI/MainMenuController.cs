using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    public static MainMenuController Instance { get; private set; }

    [Header("Menu BGM")]
    [SerializeField] private AudioClip menuBgmClip;
    [SerializeField] [Range(0f, 1f)] private float menuBgmVolume = 0.55f;

    [Header("Showcase / Preview")]
    [Tooltip("为 true 时禁止跳转 SampleScene，供 UIShowcase 预览用。")]
    [SerializeField] private bool previewOnly;

    private UIDocument _uiDocument;
    private VisualElement _mainPanel;
    private VisualElement _campaignPanel;
    private VisualElement _loadoutPanel;
    private VisualElement _shopPanel;
    private VisualElement _settingsPanel;
    private Label _mainCreditsLabel;
    private Label _homeBallName;
    private Label _homeBallDesc;
    private Label _homeSkill0Name;
    private Label _homeSkill1Name;
    private Label _homeStatWave;
    private Label _homeStatCombo;
    private Label _homeStatBosses;
    private Label _homeStatProtocols;
    private Label _homeLoadoutLine;
    private Label _homeTelemetryLine;
    private Label _termSubtitle;
    private Label _termCtaLabel;

    private Label _summaryBallLabel;
    private Label _summarySkillsLabel;
    private AudioSource _menuBgm;
    private IVisualElementScheduledItem _termFxSchedule;

    private void Awake()
    {
        Instance = this;
        EnsureMatrixBackground();
        _uiDocument = GetComponent<UIDocument>();
        if (_uiDocument == null || _uiDocument.rootVisualElement == null) return;

        var root = _uiDocument.rootVisualElement;

        _mainPanel = root.Q<VisualElement>("MainMenuPanel");
        _campaignPanel = root.Q<VisualElement>("CampaignPanel");
        _loadoutPanel = root.Q<VisualElement>("LoadoutPanel");
        _shopPanel = root.Q<VisualElement>("ShopPanel");
        _settingsPanel = root.Q<VisualElement>("SettingsPanel");
        _mainCreditsLabel = root.Q<Label>("MainCreditsLabel");
        _homeBallName = root.Q<Label>("HomeBallName");
        _homeBallDesc = root.Q<Label>("HomeBallDesc");
        _homeSkill0Name = root.Q<Label>("HomeSkill0Name");
        _homeSkill1Name = root.Q<Label>("HomeSkill1Name");
        _homeStatWave = root.Q<Label>("HomeStatWave");
        _homeStatCombo = root.Q<Label>("HomeStatCombo");
        _homeStatBosses = root.Q<Label>("HomeStatBosses");
        _homeStatProtocols = root.Q<Label>("HomeStatProtocols");
        _homeLoadoutLine = root.Q<Label>("HomeLoadoutLine");
        _homeTelemetryLine = root.Q<Label>("HomeTelemetryLine");
        _termSubtitle = root.Q<Label>("TermSubtitle");
        _termCtaLabel = root.Q<Label>("TermCtaLabel");

        _summaryBallLabel = root.Q<Label>("SummaryBallLabel");
        _summarySkillsLabel = root.Q<Label>("SummarySkillsLabel");

        root.Q<Button>("BtnCampaign")?.RegisterCallback<ClickEvent>(_ => ShowCampaign());
        root.Q<Button>("BtnLoadout")?.RegisterCallback<ClickEvent>(_ => ShowLoadout(LoadoutReturnTarget.MainMenu));
        root.Q<Button>("BtnHomeEditLoadout")?.RegisterCallback<ClickEvent>(_ => ShowLoadout(LoadoutReturnTarget.MainMenu));
        root.Q<Button>("BtnShop")?.RegisterCallback<ClickEvent>(_ => ShowShop(ShopReturnTarget.MainMenu));
        root.Q<Button>("BtnSettings")?.RegisterCallback<ClickEvent>(_ => ShowSettings());
        root.Q<Button>("BtnFooterSettings")?.RegisterCallback<ClickEvent>(_ => ShowSettings());
        root.Q<Button>("BtnSettingsBack")?.RegisterCallback<ClickEvent>(_ => ShowMainMenu());
        root.Q<Button>("BtnBack")?.RegisterCallback<ClickEvent>(_ => ShowMainMenu());
        root.Q<Button>("BtnLaunch")?.RegisterCallback<ClickEvent>(_ => TryLaunchCampaign());
        root.Q<Button>("BtnEndless")?.RegisterCallback<ClickEvent>(_ => TryLaunchEndless());
        root.Q<Button>("BtnTutorial")?.RegisterCallback<ClickEvent>(_ => TryLaunchTutorial());
        root.Q<Button>("BtnEditLoadout")?.RegisterCallback<ClickEvent>(_ => ShowLoadout(LoadoutReturnTarget.Campaign));
        root.Q<Button>("BtnQuit")?.RegisterCallback<ClickEvent>(_ => Application.Quit());

        NeonHoverGlow.AttachAll(root);
        SetupSettingsStub(root);
        SetupTerminalFx(root);
        SetupLevelScroll(root.Q<ScrollView>("LevelScrollView"));
        SetupMenuBgm();
    }

    private void Start()
    {
        RunSession.Clear();
        PlayerProfile.Load();
        RunLoadout.Load();
        var catalog = RunCatalog.Load();
        if (catalog != null)
            RunLoadout.EnsureDefaults(catalog);

        var loadout = GetComponent<LoadoutPanelController>();
        if (loadout != null)
            loadout.LoadoutChanged += OnLoadoutChanged;

        var shop = GetComponent<ShopPanelController>();
        if (shop != null)
            shop.ShopChanged += OnShopChanged;

        ShowMainMenu();
        PlayMenuBgm();
    }

    private void OnDestroy()
    {
        StopMenuBgm();
        if (Instance == this) Instance = null;
        if (LoadoutPanelController.Instance != null)
            LoadoutPanelController.Instance.LoadoutChanged -= OnLoadoutChanged;
        var shop = GetComponent<ShopPanelController>();
        if (shop != null)
            shop.ShopChanged -= OnShopChanged;
    }

    private void OnLoadoutChanged()
    {
        RefreshCampaignSummary();
        RefreshHomeLoadoutCard();
    }

    private void OnShopChanged()
    {
        RefreshCredits();
        RefreshCampaignSummary();
        RefreshHomeLoadoutCard();
        RefreshHomeTelemetry();
    }

    public void RefreshCredits()
    {
        PlayerProfile.Load();
        if (_mainCreditsLabel != null)
            _mainCreditsLabel.text = $"{PlayerProfile.Credits:N0}";
        ShopPanelController.Instance?.RefreshCredits();
    }

    public void OnLoadoutClosed(LoadoutReturnTarget target)
    {
        RefreshCredits();
        switch (target)
        {
            case LoadoutReturnTarget.Campaign:
                ShowCampaign();
                break;
            default:
                ShowMainMenu();
                break;
        }
    }

    public void OnShopClosed(ShopReturnTarget target)
    {
        RefreshCredits();
        switch (target)
        {
            case ShopReturnTarget.Campaign:
                ShowCampaign();
                break;
            default:
                ShowMainMenu();
                break;
        }
    }

    private void ShowMainMenu()
    {
        SetPanelVisible(_mainPanel);
        RefreshCampaignSummary();
        RefreshHomeLoadoutCard();
        RefreshHomeTelemetry();
        RefreshCredits();
        RefreshTutorialButton();
    }

    private void RefreshTutorialButton()
    {
        var btn = _uiDocument?.rootVisualElement?.Q<Button>("BtnTutorial");
        if (btn == null) return;
        PlayerProfile.Load();
        btn.text = PlayerProfile.HasCompletedTutorial ? "协议校准（重玩）" : "协议校准";
    }

    private void RefreshHomeLoadoutCard()
    {
        var catalog = RunCatalog.Load();
        RunLoadout.Load();
        if (catalog != null)
            RunLoadout.EnsureDefaults(catalog);

        var ball = RunLoadout.GetSelectedBall(catalog);
        if (_homeBallName != null)
            _homeBallName.text = ball != null ? ball.displayName : "—";
        if (_homeBallDesc != null)
            _homeBallDesc.text = ball != null && !string.IsNullOrEmpty(ball.loadoutDescription)
                ? ball.loadoutDescription
                : "均衡核心 · 适合全模式";

        var s0 = RunLoadout.GetSkillInSlot(0, catalog);
        var s1 = RunLoadout.GetSkillInSlot(1, catalog);
        if (_homeSkill0Name != null)
            _homeSkill0Name.text = s0 != null ? s0.displayName : "—";
        if (_homeSkill1Name != null)
            _homeSkill1Name.text = s1 != null ? s1.displayName : "—";

        if (_homeLoadoutLine != null)
        {
            string ballName = ball != null ? ball.displayName : "—";
            string q = s0 != null ? s0.displayName : "—";
            string e = s1 != null ? s1.displayName : "—";
            _homeLoadoutLine.text = $"LOADOUT  {ballName}  |  Q:{q}  E:{e}";
        }
    }

    private void RefreshHomeTelemetry()
    {
        PlayerProfile.Load();
        // 正式遥测存档尚未接入：用可拿到的 meta + 稳定展示占位，避免空白
        if (_homeStatProtocols != null)
            _homeStatProtocols.text = Mathf.Max(PlayerProfile.TotalCrateOpens, 1).ToString();
        if (_homeStatWave != null && string.IsNullOrEmpty(_homeStatWave.text))
            _homeStatWave.text = "—";
        if (_homeStatCombo != null && string.IsNullOrEmpty(_homeStatCombo.text))
            _homeStatCombo.text = "—";
        if (_homeStatBosses != null && string.IsNullOrEmpty(_homeStatBosses.text))
            _homeStatBosses.text = "—";

        if (_homeTelemetryLine != null)
        {
            string wave = _homeStatWave != null ? _homeStatWave.text : "—";
            string combo = _homeStatCombo != null ? _homeStatCombo.text : "—";
            string bosses = _homeStatBosses != null ? _homeStatBosses.text : "—";
            string proto = _homeStatProtocols != null ? _homeStatProtocols.text : "1";
            _homeTelemetryLine.text = $"WAVE {wave}  ·  COMBO {combo}  ·  BOSS {bosses}  ·  PROTO {proto}";
        }
    }

    private void SetupTerminalFx(VisualElement root)
    {
        if (_termSubtitle == null && _termCtaLabel == null) return;
        _termFxSchedule?.Pause();
        _termFxSchedule = root.schedule.Execute(TickTerminalFx).Every(33);
    }

    private void TickTerminalFx()
    {
        float t = Time.unscaledTime;
        if (_termCtaLabel != null)
        {
            // 柔脉冲，而非街机硬闪
            float pulse = 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(t * 3.2f));
            _termCtaLabel.style.opacity = pulse;
        }

        if (_termSubtitle != null)
        {
            // 青白 ↔ 品红：赛博层次，避免纯彩虹街机感
            float u = 0.5f + 0.5f * Mathf.Sin(t * 2.4f);
            var ice = new Color(0.72f, 0.95f, 1f, 1f);
            var cyan = new Color(0f, 0.9f, 1f, 1f);
            var mag = new Color(1f, 0.28f, 0.78f, 1f);
            Color a = Color.Lerp(cyan, ice, Mathf.Clamp01(u * 1.2f));
            Color b = Color.Lerp(a, mag, Mathf.Clamp01((u - 0.35f) * 1.4f));
            _termSubtitle.style.color = b;
        }
    }

    private void ShowCampaign()
    {
        SetPanelVisible(_campaignPanel);
        RefreshCampaignSummary();
    }

    private void ShowLoadout(LoadoutReturnTarget returnTarget)
    {
        HideAllPanels();
        LoadoutPanelController.Instance?.Show(returnTarget);
    }

    private void ShowShop(ShopReturnTarget returnTarget)
    {
        HideAllPanels();
        ShopPanelController.Instance?.Show(returnTarget);
    }

    private void ShowSettings()
    {
        SetPanelVisible(_settingsPanel);
    }

    private void HideAllPanels()
    {
        if (_mainPanel != null) _mainPanel.style.display = DisplayStyle.None;
        if (_campaignPanel != null) _campaignPanel.style.display = DisplayStyle.None;
        if (_loadoutPanel != null) _loadoutPanel.style.display = DisplayStyle.None;
        if (_shopPanel != null) _shopPanel.style.display = DisplayStyle.None;
        if (_settingsPanel != null) _settingsPanel.style.display = DisplayStyle.None;
    }

    private void SetPanelVisible(VisualElement panel)
    {
        HideAllPanels();
        if (panel != null)
            panel.style.display = DisplayStyle.Flex;
    }

    private void RefreshCampaignSummary()
    {
        LoadoutPanelController.Instance?.RefreshSummaryLabels(_summaryBallLabel, _summarySkillsLabel);
    }

    private void TryLaunchEndless()
    {
        if (previewOnly)
        {
            Debug.Log("[MainMenu] Preview mode — endless launch suppressed.");
            return;
        }

        if (!PrepareRunOrShowLoadout(LoadoutReturnTarget.MainMenu)) return;
        SceneManager.LoadScene("SampleScene");
    }

    private void TryLaunchCampaign()
    {
        if (previewOnly)
        {
            Debug.Log("[MainMenu] Preview mode — campaign launch suppressed.");
            return;
        }

        if (!PrepareRunOrShowLoadout(LoadoutReturnTarget.Campaign)) return;
        SceneManager.LoadScene("SampleScene");
    }

    private void TryLaunchTutorial()
    {
        if (previewOnly)
        {
            Debug.Log("[MainMenu] Preview mode — tutorial launch suppressed.");
            return;
        }

        RunSession.BeginTutorial();
        var catalog = RunCatalog.Load();
        if (catalog == null)
        {
            Debug.LogError("[MainMenu] RunCatalog missing.");
            RunSession.Clear();
            return;
        }

        RunLoadout.EnsureDefaults(catalog);
        SceneManager.LoadScene("SampleScene");
    }

    /// <summary>UIShowcase 切换子面板用。</summary>
    public void ShowcaseOpen(string panelId)
    {
        switch ((panelId ?? "").ToLowerInvariant())
        {
            case "campaign": ShowCampaign(); break;
            case "loadout": ShowLoadout(LoadoutReturnTarget.MainMenu); break;
            case "shop": ShowShop(ShopReturnTarget.MainMenu); break;
            case "settings": ShowSettings(); break;
            default: ShowMainMenu(); break;
        }
    }

    public void SetPreviewOnly(bool enabled) => previewOnly = enabled;

    private bool PrepareRunOrShowLoadout(LoadoutReturnTarget loadoutReturn)
    {
        RunLoadout.Load();
        var catalog = RunCatalog.Load();
        if (catalog == null)
        {
            Debug.LogError("[MainMenu] RunCatalog missing.");
            return false;
        }

        RunLoadout.EnsureDefaults(catalog);
        if (RunLoadout.IsValid(catalog))
            return true;

        ShowLoadout(loadoutReturn);
        return false;
    }

    private static void SetupSettingsStub(VisualElement root)
    {
        root.Q<Slider>("SliderMaster")?.SetEnabled(false);
        root.Q<Slider>("SliderSfx")?.SetEnabled(false);
        root.Q<Slider>("SliderMusic")?.SetEnabled(false);

        var hintPc = root.Q<Label>("SettingsHintPc");
        if (hintPc != null)
            hintPc.text = "PC：Esc 暂停 · 右键斩击瞄准（左键确认）· Q/E 绑定技能";

        var hintMobile = root.Q<Label>("SettingsHintMobile");
        if (hintMobile != null)
            hintMobile.text = "移动端：右上角暂停 · 技能按钮松手确认瞄准";
    }

    private void EnsureMatrixBackground()
    {
        if (FindAnyObjectByType<MainMenuMatrixBackground>() != null)
            return;

        var go = new GameObject("MenuMatrixBackground");
        go.AddComponent<MainMenuMatrixBackground>();
    }

    private void SetupMenuBgm()
    {
        _menuBgm = gameObject.GetComponent<AudioSource>();
        if (_menuBgm == null)
            _menuBgm = gameObject.AddComponent<AudioSource>();

        _menuBgm.playOnAwake = false;
        _menuBgm.loop = true;
        _menuBgm.spatialBlend = 0f;
        _menuBgm.priority = 32;
        _menuBgm.volume = menuBgmVolume;
    }

    private void PlayMenuBgm()
    {
        if (_menuBgm == null) SetupMenuBgm();
        if (menuBgmClip == null || _menuBgm == null) return;

        if (menuBgmClip.loadState != AudioDataLoadState.Loaded)
            menuBgmClip.LoadAudioData();

        if (_menuBgm.clip != menuBgmClip)
            _menuBgm.clip = menuBgmClip;

        _menuBgm.volume = menuBgmVolume;
        if (!_menuBgm.isPlaying)
            _menuBgm.Play();
    }

    private void StopMenuBgm()
    {
        if (_menuBgm != null && _menuBgm.isPlaying)
            _menuBgm.Stop();
    }

    private void SetupLevelScroll(ScrollView scroll)
    {
        if (scroll == null) return;

        scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;

        bool isDragging = false;
        Vector2 startPos = Vector2.zero;
        Vector2 startOffset = Vector2.zero;

        scroll.RegisterCallback<PointerDownEvent>(e =>
        {
            isDragging = true;
            startPos = e.position;
            startOffset = scroll.scrollOffset;
            scroll.CapturePointer(e.pointerId);
        });

        scroll.RegisterCallback<PointerMoveEvent>(e =>
        {
            if (!isDragging) return;
            Vector2 delta = (Vector2)e.position - startPos;
            scroll.scrollOffset = new Vector2(startOffset.x - delta.x, startOffset.y);
        });

        scroll.RegisterCallback<PointerUpEvent>(e =>
        {
            if (!isDragging) return;
            isDragging = false;
            scroll.ReleasePointer(e.pointerId);
        });

        scroll.RegisterCallback<PointerCaptureOutEvent>(_ => isDragging = false);

        scroll.Clear();
        for (int i = 1; i <= 3; i++)
        {
            var card = new VisualElement();
            card.AddToClassList("level-card");

            var deco = new VisualElement();
            deco.AddToClassList("card-deco");
            card.Add(deco);

            var num = new Label($"STAGE 0{i}");
            num.AddToClassList("card-num");
            card.Add(num);

            var name = new Label(i == 1 ? "石巨像之怒" : (i == 2 ? "电磁核心之灾" : "未解锁星区"));
            name.AddToClassList("card-name");
            card.Add(name);

            var stars = new Label(i == 1 ? "★★★" : "[ 锁定 ]");
            stars.AddToClassList("card-stars");
            card.Add(stars);

            scroll.Add(card);
        }
    }
}
