using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 一场景预览全部风格 UI：顶栏切换主菜单族 / 老虎机 / 结算 / 暂停。
/// 场景：Assets/Scenes/UIShowcase/UIShowcase.unity
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class UIShowcaseController : MonoBehaviour
{
    public enum PanelId
    {
        MainMenu,
        Campaign,
        Loadout,
        Shop,
        Settings,
        Slot,
        Settlement,
        Pause
    }

    [Header("Hosts")]
    [SerializeField] private GameObject mainMenuHost;
    [SerializeField] private GameObject slotHost;
    [SerializeField] private GameObject settlementHost;
    [SerializeField] private GameObject pauseHost;

    [Header("Services")]
    [SerializeField] private BuffManager buffManager;
    [SerializeField] private List<BuffDefinition> showcaseBuffPool = new List<BuffDefinition>();

    private UIDocument _hubDoc;
    private readonly Dictionary<string, Button> _dockButtons = new Dictionary<string, Button>();
    private PanelId _current = PanelId.MainMenu;

    private void Awake()
    {
        _hubDoc = GetComponent<UIDocument>();
        EnsureBuffPool();
        ForcePreviewMode();
    }

    private void Start()
    {
        BindDock();
        Show(PanelId.MainMenu);
    }

    private void EnsureBuffPool()
    {
        if (buffManager == null)
            buffManager = FindAnyObjectByType<BuffManager>();

        if (buffManager == null)
        {
            var go = new GameObject("BuffManager");
            buffManager = go.AddComponent<BuffManager>();
        }

        if (buffManager.buffPool == null)
            buffManager.buffPool = new List<BuffDefinition>();

        if (buffManager.buffPool.Count == 0 && showcaseBuffPool != null)
        {
            foreach (var b in showcaseBuffPool)
            {
                if (b != null && !buffManager.buffPool.Contains(b))
                    buffManager.buffPool.Add(b);
            }
        }
    }

    private void ForcePreviewMode()
    {
        var menu = mainMenuHost != null
            ? mainMenuHost.GetComponent<MainMenuController>()
            : FindAnyObjectByType<MainMenuController>();
        menu?.SetPreviewOnly(true);
    }

    private void BindDock()
    {
        if (_hubDoc == null || _hubDoc.rootVisualElement == null) return;
        var root = _hubDoc.rootVisualElement;

        Bind("BtnMain", PanelId.MainMenu);
        Bind("BtnCampaign", PanelId.Campaign);
        Bind("BtnLoadout", PanelId.Loadout);
        Bind("BtnShop", PanelId.Shop);
        Bind("BtnSettings", PanelId.Settings);
        Bind("BtnSlot", PanelId.Slot);
        Bind("BtnSettlement", PanelId.Settlement);
        Bind("BtnPause", PanelId.Pause);

        void Bind(string name, PanelId id)
        {
            var btn = root.Q<Button>(name);
            if (btn == null) return;
            _dockButtons[name] = btn;
            btn.RegisterCallback<ClickEvent>(_ => Show(id));
        }
    }

    public void Show(PanelId id)
    {
        _current = id;
        Time.timeScale = 1f;

        HideOverlays();

        bool menuOn = IsMenuFamily(id);
        // 主菜单族互斥；叠加面板 UIDocument 常开，靠各自 Hide/Show，避免 Start 绑定时序问题
        SetDocEnabled(mainMenuHost, menuOn);
        SetDocEnabled(slotHost, true);
        SetDocEnabled(settlementHost, true);
        SetDocEnabled(pauseHost, true);

        switch (id)
        {
            case PanelId.MainMenu:
            case PanelId.Campaign:
            case PanelId.Loadout:
            case PanelId.Shop:
            case PanelId.Settings:
                OpenMenuPanel(id);
                break;
            case PanelId.Slot:
                BuffSelectionController.Instance?.Show();
                break;
            case PanelId.Settlement:
                ShowSettlementPreview();
                break;
            case PanelId.Pause:
                PauseMenuController.Instance?.OpenForShowcase();
                break;
        }

        RefreshDockActive();
    }

    private static bool IsMenuFamily(PanelId id) =>
        id is PanelId.MainMenu or PanelId.Campaign or PanelId.Loadout or PanelId.Shop or PanelId.Settings;

    private void OpenMenuPanel(PanelId id)
    {
        var menu = mainMenuHost != null
            ? mainMenuHost.GetComponent<MainMenuController>()
            : MainMenuController.Instance;
        if (menu == null) return;

        string key = id switch
        {
            PanelId.Campaign => "campaign",
            PanelId.Loadout => "loadout",
            PanelId.Shop => "shop",
            PanelId.Settings => "settings",
            _ => "main"
        };
        menu.ShowcaseOpen(key);
    }

    private void ShowSettlementPreview()
    {
        PlayerProfile.Load();
        var result = new SettlementResult
        {
            Score = 12840,
            Wave = 8,
            CreditsEarned = 1284,
            TotalCredits = PlayerProfile.StoredCredits
        };
        SettlementController.Instance?.Show(result);
    }

    private void HideOverlays()
    {
        BuffSelectionController.Instance?.Hide();
        SettlementController.Instance?.Hide();
        PauseMenuController.Instance?.Close();
        Time.timeScale = 1f;
    }

    private static void SetDocEnabled(GameObject host, bool enabled)
    {
        if (host == null) return;
        if (!host.activeSelf)
            host.SetActive(true);
        var doc = host.GetComponent<UIDocument>();
        if (doc != null)
            doc.enabled = enabled;
    }

    private void RefreshDockActive()
    {
        foreach (var kv in _dockButtons)
            kv.Value.RemoveFromClassList("dock-active");

        string activeName = _current switch
        {
            PanelId.MainMenu => "BtnMain",
            PanelId.Campaign => "BtnCampaign",
            PanelId.Loadout => "BtnLoadout",
            PanelId.Shop => "BtnShop",
            PanelId.Settings => "BtnSettings",
            PanelId.Slot => "BtnSlot",
            PanelId.Settlement => "BtnSettlement",
            PanelId.Pause => "BtnPause",
            _ => "BtnMain"
        };

        if (_dockButtons.TryGetValue(activeName, out var btn))
            btn.AddToClassList("dock-active");
    }
}
