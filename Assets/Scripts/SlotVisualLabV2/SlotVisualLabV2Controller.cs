using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Slot cabinet presentation shared by the isolated preview and live rewards UI.
/// In gameplayMode the BuffSelectionController owns rolls, chip costs and reward application.
/// </summary>
[ExecuteAlways, RequireComponent(typeof(UIDocument))]
public sealed class SlotVisualLabV2Controller : MonoBehaviour
{
    public bool gameplayMode;
    [Range(.65f, 1f)] public float gameplayScale = .84f;
    public bool IsAssembling => assemblyStart != null || (assemblyTransition?.IsPlaying ?? false);
    public Texture2D backplate;
    public Texture2D[] glyphs;
    public Rect[] glyphBounds;
    public Texture2D edgeShade;
    public Texture2D selectionGlow;
    public Font displayFont;
    public SlotMachineSfx sfx;
    [Tooltip("Buff assets shown by this isolated visual test scene.")]
    public BuffDefinition[] demoBuffPool;
    [Min(0)] public int startingDemoChips = 8;
    [Min(1)] public float reelPitch = 118f;

    private VisualElement root, stage, testDock, inspectOverlay;
    private readonly Image[,] slots = new Image[3, 7];
    private readonly VisualElement[] windows = new VisualElement[3];
    private readonly VisualElement[] glow = new VisualElement[3];
    private readonly float[] offsets = new float[3];
    private readonly int[] glyphResults = { 0, 6, 2 };
    private readonly bool[] reelSettled = new bool[3];
    private readonly int[] lastTickCycle = new int[3];
    private readonly Button[] resultButtons = new Button[3];
    private readonly Label[] outcomeLabels = new Label[3];
    private readonly Action[] inspectCallbacks = new Action[3];
    private Sprite[] centeredGlyphs;
    private Label status, bankValue, nextCostValue;
    private Label inspectName, inspectRarity, inspectDescription;
    private Button rollButton, cashOutButton, inspectClose;
    private SlotSpinSession session;
    private ReelResult pendingResult;
    private bool spinning, specialPreset, overlays = true, settled;
    private float started, spinDuration = 2f;
    private int activeReel = -1, localChips;
    private const int DemoWave = 1;
    private static readonly string[] OutcomeClasses =
    {
        "outcome-full", "outcome-scrap", "outcome-ignored", "outcome-applied", "outcome-empty"
    };
    private SlotAssemblyTransition assemblyTransition;
    private Coroutine assemblyStart;

    private void OnEnable()
    {
        // The live controller rebuilds synchronously in Start. A scheduled rebuild can
        // wake when the reward panel first renders and cancel its assembly transition.
        if (gameplayMode) return;
        var document = GetComponent<UIDocument>();
        if (document.rootVisualElement != null)
            document.rootVisualElement.schedule.Execute(Rebuild);
    }

    private void OnDisable()
    {
        if (assemblyStart != null) { StopCoroutine(assemblyStart); assemblyStart = null; }
        assemblyTransition?.Dispose();
        if (root != null) root.UnregisterCallback<GeometryChangedEvent>(Fit);
        spinning = false;
        UnbindButtons();
        ReleaseGlyphs();
    }

    public void Rebuild()
    {
        if (assemblyStart != null) { StopCoroutine(assemblyStart); assemblyStart = null; }
        assemblyTransition?.Dispose();
        UnbindButtons();
        if (root != null) root.UnregisterCallback<GeometryChangedEvent>(Fit);
        root = GetComponent<UIDocument>().rootVisualElement;
        stage = root.Q<VisualElement>("stage");
        if (stage == null || glyphs == null || glyphs.Length < 8) return;

        spinning = false;
        overlays = true;
        ReleaseGlyphs();
        centeredGlyphs = new Sprite[glyphs.Length];
        for (int i = 0; i < glyphs.Length; i++)
        {
            Rect bounds = glyphBounds != null && i < glyphBounds.Length && glyphBounds[i].width > 0
                ? glyphBounds[i]
                : new Rect(0, 0, glyphs[i].width, glyphs[i].height);
            centeredGlyphs[i] = Sprite.Create(glyphs[i], bounds, new Vector2(.5f, .5f), 100);
            centeredGlyphs[i].hideFlags = HideFlags.HideAndDontSave;
        }

        status = stage.Q<Label>("statusLabel");
        rollButton = stage.Q<Button>("rollButton");
        cashOutButton = stage.Q<Button>("cashOutButton");
        testDock = stage.Q<VisualElement>("testDock");
        inspectOverlay = stage.Q<VisualElement>("inspectOverlay");
        inspectName = stage.Q<Label>("inspectName");
        inspectRarity = stage.Q<Label>("inspectRarity");
        inspectDescription = stage.Q<Label>("inspectDescription");
        inspectClose = stage.Q<Button>("inspectClose");
        bankValue = stage.Q<Label>("bankValue");
        nextCostValue = stage.Q<Label>("nextCostValue");
        for (int i = 0; i < 3; i++)
        {
            windows[i] = stage.Q<VisualElement>("reel" + (i + 1));
            if (windows[i].Q("unknownProtocol") == null)
            {
                var unknown = new Label("◇ ?") { name = "unknownProtocol", pickingMode = PickingMode.Ignore };
                unknown.style.position = Position.Absolute;
                unknown.style.left = 0; unknown.style.top = 169;
                unknown.style.width = 122; unknown.style.height = 96;
                unknown.style.fontSize = 38;
                unknown.style.color = new Color(.15f, .84f, .94f, .8f);
                windows[i].Add(unknown);
            }
            glow[i] = windows[i].Q<VisualElement>("selectionGlow");
            for (int j = 0; j < 7; j++) slots[i, j] = windows[i].Q<Image>("cell" + j);
            resultButtons[i] = stage.Q<Button>("result" + (i + 1));
            outcomeLabels[i] = stage.Q<Label>("outcome" + (i + 1));
            int index = i;
            inspectCallbacks[i] = () => InspectReel(index);
            resultButtons[i].clicked += inspectCallbacks[i];
        }

        if (!gameplayMode) rollButton.clicked += Advance;
        if (!gameplayMode) cashOutButton.clicked += CashOut;
        inspectClose.clicked += CloseInspect;
        if (stage.Q<Button>("labButton") is Button labButton) labButton.clicked += ToggleLab;
        if (stage.Q<Button>("normalButton") is Button normalButton) normalButton.clicked += RollNormal;
        if (stage.Q<Button>("specialButton") is Button specialButton) specialButton.clicked += RollSpecial;
        if (stage.Q<Button>("bloomButton") is Button bloomButton) bloomButton.clicked += ToggleBloom;
        if (stage.Q<Button>("resetButton") is Button resetButton) resetButton.clicked += ResetPreview;
        if (stage.Q<Button>("audioButton") is Button audioButton) audioButton.clicked += PreviewAudio;
        stage.Query<Button>().ForEach(button =>
        {
            button.RegisterCallback<PointerEnterEvent>(OnButtonHover);
            button.RegisterCallback<PointerDownEvent>(OnButtonPress);
        });

        root.RegisterCallback<GeometryChangedEvent>(Fit);
        if (!gameplayMode) ResetPreview();
        FitStage();
        if (Application.isPlaying && !gameplayMode) ReplayAssembly();
    }

    public void ReplayAssembly()
    {
        if (!Application.isPlaying || stage == null || spinning) return;
        if (assemblyStart != null) { StopCoroutine(assemblyStart); assemblyStart = null; }
        assemblyTransition?.Dispose();
        if (gameplayMode)
        {
            GetComponent<SlotVisualLabV2Bloom>()?.SetCabinetFrameVisible(false);
            assemblyStart = StartCoroutine(WaitForGameplayLayout());
            return;
        }
        PlayCabinetAssembly();
    }

    private IEnumerator WaitForGameplayLayout()
    {
        // Display.None panels have zero geometry until UI Toolkit publishes a layout.
        yield return null;
        yield return null;
        for (int frame = 0; frame < 60; frame++)
        {
            var output = GetComponent<SlotVisualLabV2Bloom>();
            var host = output != null ? output.GameplayOverlayRoot : null;
            if (output != null && output.IsBackdropReady && host != null && host.resolvedStyle.width > 0 && host.resolvedStyle.height > 0 &&
                stage.resolvedStyle.width > 0 && stage.resolvedStyle.height > 0)
            {
                FitStage();
                break;
            }
            yield return null;
        }
        PlayCabinetAssembly();
        // Play hides the cabinet and schedules the initial assembly geometry. Keep
        // the compositor hidden until UI Toolkit and the camera replace the old frame.
        yield return null;
        yield return new WaitForEndOfFrame();
        GetComponent<SlotVisualLabV2Bloom>()?.SetCabinetFrameVisible(true);
        assemblyStart = null;
    }

    private void PlayCabinetAssembly()
    {
        assemblyTransition = new SlotAssemblyTransition(stage);
        sfx?.PlayPress();
        var relighting = GetComponent<SlotCabinetRelighting>();
        Texture appearance = relighting != null ? relighting.GetAssemblyTexture() : backplate;
        assemblyTransition.Play(backplate, stage.Q("machineBackplate"), () =>
        {
            sfx?.PlayRollConfirm();
            rollButton.Focus();
        }, GetComponent<SlotMechanicalAssembly3D>(), appearance);
    }

    public void PreviewAssembly(float progress) => assemblyTransition?.Preview(progress);

    private void RollNormal()
    {
        specialPreset = false;
        ResetPreview();
    }

    private void RollSpecial()
    {
        specialPreset = true;
        ResetPreview();
    }

    private void PreviewAudio() => sfx?.PreviewFullSet();
    private void OnButtonHover(PointerEnterEvent evt) => sfx?.PlayHover();
    private void OnButtonPress(PointerDownEvent evt) { if (evt.button == 0) sfx?.PlayPress(); }
    private void ToggleBloom() => GetComponent<SlotVisualLabV2Bloom>()?.ToggleBloom();
    private void ToggleLab() => testDock.style.display = testDock.resolvedStyle.display == DisplayStyle.None
        ? DisplayStyle.Flex
        : DisplayStyle.None;

    private void UnbindButtons()
    {
        if (stage == null) return;
        for (int i = 0; i < resultButtons.Length; i++)
            if (resultButtons[i] != null && inspectCallbacks[i] != null)
                resultButtons[i].clicked -= inspectCallbacks[i];

        stage.Query<Button>().ForEach(button =>
        {
            button.UnregisterCallback<PointerEnterEvent>(OnButtonHover);
            button.UnregisterCallback<PointerDownEvent>(OnButtonPress);
        });
        var button = stage.Q<Button>("rollButton"); if (button != null) button.clicked -= Advance;
        button = stage.Q<Button>("cashOutButton"); if (button != null) button.clicked -= CashOut;
        button = stage.Q<Button>("inspectClose"); if (button != null) button.clicked -= CloseInspect;
        button = stage.Q<Button>("labButton"); if (button != null) button.clicked -= ToggleLab;
        button = stage.Q<Button>("normalButton"); if (button != null) button.clicked -= RollNormal;
        button = stage.Q<Button>("specialButton"); if (button != null) button.clicked -= RollSpecial;
        button = stage.Q<Button>("bloomButton"); if (button != null) button.clicked -= ToggleBloom;
        button = stage.Q<Button>("resetButton"); if (button != null) button.clicked -= ResetPreview;
        button = stage.Q<Button>("audioButton"); if (button != null) button.clicked -= PreviewAudio;
    }

    private void Fit(GeometryChangedEvent evt)
    {
        FitStage();
        if (centeredGlyphs != null && !spinning)
            for (int i = 0; i < 3; i++) RenderReel(i);
    }

    public void FitStage()
    {
        if (stage == null || root == null) return;
        float width = root.resolvedStyle.width;
        float height = root.resolvedStyle.height;
        if (float.IsNaN(width) || float.IsNaN(height) || width <= 0 || height <= 0) return;

        float left = 0, top = 0, availableWidth = width, availableHeight = height;
        if (Application.isPlaying && Screen.width > 0 && Screen.height > 0)
        {
            Rect safe = Screen.safeArea;
            left = safe.xMin / Screen.width * width;
            top = (Screen.height - safe.yMax) / Screen.height * height;
            availableWidth = safe.width / Screen.width * width;
            availableHeight = safe.height / Screen.height * height;
        }

        float designWidth = stage.resolvedStyle.width;
        float designHeight = stage.resolvedStyle.height;
        if (float.IsNaN(designWidth) || float.IsNaN(designHeight) || designWidth <= 0 || designHeight <= 0) return;
        float scale = Mathf.Min(availableWidth / designWidth, availableHeight / designHeight);
        if (gameplayMode) scale *= gameplayScale;
        stage.transform.scale = new Vector3(scale, scale, 1);
        // The USS centers the fixed 9:16 design canvas when viewed in UI Builder,
        // where this scene's runtime fitting controller is not present. Once this
        // controller runs, remove those authoring offsets and fit to the real panel.
        stage.style.marginLeft = 0;
        stage.style.marginTop = 0;
        stage.style.left = left + (availableWidth - designWidth * scale) * .5f;
        stage.style.top = top + (availableHeight - designHeight * scale) * .5f;
    }

    private SlotSpinSession NewDemoSession() => new SlotSpinSession
    {
        waveIndex = DemoWave,
        useProgressiveReveal = true,
        revealStage = SlotRevealStage.None
    };

    private void Advance()
    {
        if (gameplayMode) return;
        if (!Application.isPlaying || stage == null || spinning || settled || (assemblyTransition?.IsPlaying ?? false)) return;
        if (session == null) session = NewDemoSession();

        spinDuration = 2f;
        int index = session.revealedReelCount;
        if (index >= 3) return;
        int cost = SlotMachineBuffRoller.GetContinueChipCost(session);
        if (localChips < cost) return;

        ReelRarity rarity = specialPreset ? ReelRarity.Epic : RollDemoRarity();
        BuffDefinition buff = PickDemoBuff(ref rarity);
        if (buff == null)
        {
            status.text = "NO BUFF FOR THIS RARITY";
            return;
        }

        localChips -= cost;
        pendingResult = new ReelResult { rarity = rarity, buff = buff, isRevealed = true };
        activeReel = index;
        started = Time.unscaledTime;
        spinning = true;
        reelSettled[index] = false;
        lastTickCycle[index] = 12 + index * 2;
        resultButtons[index].text = "···";
        resultButtons[index].SetEnabled(false);
        rollButton.SetEnabled(false);
        cashOutButton.SetEnabled(false);
        status.text = $"正在揭晓 · 第 {index + 1} 轮";
        status.style.color = new Color(.74f, .98f, 1f);
        sfx?.PlayRollConfirm();
        sfx?.PlaySpinStart();
    }

    private static ReelRarity RollDemoRarity()
    {
        float roll = UnityEngine.Random.value * 100f;
        // Match the live slot's wave-one Common / Rare / Epic weights.
        return roll < 70f ? ReelRarity.Common : roll < 95f ? ReelRarity.Rare : ReelRarity.Epic;
    }

    private BuffDefinition PickDemoBuff(ref ReelRarity rarity)
    {
        if (demoBuffPool == null) return null;
        for (int downgrade = 0; downgrade < 3; downgrade++)
        {
            var candidates = new List<BuffDefinition>();
            foreach (var buff in demoBuffPool)
                if (buff != null && buff.minWave <= DemoWave && (int)buff.rarity == (int)rarity)
                    candidates.Add(buff);
            if (candidates.Count > 0) return candidates[UnityEngine.Random.Range(0, candidates.Count)];
            if (rarity == ReelRarity.Epic) rarity = ReelRarity.Rare;
            else if (rarity == ReelRarity.Rare) rarity = ReelRarity.Common;
            else break;
        }
        return null;
    }

    private void Update()
    {
        if (!gameplayMode && Application.isPlaying && Input.GetKeyDown(KeyCode.F6)) ReplayAssembly();
        if (!Application.isPlaying || stage == null || !spinning || activeReel < 0) return;
        int reel = activeReel;
        float elapsed = Time.unscaledTime - started;
        float duration = spinDuration;
        float t = Mathf.Clamp01(elapsed / duration);
        float progress = t < .15f
            ? .16f * Mathf.Pow(t / .15f, 2)
            : .16f + .84f * (1 - Mathf.Pow(1 - (t - .15f) / .85f, 3));
        offsets[reel] = (12 + reel * 2) * reelPitch * (1 - progress);
        int currentCycle = Mathf.FloorToInt(offsets[reel] / reelPitch);
        if (currentCycle != lastTickCycle[reel])
        {
            lastTickCycle[reel] = currentCycle;
            sfx?.PlayReelTick(reel);
        }
        RenderReel(reel);
        glow[reel].style.opacity = overlays ? (t >= 1 ? .85f + .15f * Mathf.Sin(elapsed * 12) : .45f) : 0;

        if (t >= 1f && !reelSettled[reel])
        {
            reelSettled[reel] = true;
            sfx?.PlayReelStop(reel);
            if (specialPreset && reel == 2) sfx?.PlaySpecialReveal();
            if (gameplayMode) SetGameplayResult(reel, pendingResult);
            else CommitPendingReel(reel);
        }
        if (elapsed >= duration)
        {
            spinning = false;
            activeReel = -1;
            RefreshInfo();
        }
    }

    private void CommitPendingReel(int index)
    {
        session.reels[index] = pendingResult;
        session.revealedReelCount = index + 1;
        session.revealStage = session.revealedReelCount switch
        {
            1 => SlotRevealStage.Stage1,
            2 => SlotRevealStage.Stage2,
            _ => SlotRevealStage.Stage3
        };
        session.hasSpunOnce = true;
        glyphResults[index] = GlyphForBuff(pendingResult.buff);
        resultButtons[index].text = ReelSummary(pendingResult);
        resultButtons[index].SetEnabled(true);
        RenderReel(index);
        if (session.revealedReelCount == 3)
            SlotMachineBuffRoller.EvaluateSession(session, grantFreeRerolls: true);
    }

    public static int GlyphForBuff(BuffDefinition buff)
    {
        if (buff == null) return 7;
        return buff.effectType switch
        {
            BuffEffectType.FrostMark or BuffEffectType.FrostBurst or BuffEffectType.DeployFrostTower or BuffEffectType.Permafrost => 3,
            BuffEffectType.ElectricCharge or BuffEffectType.ElectricIgniter or BuffEffectType.DeployTeslaCoil or BuffEffectType.ConductiveNetwork => 7,
            BuffEffectType.HeartGuard => 1,
            BuffEffectType.MaxHPUp or BuffEffectType.HealOnKill => 2,
            BuffEffectType.ComboMomentum or BuffEffectType.ComboOverload or BuffEffectType.ComboFrenzy => 4,
            BuffEffectType.BallDamageUp or BuffEffectType.ScoreOnKillUp => 0,
            _ => 6
        };
    }

    private static string ReelSummary(ReelResult reel) => reel.buff == null
        ? "无效果"
        : $"{reel.buff.buffName}\n{GetRarityLabel(reel.rarity)} · 详情";

    private static string GetRarityLabel(ReelRarity rarity) => rarity switch
    {
        ReelRarity.Common => "普通",
        ReelRarity.Rare => "稀有",
        ReelRarity.Epic => "史诗",
        _ => "协议"
    };

    private void RefreshInfo()
    {
        if (gameplayMode) { RefreshOutcomeFeedback(); return; }
        if (session == null) session = NewDemoSession();
        RefreshOutcomeFeedback();
        bankValue.text = localChips.ToString("00");
        int revealed = session.revealedReelCount;
        int nextCost = SlotMachineBuffRoller.GetContinueChipCost(session);
        nextCostValue.text = revealed == 0 ? "免费" : revealed < 3 ? $"{nextCost:00}" : "—";

        if (revealed == 0)
        {
            cashOutButton.style.display = DisplayStyle.None;
            rollButton.text = "PULL · FREE";
            rollButton.SetEnabled(!spinning && !settled);
            status.text = specialPreset ? "测试预设 · 三轮史诗" : "首轮免费 · 揭晓后可兑现或继续";
            return;
        }

        if (revealed < 3)
        {
            cashOutButton.style.display = DisplayStyle.Flex;
            cashOutButton.text = "立即兑现\n收下当前奖励";
            cashOutButton.SetEnabled(!spinning && !settled);
            rollButton.text = revealed == 1 ? "继续转 · 投入 01" : "继续转 · 投入 02";
            rollButton.SetEnabled(!spinning && !settled && localChips >= nextCost);
            status.text = revealed == 1
                ? "第一轮已揭晓 · 可收下或投入 01 枚继续"
                : "第二轮已揭晓 · 最高稀有度兑现 · 续转 02 枚";
        }
        else
        {
            cashOutButton.style.display = DisplayStyle.None;
            rollButton.text = "三轮已完成";
            rollButton.SetEnabled(false);
            status.text = GetComboHeadline(session.combo);
        }

        if (settled)
        {
            rollButton.SetEnabled(false);
            cashOutButton.SetEnabled(false);
            status.text = "预览已兑现 · 正式对局未受影响";
        }
    }

    private void RefreshOutcomeFeedback()
    {
        if (session == null) return;

        var windows = this.windows;
        for (int i = 0; i < 3; i++)
        {
            RemoveOutcomeClasses(resultButtons[i]);
            RemoveOutcomeClasses(outcomeLabels[i]);
            RemoveOutcomeClasses(windows[i]);
            RemoveOutcomeClasses(glow[i]);
            outcomeLabels[i].text = string.Empty;
        }

        int revealed = session.revealedReelCount;
        if (revealed <= 0) return;

        bool finalResult = revealed >= 3;
        var lines = finalResult
            ? SlotMachineBuffRoller.BuildOutcomePreview(session)
            : SlotMachineBuffRoller.BuildCashOutPreview(session);
        var reelKinds = new OutcomeLineKind?[3];
        foreach (var line in lines)
        {
            if (line.reelIndex < 0 || line.reelIndex >= revealed) continue;
            reelKinds[line.reelIndex] = line.kind;
        }

        for (int i = 0; i < revealed && i < 3; i++)
        {
            var reel = session.reels[i];
            string outcomeClass;
            string badge;

            if (reel.isEmptySpin || reel.buff == null)
            {
                outcomeClass = "outcome-empty";
                badge = "— 空转";
            }
            else
            {
                switch (reelKinds[i])
                {
                    case OutcomeLineKind.FullApply:
                        outcomeClass = "outcome-full";
                        badge = finalResult ? "✓ 生效" : settled ? "✓ 已兑现" : "✓ 当前兑现";
                        break;
                    case OutcomeLineKind.PurpleScrap:
                        outcomeClass = "outcome-scrap";
                        badge = "◈ 折现";
                        break;
                    case OutcomeLineKind.MysteryApplied:
                        outcomeClass = "outcome-applied";
                        badge = "✓ 凶兆已生效";
                        break;
                    case OutcomeLineKind.Ignored:
                        outcomeClass = "outcome-ignored";
                        badge = finalResult ? "— 未生效" : "— 未兑现";
                        break;
                    default:
                        outcomeClass = "outcome-empty";
                        badge = "— 无奖励";
                        break;
                }
            }

            AddOutcomeClass(resultButtons[i], outcomeClass);
            AddOutcomeClass(outcomeLabels[i], outcomeClass);
            AddOutcomeClass(windows[i], outcomeClass);
            AddOutcomeClass(glow[i], outcomeClass);
            outcomeLabels[i].text = badge;
        }
    }

    private void RemoveOutcomeClasses(VisualElement element)
    {
        if (element == null) return;
        foreach (string className in OutcomeClasses)
            element.RemoveFromClassList(className);
    }

    private void AddOutcomeClass(VisualElement element, string className)
    {
        if (element == null) return;
        element.AddToClassList(className);
    }

    private static string GetComboHeadline(SlotCombo combo) => combo switch
    {
        SlotCombo.Jackpot => "JACKPOT · 三轮生效 · 轮位税 +1 中 Debuff",
        SlotCombo.TripleRare => "三连稀有 · 中轮生效 · 左右轮折现",
        SlotCombo.DoubleEpic => "双史诗 · 史诗 Buff 生效 · 免费重转 ×1",
        SlotCombo.DoubleRare => "双稀有 · 免费重转 ×1 · 最高稀有度生效",
        SlotCombo.TripleCommon => "三连普通 · 左轮 Buff 生效",
        SlotCombo.Mixed => "杂色组合 · 中轮生效 · 下波史诗权重 +5%",
        SlotCombo.Omen => "凶兆轮结算 · 余轮按降级组合生效",
        SlotCombo.Smooth => "平顺 · 中轮 Buff 生效",
        _ => SlotMachineBuffRoller.GetComboDisplayName(combo)
    };

    private void CashOut()
    {
        if (session == null || session.revealedReelCount <= 0 || session.revealedReelCount >= 3 || spinning) return;
        // Use the live action builder for the preview, without applying it to BuffManager.
        SlotMachineBuffRoller.BuildCashOutActions(session);
        settled = true;
        RefreshInfo();
    }

    public void InspectReel(int index)
    {
        if (session == null || index < 0 || index >= session.revealedReelCount) return;
        BuffDefinition buff = session.reels[index].buff;
        if (buff == null) return;
        inspectName.text = buff.buffName;
        inspectRarity.text = $"{GetRarityLabel(session.reels[index].rarity)}  ·  第 {index + 1} 轮";
        inspectDescription.text = buff.GetBriefDescription();
        inspectOverlay.style.display = DisplayStyle.Flex;
        inspectClose.Focus();
    }

    private void CloseInspect()
    {
        inspectOverlay.style.display = DisplayStyle.None;
        if (session != null && session.revealedReelCount > 0)
            resultButtons[Mathf.Min(session.revealedReelCount - 1, 2)].Focus();
    }

    private void RenderReel(int reel)
    {
        bool visible = session == null || reel < session.revealedReelCount || (spinning && reel == activeReel);
        var unknown = windows[reel].Q("unknownProtocol");
        if (unknown != null) unknown.style.display = visible ? DisplayStyle.None : DisplayStyle.Flex;
        int cycle = Mathf.FloorToInt(offsets[reel] / reelPitch);
        float fraction = offsets[reel] % reelPitch;
        for (int j = 0; j < 7; j++)
        {
            float distanceInPixels = (j - 3) * reelPitch + fraction;
            // Animate toward the pending Buff glyph. Using the previous result here makes the
            // center cell snap to the new result on the final frame when CommitPendingReel runs.
            int baseGlyph = spinning && reel == activeReel
                ? GlyphForBuff(pendingResult.buff)
                : glyphResults[reel];
            int id = ((baseGlyph + j - 3 - cycle) % 8 + 8) % 8;
            Image cell = slots[reel, j];
            cell.sprite = centeredGlyphs[id];
            cell.scaleMode = ScaleMode.ScaleToFit;
            float height = cell.resolvedStyle.height;
            if (float.IsNaN(height) || height <= 0) height = cell.style.height.value.value;
            float reelHeight = windows[reel].resolvedStyle.height;
            if (float.IsNaN(reelHeight) || reelHeight <= 0) reelHeight = windows[reel].style.height.value.value;
            cell.style.top = (reelHeight - height) * .5f + distanceInPixels;
            float distance = Mathf.Clamp01(Mathf.Abs(distanceInPixels) / 220);
            cell.transform.scale = new Vector3(1 - distance * .12f, 1 - distance * .55f, 1);
            cell.style.opacity = visible ? Mathf.Lerp(1, .08f, distance) : 0;
        }
    }

    private void ToggleOverlays()
    {
        overlays = !overlays;
        foreach (VisualElement window in windows)
        {
            window.Q("glassShade").style.display = overlays ? DisplayStyle.Flex : DisplayStyle.None;
            window.Q("selectionGlow").style.display = overlays ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    public void SetGameplaySession(SlotSpinSession liveSession)
    {
        session = liveSession;
        settled = false; spinning = false; activeReel = -1;
        if (inspectOverlay != null) inspectOverlay.style.display = DisplayStyle.None;
        for (int i = 0; i < 3; i++) SetGameplayIdle(i);
        SyncGameplay();
    }

    public void SyncGameplay()
    {
        if (!gameplayMode || session == null) return;
        bankValue.text = Mathf.Max(0, RunSession.Chips).ToString("00");
        int cost = SlotMachineBuffRoller.GetContinueChipCost(session);
        nextCostValue.text = session.revealedReelCount == 0 ? "免费" : session.revealedReelCount < 3 ? cost.ToString("00") : "—";
        RefreshOutcomeFeedback();
    }

    public void SetPresentationVisible(bool visible)
    {
        GetComponent<SlotVisualLabV2Bloom>()?.SetGameplayVisible(visible);
        if (!visible)
        {
            spinning = false; activeReel = -1;
            if (assemblyStart != null) { StopCoroutine(assemblyStart); assemblyStart = null; }
        assemblyTransition?.Dispose();
            if (inspectOverlay != null) inspectOverlay.style.display = DisplayStyle.None;
        }
    }

    public void SetGameplayIdle(int index)
    {
        if (windows[index] == null) return;
        offsets[index] = 0;
        resultButtons[index].text = "未揭晓\n等待抽取";
        resultButtons[index].SetEnabled(false);
        outcomeLabels[index].text = "";
        RemoveOutcomeClasses(windows[index]); RemoveOutcomeClasses(resultButtons[index]); RemoveOutcomeClasses(outcomeLabels[index]);
        windows[index].EnableInClassList("reel-selected", false);
        RenderReel(index);
    }

    public void SetGameplayResult(int index, ReelResult result)
    {
        glyphResults[index] = GlyphForBuff(result.buff);
        offsets[index] = 0;
        resultButtons[index].text = result.isEmptySpin ? "空转" : ReelSummary(result);
        resultButtons[index].SetEnabled(result.buff != null);
        RenderReel(index);
        SyncGameplay();
    }

    public IEnumerator AnimateGameplayReel(int index, ReelResult result, float duration)
    {
        spinDuration = Mathf.Max(.05f, duration);
        pendingResult = result; activeReel = index; started = Time.unscaledTime;
        spinning = true; reelSettled[index] = false; lastTickCycle[index] = 12 + index * 2;
        resultButtons[index].text = "···";
        resultButtons[index].SetEnabled(false);
        sfx?.PlaySpinStart();
        while (spinning && activeReel == index) yield return null;
    }

    public void SetGameplaySelection(int index, bool selected)
    {
        windows[index].EnableInClassList("reel-selected", selected);
    }

    public void SetGameplayHint(int index, bool hint)
    {
        windows[index].EnableInClassList("reel-selectable-hint", hint);
    }

    public void SetGameplayBadge(int index, string text) { outcomeLabels[index].text = text ?? ""; }
    public VisualElement GameplayReel(int index) => windows[index];

    private void ReleaseGlyphs()
    {
        if (centeredGlyphs == null) return;
        foreach (Sprite sprite in centeredGlyphs)
            if (sprite != null)
            {
                if (Application.isPlaying) Destroy(sprite);
                else DestroyImmediate(sprite);
            }
        centeredGlyphs = null;
    }

    public void ResetPreview()
    {
        if (gameplayMode) return;
        spinning = false;
        settled = false;
        activeReel = -1;
        localChips = startingDemoChips;
        session = NewDemoSession();
        glyphResults[0] = 0; glyphResults[1] = 6; glyphResults[2] = 2;
        for (int i = 0; i < 3; i++)
        {
            offsets[i] = 0;
            reelSettled[i] = false;
            resultButtons[i].text = "未揭晓\n等待抽取";
            resultButtons[i].SetEnabled(false);
            outcomeLabels[i].text = string.Empty;
            RemoveOutcomeClasses(resultButtons[i]);
            RemoveOutcomeClasses(outcomeLabels[i]);
            RemoveOutcomeClasses(windows[i]);
            RemoveOutcomeClasses(glow[i]);
            RenderReel(i);
        }
        inspectOverlay.style.display = DisplayStyle.None;
        RefreshInfo();
        if (Application.isPlaying) ReplayAssembly();
    }
}
