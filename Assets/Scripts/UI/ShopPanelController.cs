using System;
using UnityEngine;
using UnityEngine.UIElements;

public enum ShopReturnTarget { MainMenu }

[RequireComponent(typeof(UIDocument))]
public class ShopPanelController : MonoBehaviour
{
    public static ShopPanelController Instance { get; private set; }

    public event Action ShopChanged;
    [SerializeField] private ShopCanvasView canvasView;
    private bool canvasShopOpen;

    public void PurchaseFromCanvas(string ballId) => OnPurchaseClicked(ballId);
    public void HideCanvasForNavigation()
    {
        canvasShopOpen = false;
        canvasView?.Hide();
    }
    private void PresentCanvasResult()
    {
        if (canvasView == null) return;
        canvasView.Hide();
        if (_panel != null)
        {
            UiPanelMotion.Kill(_panel);
            _panel.style.display = DisplayStyle.Flex;
            _panel.style.opacity = 1;
        }
        if (_directTab != null) _directTab.style.display = DisplayStyle.None;
        if (_crateTab != null) _crateTab.style.display = DisplayStyle.None;
    }

    private enum RitualPhase
    {
        Idle,
        DropIn,
        Shake,
        Burst,
        RevealRise,
        RevealHold,
        Done
    }

    private VisualElement _panel;
    private bool _panelVisible;
    private Label _creditsLabel;
    private VisualElement _directTab;
    private VisualElement _crateTab;
    private Button _tabDirectBtn;
    private Button _tabCrateBtn;
    private VisualElement _directList;
    private Image _directPreviewOrb;
    private Label _directBallRarity;
    private Label _directBallName;
    private Label _directBallDescription;
    private Label _directBallState;
    private Label _directBallPrice;
    private Button _directPurchaseBtn;
    private Label _crateCostLabel;
    private Label _pityLabel;
    private VisualElement _pityProgressFill;
    private Button _openCrateBtn;

    private VisualElement _resultPopup;
    private VisualElement _vignetteHost;
    private VisualElement _rayHost;
    private VisualElement _screenFlash;
    private VisualElement _lightPillar;
    private VisualElement _ritualStage;
    private VisualElement _sparkLayer;
    private Label _ritualStatus;
    private VisualElement _crateAssembly;
    private VisualElement _crateStack;
    private VisualElement _crateLid;
    private VisualElement _crateSeal;
    private VisualElement _crateCore;
    private VisualElement _revealBlock;
    private VisualElement _revealOrb;
    private Label _rarityLabel;
    private Label _resultTitle;
    private Label _resultBody;
    private Button _resultOkBtn;

    private ShopReturnTarget _returnTarget = ShopReturnTarget.MainMenu;
    private bool _showingDirect = true;
    private bool _ritualBusy;
    private string _selectedDirectBallId;
    private Texture2D _ballPreviewTexture;

    private RitualPhase _ritualPhase = RitualPhase.Idle;
    private float _phaseElapsed;
    private float _phaseDuration;
    private IVisualElementScheduledItem _ritualTicker;
    private CrateOpenResult _pendingResult;
    private Color _pendingBallColor;
    private Color _pendingRarityColor;

    private float _rayIntensity;
    private float _flashAlpha;
    private float _vignetteStrength = 0.65f;
    private Color _rayColor = new Color(0.12f, 0.78f, 1f, 1f);

    private void Awake()
    {
        Instance = this;
        CrateOpenVfx.EnsureExists();
    }

    private void OnDestroy()
    {
        UiPanelMotion.Kill(_panel);
        if (Instance == this) Instance = null;
        StopRitualTicker();
        CrateOpenVfx.Instance?.StopAll();
    }

    private void Start()
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null || doc.rootVisualElement == null) return;

        var root = doc.rootVisualElement;
        _panel = root.Q<VisualElement>("ShopPanel");
        _creditsLabel = root.Q<Label>("ShopCreditsLabel");
        _directTab = root.Q<VisualElement>("ShopDirectTab");
        _crateTab = root.Q<VisualElement>("ShopCrateTab");
        _tabDirectBtn = root.Q<Button>("BtnTabDirect");
        _tabCrateBtn = root.Q<Button>("BtnTabCrate");
        _directList = root.Q<VisualElement>("DirectOffersList");
        _directPreviewOrb = root.Q<Image>("DirectPreviewOrb");
        _ballPreviewTexture = Resources.Load<Texture2D>("UI/shop_ball_preview");
        if (_directPreviewOrb != null && _ballPreviewTexture != null)
        {
            _directPreviewOrb.image = _ballPreviewTexture;
            _directPreviewOrb.scaleMode = ScaleMode.ScaleToFit;
        }
        _directBallRarity = root.Q<Label>("DirectBallRarity");
        _directBallName = root.Q<Label>("DirectBallName");
        _directBallDescription = root.Q<Label>("DirectBallDescription");
        _directBallState = root.Q<Label>("DirectBallState");
        _directBallPrice = root.Q<Label>("DirectBallPrice");
        _directPurchaseBtn = root.Q<Button>("BtnDirectPurchase");
        _crateCostLabel = root.Q<Label>("CrateCostLabel");
        _pityLabel = root.Q<Label>("CratePityLabel");
        _pityProgressFill = root.Q<VisualElement>("CratePityFill");
        _openCrateBtn = root.Q<Button>("BtnOpenCrate");

        _resultPopup = root.Q<VisualElement>("CrateResultPopup");
        _vignetteHost = root.Q<VisualElement>("CrateVignette");
        _rayHost = root.Q<VisualElement>("CrateRayHost");
        _screenFlash = root.Q<VisualElement>("CrateScreenFlash");
        _lightPillar = root.Q<VisualElement>("CrateLightPillar");
        _ritualStage = root.Q<VisualElement>("CrateRitualStage");
        _sparkLayer = root.Q<VisualElement>("CrateSparkLayer");
        _ritualStatus = root.Q<Label>("CrateRitualStatus");
        _crateAssembly = root.Q<VisualElement>("CrateAssembly");
        _crateStack = root.Q<VisualElement>("CrateStack");
        _crateLid = root.Q<VisualElement>("CrateLid");
        _crateSeal = root.Q<VisualElement>("CrateSeal");
        _crateCore = root.Q<VisualElement>("CrateCore");
        _revealBlock = root.Q<VisualElement>("CrateRevealBlock");
        _revealOrb = root.Q<VisualElement>("CrateRevealOrb");
        _rarityLabel = root.Q<Label>("CrateRarityLabel");
        _resultTitle = root.Q<Label>("CrateResultTitle");
        _resultBody = root.Q<Label>("CrateResultBody");
        _resultOkBtn = root.Q<Button>("BtnCrateResultOk");

        CrateRitualFx.BindVignettePulse(_vignetteHost, () => _vignetteStrength);
        CrateRitualFx.BindRayHost(_rayHost, () => _rayIntensity, () => _rayColor);

        root.Q<Button>("BtnShopBack")?.RegisterCallback<ClickEvent>(_ => Hide());
        _tabDirectBtn?.RegisterCallback<ClickEvent>(_ => ShowDirectTab());
        _tabCrateBtn?.RegisterCallback<ClickEvent>(_ => ShowCrateTab());
        _directPurchaseBtn?.RegisterCallback<ClickEvent>(_ => OnPurchaseClicked(_selectedDirectBallId));
        _openCrateBtn?.RegisterCallback<ClickEvent>(_ => OnOpenCrateClicked());
        _resultOkBtn?.RegisterCallback<ClickEvent>(_ => HideResultPopup());
        _resultPopup?.RegisterCallback<PointerDownEvent>(_ =>
        {
            if (_ritualPhase == RitualPhase.Shake)
                _phaseElapsed = _phaseDuration;
        });

        if (_panel != null)
        {
            _panel.style.display = DisplayStyle.None;
            UiPanelMotion.MarkHidden(_panel);
            _panel.RegisterCallback<GeometryChangedEvent>(OnShopGeometryChanged);
        }
        ResetRitualVisuals();
    }

    public void Show(ShopReturnTarget returnTarget)
    {
        _returnTarget = returnTarget;
        PlayerProfile.Load();
        if (canvasView != null)
        {
            canvasShopOpen = true;
            if (_panel != null) _panel.style.display = DisplayStyle.None;
            canvasView.Show();
            return;
        }
        if (_panel != null && !_panelVisible)
        {
            _panelVisible = true;
            UiPanelMotion.Show(_panel, new Vector2(0f, 16f), 0.20f);
        }
        ShowDirectTab();
        RefreshAll();
    }

    public void Hide()
    {
        HideCanvasForNavigation();
        AbortRitual();
        HideResultPopup();
        if (_panel != null && _panelVisible)
        {
            _panelVisible = false;
            UiPanelMotion.Hide(_panel, new Vector2(0f, 8f), 0.12f);
        }

        if (MainMenuController.Instance != null)
            MainMenuController.Instance.OnShopClosed(_returnTarget);
    }

    public void RefreshCredits()
    {
        PlayerProfile.Load();
        if (canvasView != null && canvasView.Visible) canvasView.Refresh();
        if (_creditsLabel != null)
            _creditsLabel.text = $"协议币 {PlayerProfile.Credits:N0}";

        if (_showingDirect && !string.IsNullOrEmpty(_selectedDirectBallId))
            SelectDirectOffer(_selectedDirectBallId);
    }

    private void RefreshAll()
    {
        RefreshCredits();
        if (_showingDirect) ShowDirectTab();
        else ShowCrateTab();
    }

    private void ShowDirectTab()
    {
        if (_ritualBusy) return;
        _showingDirect = true;
        _tabDirectBtn?.AddToClassList("shop-tab-active");
        _tabCrateBtn?.RemoveFromClassList("shop-tab-active");
        if (_directTab != null) _directTab.style.display = DisplayStyle.Flex;
        if (_crateTab != null) _crateTab.style.display = DisplayStyle.None;
        RefreshDirectOffers();
    }

    private void ShowCrateTab()
    {
        if (_ritualBusy) return;
        _showingDirect = false;
        _tabCrateBtn?.AddToClassList("shop-tab-active");
        _tabDirectBtn?.RemoveFromClassList("shop-tab-active");
        if (_directTab != null) _directTab.style.display = DisplayStyle.None;
        if (_crateTab != null) _crateTab.style.display = DisplayStyle.Flex;
        RefreshCrateTab();
    }

    private void RefreshDirectOffers()
    {
        if (_directList == null) return;
        _directList.Clear();

        var shop = ShopCatalog.Load();
        var run = RunCatalog.Load();
        if (shop == null || run == null)
        {
            RefreshDirectDetails(null, 0);
            return;
        }

        string firstBallId = null;
        BallDefinition selectedBall = null;
        int selectedPrice = 0;

        foreach (var offer in shop.directOffers)
        {
            if (offer == null || string.IsNullOrEmpty(offer.ballId)) continue;
            var ball = run.GetBall(offer.ballId);
            if (ball == null || ball.acquisitionType != BallAcquisitionType.DirectPurchase) continue;

            int price = shop.GetDirectPrice(ball);
            if (firstBallId == null) firstBallId = ball.ballId;
            _directList.Add(BuildDirectOfferRow(ball, price));
            if (ball.ballId == _selectedDirectBallId)
            {
                selectedBall = ball;
                selectedPrice = price;
            }
        }

        if (_directList.childCount == 0)
        {
            var empty = new Label("暂无直购弹珠");
            empty.AddToClassList("shop-empty-label");
            _directList.Add(empty);
            _selectedDirectBallId = null;
            RefreshDirectDetails(null, 0);
            return;
        }

        if (selectedBall == null)
        {
            _selectedDirectBallId = firstBallId;
            selectedBall = run.GetBall(_selectedDirectBallId);
            if (selectedBall != null) selectedPrice = shop.GetDirectPrice(selectedBall);
        }

        RefreshDirectDetails(selectedBall, selectedPrice);
    }

    private void OnShopGeometryChanged(GeometryChangedEvent evt)
    {
        if (_panel == null) return;
        bool portrait = evt.newRect.height > 0f && evt.newRect.width / evt.newRect.height < 0.9f;
        _panel.EnableInClassList("shop-portrait", portrait);
    }

    private VisualElement BuildDirectOfferRow(BallDefinition ball, int price)
    {
        bool owned = PlayerProfile.IsBallUnlocked(ball.ballId);

        var row = new Button();
        row.text = string.Empty;
        row.AddToClassList("shop-offer-row");
        row.AddToClassList("shop-ball-option");
        row.userData = ball.ballId;
        if (ball.ballId == _selectedDirectBallId)
            row.AddToClassList("shop-ball-option-selected");
        if (owned)
            row.AddToClassList("shop-ball-option-owned");
        row.RegisterCallback<ClickEvent>(_ => SelectDirectOffer(ball.ballId));

        var orb = new Image();
        orb.AddToClassList("shop-offer-orb");
        orb.image = _ballPreviewTexture;
        orb.scaleMode = ScaleMode.ScaleToFit;
        orb.tintColor = Color.Lerp(Color.white, ball.glowColor, 0.28f);
        row.Add(orb);

        var info = new VisualElement();
        info.AddToClassList("shop-offer-info");

        var name = new Label(ball.displayName);
        name.AddToClassList("shop-offer-name");
        info.Add(name);

        var state = new Label(owned ? "已拥有" : $"{price} 币");
        state.AddToClassList("shop-offer-state");
        if (owned) state.AddToClassList("shop-offer-state-owned");
        info.Add(state);
        row.Add(info);

        return row;
    }

    private void SelectDirectOffer(string ballId)
    {
        if (string.IsNullOrEmpty(ballId) || _ritualBusy) return;

        _selectedDirectBallId = ballId;
        var shop = ShopCatalog.Load();
        var ball = RunCatalog.Load()?.GetBall(ballId);
        int price = ball != null && shop != null ? shop.GetDirectPrice(ball) : 0;
        RefreshDirectDetails(ball, price);
    }

    private void RefreshDirectDetails(BallDefinition ball, int price)
    {
        foreach (var option in _directList.Children())
        {
            bool selected = option.userData is string id && ball != null && id == ball.ballId;
            option.EnableInClassList("shop-ball-option-selected", selected);
        }

        if (ball == null)
        {
            if (_directBallRarity != null) _directBallRarity.text = string.Empty;
            if (_directBallName != null) _directBallName.text = "暂无可购弹珠";
            if (_directBallDescription != null) _directBallDescription.text = "当前没有可直接购买的弹珠。";
            if (_directBallState != null) _directBallState.text = "商店库存为空";
            if (_directBallPrice != null) _directBallPrice.text = "— 币";
            if (_directPurchaseBtn != null)
            {
                _directPurchaseBtn.text = "暂无商品";
                _directPurchaseBtn.SetEnabled(false);
            }
            return;
        }

        bool owned = PlayerProfile.IsBallUnlocked(ball.ballId);
        bool canAfford = PlayerProfile.Credits >= price;
        if (_directPreviewOrb != null)
            _directPreviewOrb.tintColor = Color.Lerp(Color.white, ball.glowColor, 0.22f);
        if (_directBallRarity != null)
        {
            _directBallRarity.text = RarityText(ball.crateRarity);
            _directBallRarity.style.color = RarityColor(ball.crateRarity);
        }
        if (_directBallName != null) _directBallName.text = ball.displayName;
        if (_directBallDescription != null) _directBallDescription.text = ball.loadoutDescription;
        if (_directBallState != null)
        {
            _directBallState.text = owned ? "已拥有" : canAfford ? "可购买" : "协议币不足";
            _directBallState.EnableInClassList("shop-product-state-owned", owned);
            _directBallState.EnableInClassList("shop-product-state-unavailable", !owned && !canAfford);
        }
        if (_directBallPrice != null) _directBallPrice.text = $"{price} 币";
        if (_directPurchaseBtn != null)
        {
            _directPurchaseBtn.text = owned ? "已拥有" : canAfford ? "购买弹珠" : "协议币不足";
            _directPurchaseBtn.SetEnabled(!owned && canAfford);
        }
    }

    private void OnPurchaseClicked(string ballId)
    {
        if (ShopService.TryPurchaseDirect(ballId, out string error))
        {
            RefreshAll();
            ShopChanged?.Invoke();
            return;
        }
        ShowSimpleError("购买失败", error ?? "未知错误");
    }

    private void RefreshCrateTab()
    {
        var shop = ShopCatalog.Load();
        if (shop == null) return;

        if (_crateCostLabel != null)
            _crateCostLabel.text = $"单次开启 · {shop.crateCost} 协议币";

        if (_pityLabel != null)
        {
            int sinceEpic = PlayerProfile.CrateOpensSinceLastEpic;
            int toHard = Mathf.Max(0, ShopCatalog.HardPityOpens - sinceEpic);
            _pityLabel.text = $"距史诗保底 {toHard} 抽 · 已连续 {sinceEpic} 抽无史诗";
            if (_pityProgressFill != null)
            {
                float progress = Mathf.Clamp01(sinceEpic / (float)ShopCatalog.HardPityOpens);
                _pityProgressFill.style.width = new StyleLength(new Length(progress * 100f, LengthUnit.Percent));
            }
        }

        if (_openCrateBtn != null)
            _openCrateBtn.SetEnabled(!_ritualBusy && PlayerProfile.Credits >= shop.crateCost);
    }

    private void OnOpenCrateClicked()
    {
        BeginOpenCrate();
    }

    /// <summary>UIShowcase / 调试入口。</summary>
    public void BeginOpenCrate()
    {
        if (_ritualBusy) return;

        var shop = ShopCatalog.Load();
        if (shop == null)
        {
            ShowSimpleError("开启失败", "宝箱数据未配置");
            return;
        }

        if (PlayerProfile.Credits < shop.crateCost)
        {
            ShowSimpleError("开启失败", "协议币不足");
            return;
        }

        _ritualBusy = true;
        PresentCanvasResult();
        _openCrateBtn?.SetEnabled(false);

        ResetRitualVisuals();
        if (_resultPopup != null)
            _resultPopup.style.display = DisplayStyle.Flex;
        if (_resultOkBtn != null)
            _resultOkBtn.style.display = DisplayStyle.None;
        if (_revealBlock != null)
            _revealBlock.style.display = DisplayStyle.None;
        if (_crateAssembly != null)
        {
            _crateAssembly.style.display = DisplayStyle.Flex;
            _crateAssembly.style.opacity = 1f;
        }

        SetStatus("宝箱降落中…");
        ClearSparks();
        _rayIntensity = 0f;
        _flashAlpha = 0f;
        _vignetteStrength = 0.55f;
        _rayColor = new Color(0.12f, 0.78f, 1f, 1f);
        CrateOpenVfx.EnsureExists().PlayCharge(new Color(0.12f, 0.78f, 1f, 0.9f));

        EnterPhase(RitualPhase.DropIn, 0.4f);
        StartRitualTicker();
    }

    private void Update()
    {
        if (_ritualPhase == RitualPhase.Idle || _ritualPhase == RitualPhase.Done)
            return;
        TickRitualFrame(Time.unscaledDeltaTime);
    }

    private void StartRitualTicker()
    {
        StopRitualTicker();
        var host = _resultPopup ?? _ritualStage ?? _panel;
        if (host == null) return;
        _ritualTicker = host.schedule.Execute(() => TickRitualFrame(0.016f)).Every(16);
    }

    private void StopRitualTicker()
    {
        _ritualTicker?.Pause();
        _ritualTicker = null;
    }

    private void TickRitualFrame(float dt)
    {
        if (_ritualPhase == RitualPhase.Idle || _ritualPhase == RitualPhase.Done)
        {
            StopRitualTicker();
            return;
        }

        _phaseElapsed += Mathf.Max(0.001f, dt);
        float t = _phaseDuration > 0f ? Mathf.Clamp01(_phaseElapsed / _phaseDuration) : 1f;

        switch (_ritualPhase)
        {
            case RitualPhase.DropIn:
                AnimateDropIn(t);
                break;
            case RitualPhase.Shake:
                AnimateShake(t);
                break;
            case RitualPhase.Burst:
                AnimateBurst(t);
                break;
            case RitualPhase.RevealRise:
                AnimateRevealRise(t);
                if (_phaseElapsed < _phaseDuration) return;
                EnterPhase(RitualPhase.RevealHold, 0.55f);
                CrateOpenVfx.Instance?.PlayConfetti(_pendingRarityColor);
                return;
            case RitualPhase.RevealHold:
                break;
        }

        if (_phaseElapsed < _phaseDuration)
            return;

        AdvanceRitual();
    }

    private void EnterPhase(RitualPhase phase, float duration)
    {
        _ritualPhase = phase;
        _phaseElapsed = 0f;
        _phaseDuration = duration;
    }

    private void AdvanceRitual()
    {
        switch (_ritualPhase)
        {
            case RitualPhase.DropIn:
                SetStatus("蓄力中… 点击屏幕可跳过");
                EnterPhase(RitualPhase.Shake, 3.1f);
                break;

            case RitualPhase.Shake:
                if (!ShopService.TryOpenCrate(out CrateOpenResult result, out string error))
                {
                    CrateOpenVfx.Instance?.StopAll();
                    ClearSparks();
                    ShowSimpleError("开启失败", error ?? "未知错误");
                    FinishRitualBusy();
                    return;
                }

                RefreshCredits();
                ShopChanged?.Invoke();
                _pendingResult = result;
                _pendingRarityColor = RarityColor(result.Rarity);
                _pendingBallColor = ResolveBallColor(result.BallId);
                _rayColor = Color.Lerp(new Color(0.12f, 0.78f, 1f), _pendingRarityColor, 0.55f);

                SetStatus("");
                CrateOpenVfx.Instance?.PlayOpen(_pendingRarityColor, _pendingBallColor);
                SpawnSparkBurst(Color.Lerp(_pendingRarityColor, Color.white, 0.5f), 48, 160f);
                _flashAlpha = 0.92f;
                _rayIntensity = 1f;
                if (_screenFlash != null)
                    _screenFlash.style.opacity = _flashAlpha;
                if (_lightPillar != null)
                    _lightPillar.style.opacity = 0.55f;
                EnterPhase(RitualPhase.Burst, 0.58f);
                break;

            case RitualPhase.Burst:
                PresentReveal(_pendingResult, _pendingBallColor, _pendingRarityColor);
                EnterPhase(RitualPhase.RevealRise, 0.75f);
                break;

            case RitualPhase.RevealHold:
                if (_resultOkBtn != null)
                    _resultOkBtn.style.display = DisplayStyle.Flex;
                _ritualPhase = RitualPhase.Done;
                FinishRitualBusy();
                break;
        }
    }

    private static Scale UiScale(float s) => new Scale(new Vector3(s, s, 1f));

    private void AnimateDropIn(float t)
    {
        float ease = CrateEasing.OutBack(t);
        if (_crateStack != null)
            _crateStack.style.scale = UiScale(Mathf.Lerp(0.72f, 1f, ease));
        _vignetteStrength = Mathf.Lerp(0.45f, 0.68f, ease);
    }

    private void AnimateShake(float t)
    {
        float build = CrateEasing.InCubic(t);
        float breath = 0.72f + 0.28f * Mathf.Sin(CrateEasing.InOutSine(t) * Mathf.PI);
        float intensity = build * breath;

        _rayIntensity = Mathf.Lerp(_rayIntensity, intensity * 0.95f, 0.08f);
        _vignetteStrength = 0.62f + intensity * 0.18f;

        float freq = Mathf.Lerp(5f, 14f, build);
        float ampX = Mathf.Lerp(1.2f, 12f, intensity);
        float ampY = Mathf.Lerp(0.8f, 7.5f, intensity);
        float sway = Mathf.Sin(_phaseElapsed * 2.1f) * Mathf.Lerp(2f, 5f, intensity);

        float shakeX = Mathf.Sin(_phaseElapsed * freq) * ampX + sway;
        float shakeY = Mathf.Cos(_phaseElapsed * (freq * 0.82f)) * ampY;
        float rot = shakeX * 0.42f;

        float breatheScale = 1f + CrateEasing.InOutSine(t) * 0.045f + intensity * 0.035f;

        if (_crateStack != null)
        {
            _crateStack.style.translate = new Translate(shakeX, shakeY);
            _crateStack.style.rotate = new Rotate(new Angle(rot, AngleUnit.Degree));
            _crateStack.style.scale = UiScale(breatheScale);
        }

        var glow = _ritualStage?.Q<VisualElement>("CrateGlow");
        if (glow != null)
            glow.style.opacity = 0.28f + intensity * 0.6f;

        if (_crateSeal != null)
            _crateSeal.style.scale = UiScale(1f + intensity * 0.14f);

        if (t > 0.55f && UnityEngine.Random.value < 0.045f + intensity * 0.04f)
            SpawnSparkBurst(new Color(1f, 0.92f, 0.45f, 1f), 2, 28f + intensity * 36f);
    }

    private void AnimateBurst(float t)
    {
        _rayIntensity = Mathf.Lerp(1f, 0.2f, CrateEasing.OutCubic(t));
        _flashAlpha = Mathf.Lerp(_flashAlpha, 0f, t * 1.8f);
        if (_screenFlash != null)
            _screenFlash.style.opacity = _flashAlpha;

        if (_lightPillar != null)
            _lightPillar.style.opacity = Mathf.Lerp(0.55f, 0.05f, t);

        if (_crateLid != null)
        {
            _crateLid.style.translate = new Translate(Mathf.Lerp(0f, 24f, t), Mathf.Lerp(0f, -92f, CrateEasing.OutCubic(t)));
            _crateLid.style.rotate = new Rotate(new Angle(Mathf.Lerp(0f, -38f, CrateEasing.OutCubic(t)), AngleUnit.Degree));
        }

        if (_crateSeal != null)
        {
            _crateSeal.style.opacity = 1f - t;
            _crateSeal.style.scale = UiScale(1f + t * 1.6f);
        }

        if (_crateCore != null)
        {
            _crateCore.style.opacity = Mathf.Clamp01(t * 1.4f);
            _crateCore.style.scale = UiScale(0.4f + t * 1.2f);
        }

        if (_crateAssembly != null)
            _crateAssembly.style.opacity = 1f - t * 0.75f;
    }

    private void AnimateRevealRise(float t)
    {
        float ease = CrateEasing.OutBack(t);
        if (_revealBlock != null)
        {
            _revealBlock.style.translate = new Translate(0, 140f * (1f - ease));
            _revealBlock.style.opacity = ease;
        }
        if (_revealOrb != null)
            _revealOrb.style.scale = UiScale(0.15f + 0.95f * ease);
    }

    private void ClearSparks()
    {
        _sparkLayer?.Clear();
    }

    private void SpawnSparkBurst(Color color, int count, float radius)
    {
        if (_sparkLayer == null) return;
        var stage = _ritualStage;
        float cx = stage != null ? stage.resolvedStyle.width * 0.5f : 200f;
        float cy = stage != null ? stage.resolvedStyle.height * 0.58f : 180f;
        if (float.IsNaN(cx) || cx < 1f) cx = 200f;
        if (float.IsNaN(cy) || cy < 1f) cy = 160f;

        for (int i = 0; i < count; i++)
        {
            float ang = (i / (float)count) * Mathf.PI * 2f + UnityEngine.Random.Range(-0.2f, 0.2f);
            float dist = UnityEngine.Random.Range(radius * 0.25f, radius);
            var spark = new VisualElement();
            spark.AddToClassList("crate-ui-spark");
            spark.style.backgroundColor = color;
            spark.style.left = cx + Mathf.Cos(ang) * dist;
            spark.style.top = cy + Mathf.Sin(ang) * dist;
            float size = UnityEngine.Random.Range(3f, 8f);
            spark.style.width = size;
            spark.style.height = size;
            _sparkLayer.Add(spark);

            float life = UnityEngine.Random.Range(0.35f, 0.7f);
            spark.schedule.Execute(() =>
            {
                if (spark.parent != null)
                    spark.RemoveFromHierarchy();
            }).StartingIn((long)(life * 1000f));
        }
    }

    private void PresentReveal(CrateOpenResult result, Color ballColor, Color rarityColor)
    {
        if (_revealBlock != null)
        {
            _revealBlock.style.display = DisplayStyle.Flex;
            _revealBlock.style.translate = new Translate(0, 140f);
            _revealBlock.style.opacity = 0f;
        }

        if (_revealOrb != null)
        {
            _revealOrb.style.display = DisplayStyle.Flex;
            _revealOrb.style.backgroundColor = ballColor;
            _revealOrb.style.borderLeftColor = rarityColor;
            _revealOrb.style.borderRightColor = rarityColor;
            _revealOrb.style.borderTopColor = rarityColor;
            _revealOrb.style.borderBottomColor = rarityColor;
            _revealOrb.style.scale = UiScale(0.15f);
        }

        if (_rarityLabel != null)
        {
            _rarityLabel.RemoveFromClassList("crate-rarity-rare");
            _rarityLabel.RemoveFromClassList("crate-rarity-epic");
            _rarityLabel.RemoveFromClassList("crate-rarity-legendary");
            _rarityLabel.text = RarityText(result.Rarity);
            _rarityLabel.AddToClassList(RarityClass(result.Rarity));
            _rarityLabel.style.color = rarityColor;
        }

        if (_resultTitle != null)
            _resultTitle.text = result.IsDuplicate ? "重复弹珠" : "获得弹珠";

        if (_resultBody != null)
        {
            _resultBody.text = result.IsDuplicate
                ? $"{result.BallDisplayName}\n重复折算 +{result.CreditsRefund} 协议币"
                : $"{result.BallDisplayName}\n已永久解锁，可在战前配置选用";
        }

        SetStatus(result.IsDuplicate ? "重复 · 已折算协议币" : "新弹珠已解锁");
    }

    private void SetStatus(string text)
    {
        if (_ritualStatus != null)
            _ritualStatus.text = text;
    }

    private void ResetRitualVisuals()
    {
        _rayIntensity = 0f;
        _flashAlpha = 0f;
        _vignetteStrength = 0.65f;
        _rayColor = new Color(0.12f, 0.78f, 1f, 1f);

        if (_screenFlash != null)
            _screenFlash.style.opacity = 0f;
        if (_lightPillar != null)
            _lightPillar.style.opacity = 0f;

        if (_crateAssembly != null)
        {
            _crateAssembly.style.display = DisplayStyle.Flex;
            _crateAssembly.style.opacity = 1f;
        }
        if (_crateStack != null)
        {
            _crateStack.style.translate = new Translate(0, 0);
            _crateStack.style.rotate = new Rotate(new Angle(0, AngleUnit.Degree));
            _crateStack.style.scale = UiScale(1f);
        }
        if (_crateLid != null)
        {
            _crateLid.style.translate = new Translate(0, 0);
            _crateLid.style.rotate = new Rotate(new Angle(0, AngleUnit.Degree));
        }
        if (_crateSeal != null)
        {
            _crateSeal.style.opacity = 1f;
            _crateSeal.style.scale = UiScale(1f);
        }
        if (_crateCore != null)
        {
            _crateCore.style.opacity = 0f;
            _crateCore.style.scale = UiScale(0.4f);
        }
        if (_revealBlock != null)
        {
            _revealBlock.style.display = DisplayStyle.None;
            _revealBlock.style.translate = new Translate(0, 0);
            _revealBlock.style.opacity = 1f;
        }
        if (_resultOkBtn != null)
            _resultOkBtn.style.display = DisplayStyle.None;
        if (_revealOrb != null)
            _revealOrb.style.display = DisplayStyle.Flex;
        SetStatus("");
    }

    private void AbortRitual()
    {
        StopRitualTicker();
        _ritualPhase = RitualPhase.Idle;
        _ritualBusy = false;
        CrateOpenVfx.Instance?.StopAll();
        ClearSparks();
    }

    private void FinishRitualBusy()
    {
        StopRitualTicker();
        _ritualBusy = false;
        if (_ritualPhase != RitualPhase.Done)
            _ritualPhase = RitualPhase.Idle;
        RefreshCrateTab();
    }

    private void ShowSimpleError(string title, string body)
    {
        PresentCanvasResult();
        AbortRitual();
        ResetRitualVisuals();
        if (_resultPopup != null)
            _resultPopup.style.display = DisplayStyle.Flex;
        if (_crateAssembly != null)
            _crateAssembly.style.display = DisplayStyle.None;
        if (_revealBlock != null)
            _revealBlock.style.display = DisplayStyle.Flex;
        if (_revealOrb != null)
            _revealOrb.style.display = DisplayStyle.None;
        if (_rarityLabel != null)
            _rarityLabel.text = "ERROR";
        if (_resultTitle != null)
            _resultTitle.text = title;
        if (_resultBody != null)
            _resultBody.text = body;
        if (_resultOkBtn != null)
            _resultOkBtn.style.display = DisplayStyle.Flex;
        SetStatus("开启失败");
    }

    private void HideResultPopup()
    {
        AbortRitual();
        CrateOpenVfx.Instance?.StopAll();
        ClearSparks();
        if (_resultPopup != null)
            _resultPopup.style.display = DisplayStyle.None;
        if (_revealOrb != null)
            _revealOrb.style.display = DisplayStyle.Flex;
        ResetRitualVisuals();
        RefreshCrateTab();
        if (canvasView != null)
        {
            if (_panel != null) _panel.style.display = DisplayStyle.None;
            if (canvasShopOpen) canvasView.Resume();
        }
    }

    private static Color ResolveBallColor(string ballId)
    {
        var ball = RunCatalog.Load()?.GetBall(ballId);
        return ball != null ? ball.glowColor : new Color(0f, 1f, 1f, 1f);
    }

    private static Color RarityColor(BallCrateRarity rarity) => rarity switch
    {
        BallCrateRarity.Epic => new Color(0.78f, 0.38f, 1f, 1f),
        BallCrateRarity.Legendary => new Color(0.68f, 0.9f, 1f, 1f),
        _ => new Color(0.12f, 0.78f, 1f, 1f)
    };

    private static string RarityText(BallCrateRarity rarity) => rarity switch
    {
        BallCrateRarity.Epic => "EPIC",
        BallCrateRarity.Legendary => "LEGENDARY",
        _ => "RARE"
    };

    private static string RarityClass(BallCrateRarity rarity) => rarity switch
    {
        BallCrateRarity.Epic => "crate-rarity-epic",
        BallCrateRarity.Legendary => "crate-rarity-legendary",
        _ => "crate-rarity-rare"
    };
}
