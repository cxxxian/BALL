using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    public static MainMenuController Instance { get; private set; }

    [Header("Menu BGM")]
    [SerializeField] private AudioClip menuBgmClip;
    [SerializeField] [Range(0f, 1f)] private float menuBgmVolume = 0.55f;

    private UIDocument _uiDocument;
    private VisualElement _mainPanel;
    private VisualElement _campaignPanel;
    private VisualElement _loadoutPanel;

    private Label _summaryBallLabel;
    private Label _summarySkillsLabel;
    private AudioSource _menuBgm;

    private void Awake()
    {
        Instance = this;
        _uiDocument = GetComponent<UIDocument>();
        if (_uiDocument == null || _uiDocument.rootVisualElement == null) return;

        var root = _uiDocument.rootVisualElement;

        _mainPanel = root.Q<VisualElement>("MainMenuPanel");
        _campaignPanel = root.Q<VisualElement>("CampaignPanel");
        _loadoutPanel = root.Q<VisualElement>("LoadoutPanel");

        _summaryBallLabel = root.Q<Label>("SummaryBallLabel");
        _summarySkillsLabel = root.Q<Label>("SummarySkillsLabel");

        root.Q<Button>("BtnCampaign")?.RegisterCallback<ClickEvent>(_ => ShowCampaign());
        root.Q<Button>("BtnLoadout")?.RegisterCallback<ClickEvent>(_ => ShowLoadout(LoadoutReturnTarget.MainMenu));
        root.Q<Button>("BtnBack")?.RegisterCallback<ClickEvent>(_ => ShowMainMenu());
        root.Q<Button>("BtnLaunch")?.RegisterCallback<ClickEvent>(_ => TryLaunchCampaign());
        root.Q<Button>("BtnEndless")?.RegisterCallback<ClickEvent>(_ => TryLaunchEndless());
        root.Q<Button>("BtnEditLoadout")?.RegisterCallback<ClickEvent>(_ => ShowLoadout(LoadoutReturnTarget.Campaign));
        root.Q<Button>("BtnQuit")?.RegisterCallback<ClickEvent>(_ => Application.Quit());

        SetupLevelScroll(root.Q<ScrollView>("LevelScrollView"));
        SetupMenuBgm();
    }

    private void Start()
    {
        RunLoadout.Load();
        var catalog = RunCatalog.Load();
        if (catalog != null)
            RunLoadout.EnsureDefaults(catalog);

        var loadout = GetComponent<LoadoutPanelController>();
        if (loadout != null)
            loadout.LoadoutChanged += RefreshCampaignSummary;

        ShowMainMenu();
        RefreshCampaignSummary();
        PlayMenuBgm();
    }

    private void OnDestroy()
    {
        StopMenuBgm();
        if (Instance == this) Instance = null;
        if (LoadoutPanelController.Instance != null)
            LoadoutPanelController.Instance.LoadoutChanged -= RefreshCampaignSummary;
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

    public void OnLoadoutClosed(LoadoutReturnTarget target)
    {
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

    private void ShowMainMenu()
    {
        SetPanelVisible(_mainPanel);
        RefreshCampaignSummary();
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

    private void HideAllPanels()
    {
        if (_mainPanel != null) _mainPanel.style.display = DisplayStyle.None;
        if (_campaignPanel != null) _campaignPanel.style.display = DisplayStyle.None;
        if (_loadoutPanel != null) _loadoutPanel.style.display = DisplayStyle.None;
    }

    private void SetPanelVisible(VisualElement panel)
    {
        HideAllPanels();
        if (LoadoutPanelController.Instance != null && _loadoutPanel != null)
            _loadoutPanel.style.display = DisplayStyle.None;
        if (panel != null)
            panel.style.display = DisplayStyle.Flex;
    }

    private void RefreshCampaignSummary()
    {
        LoadoutPanelController.Instance?.RefreshSummaryLabels(_summaryBallLabel, _summarySkillsLabel);
    }

    private void TryLaunchEndless()
    {
        if (!PrepareRunOrShowLoadout(LoadoutReturnTarget.MainMenu)) return;
        SceneManager.LoadScene("SampleScene");
    }

    private void TryLaunchCampaign()
    {
        if (!PrepareRunOrShowLoadout(LoadoutReturnTarget.Campaign)) return;
        SceneManager.LoadScene("SampleScene");
    }

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
