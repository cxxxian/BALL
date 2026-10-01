using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>Routes menu screens and launches runs. No runtime-generated menu layout.</summary>
public class MainMenuController : MonoBehaviour
{
    public static MainMenuController Instance { get; private set; }
    [Header("Editable uGUI")]
    [SerializeField] private MainMenuCanvasView canvasView;
    [Header("Menu BGM")]
    [SerializeField] private AudioClip menuBgmClip;
    [SerializeField, Range(0, 1)] private float menuBgmVolume = 0.55f;
    [Header("Showcase / Preview")]
    [SerializeField] private bool previewOnly;
    private UIDocument document;
    private VisualElement settingsPanel, shopPanel;
    private AudioSource menuBgm;
    private LoadoutPanelController loadout;
    private ShopPanelController shop;
    private void Awake()
    {
        Instance = this;
        if (FindObjectOfType<MainMenuMatrixBackground>() == null)
            new GameObject("MenuMatrixBackground").AddComponent<MainMenuMatrixBackground>();
        document = GetComponent<UIDocument>();
        var root = document != null ? document.rootVisualElement : null;
        if (root != null)
        {
            settingsPanel = root.Q<VisualElement>("SettingsPanel");
            shopPanel = root.Q<VisualElement>("ShopPanel");
            root.Q<Button>("BtnSettingsBack")?.RegisterCallback<ClickEvent>(_ => ShowMainMenu());
            root.Q<Slider>("SliderMaster")?.SetEnabled(false);
            root.Q<Slider>("SliderSfx")?.SetEnabled(false);
            root.Q<Slider>("SliderMusic")?.SetEnabled(false);
            if (settingsPanel != null) settingsPanel.style.display = DisplayStyle.None;
            if (shopPanel != null) shopPanel.style.display = DisplayStyle.None;
        }
        SetupMenuBgm();
    }
    private void Start()
    {
        RunSession.Clear(); PlayerProfile.Load(); RunLoadout.Load();
        var catalog = RunCatalog.Load();
        if (catalog != null) RunLoadout.EnsureDefaults(catalog);
        loadout = GetComponent<LoadoutPanelController>(); shop = GetComponent<ShopPanelController>();
        if (loadout != null) loadout.LoadoutChanged += RefreshMainPresentation;
        if (shop != null) shop.ShopChanged += RefreshMainPresentation;
        ShowMainMenu();
        if (menuBgmClip != null) { menuBgm.clip = menuBgmClip; menuBgm.Play(); }
    }
    public void HideMenuPresentation()
    {
        canvasView?.Hide();
        LoadoutPanelController.Instance?.HideForNavigation();
        ShopPanelController.Instance?.HideCanvasForNavigation();
        UiPanelMotion.Kill(settingsPanel); UiPanelMotion.Kill(shopPanel);
        if (settingsPanel != null) settingsPanel.style.display = DisplayStyle.None;
        if (shopPanel != null) shopPanel.style.display = DisplayStyle.None;
    }
    public void ShowMainMenu()
    {
        HideMenuPresentation(); canvasView?.Show();
    }
    public void OpenLoadout()
    {
        HideMenuPresentation(); LoadoutPanelController.Instance?.Show(LoadoutReturnTarget.MainMenu);
    }
    public void OpenShop()
    {
        HideMenuPresentation(); ShopPanelController.Instance?.Show(ShopReturnTarget.MainMenu);
    }
    public void OpenSettings()
    {
        HideMenuPresentation();
        if (settingsPanel != null) UiPanelMotion.Show(settingsPanel, new Vector2(0,16), 0.2f);
    }
    public void OnLoadoutClosed(LoadoutReturnTarget target) => ShowMainMenu();
    public void OnShopClosed(ShopReturnTarget target) => ShowMainMenu();
    public void RefreshCredits()
    {
        RefreshMainPresentation(); ShopPanelController.Instance?.RefreshCredits();
    }
    private void RefreshMainPresentation()
    {
        if (canvasView != null && canvasView.Visible) canvasView.RefreshPresentation();
    }
    public void LaunchEndless()
    {
        if (previewOnly) { Debug.Log("[MainMenu] Preview mode: launch suppressed."); return; }
        RunLoadout.Load(); var catalog = RunCatalog.Load();
        if (catalog == null) { Debug.LogError("[MainMenu] RunCatalog missing."); return; }
        RunLoadout.EnsureDefaults(catalog);
        if (!RunLoadout.IsValid(catalog)) { OpenLoadout(); return; }
        RunSession.BeginEndless(); SceneManager.LoadScene("SampleScene");
    }
    public void LaunchTutorial()
    {
        if (previewOnly) { Debug.Log("[MainMenu] Preview mode: tutorial launch suppressed."); return; }
        if (RunCatalog.Load() == null) { Debug.LogError("[MainMenu] RunCatalog missing."); return; }
        RunSession.BeginTutorial(); SceneManager.LoadScene("SampleScene");
    }
    public void ShowcaseOpen(string id)
    {
        switch ((id ?? "").ToLowerInvariant())
        {
            case "loadout": OpenLoadout(); break;
            case "shop": OpenShop(); break;
            case "settings": OpenSettings(); break;
            default: ShowMainMenu(); break;
        }
    }
    public void SetPreviewOnly(bool enabled) => previewOnly = enabled;
    private void SetupMenuBgm()
    {
        menuBgm = GetComponent<AudioSource>();
        if (menuBgm == null) menuBgm = gameObject.AddComponent<AudioSource>();
        menuBgm.playOnAwake = false; menuBgm.loop = true; menuBgm.spatialBlend = 0;
        menuBgm.priority = 32; menuBgm.volume = menuBgmVolume;
    }
    private void OnDestroy()
    {
        if (menuBgm != null) menuBgm.Stop();
        if (loadout != null) loadout.LoadoutChanged -= RefreshMainPresentation;
        if (shop != null) shop.ShopChanged -= RefreshMainPresentation;
        UiPanelMotion.Kill(settingsPanel); UiPanelMotion.Kill(shopPanel);
        if (Instance == this) Instance = null;
    }
}
