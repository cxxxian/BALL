using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class SlotReelView
{
    public const float ViewportHeight = 96f;
    public const float SlotHeight = ViewportHeight;

    private static readonly string[] ReelBorderClasses =
    {
        "reel-border-common",
        "reel-border-rare",
        "reel-border-epic",
        "reel-border-mystery",
        "reel-border-neutral"
    };

    public VisualElement Root { get; }
    public VisualElement Viewport { get; }
    public VisualElement Strip { get; }
    public VisualElement ResultOverlay { get; }
    public Label RarityLabel { get; }
    public Label NameLabel { get; }

    private readonly int _index;

    public SlotReelView(VisualElement root, int index)
    {
        Root = root;
        _index = index;
        Viewport = root.Q<VisualElement>($"reel-viewport-{index}");
        Strip = root.Q<VisualElement>($"reel-strip-{index}");
        ResultOverlay = root.Q<VisualElement>($"reel-result-{index}");
        RarityLabel = root.Q<Label>($"rarity-{index}");
        NameLabel = root.Q<Label>($"name-{index}");

        if (RarityLabel != null)
            RarityLabel.style.display = DisplayStyle.None;
    }

    public void ShowIdleEmpty()
    {
        Viewport.style.display = DisplayStyle.Flex;
        ResultOverlay.style.display = DisplayStyle.None;
        Root.RemoveFromClassList("reel-spinning");
        ClearRarityBorder(Root);
        Root.AddToClassList("reel-border-neutral");
        Strip.Clear();
        AddStripSlot("等待转动", -1, false);
        Strip.style.top = 0f;
    }

    public void SetSelected(bool selected) => Root.EnableInClassList("reel-selected", selected);

    public void SetSelectableHint(bool hint) => Root.EnableInClassList("reel-selectable-hint", hint);

    public void SetOutcomeHighlight(OutcomeLineKind kind, bool canRerollThisReel = false)
    {
        Root.EnableInClassList("reel-outcome-apply", kind == OutcomeLineKind.FullApply || kind == OutcomeLineKind.MysteryApplied);
        Root.EnableInClassList("reel-outcome-scrap", kind == OutcomeLineKind.PurpleScrap);
        Root.EnableInClassList("reel-outcome-ignored", kind == OutcomeLineKind.Ignored && !canRerollThisReel);
        Root.EnableInClassList("reel-outcome-inactive", kind == OutcomeLineKind.Ignored && canRerollThisReel);
    }

    public void ClearOutcomeHighlight()
    {
        Root.RemoveFromClassList("reel-outcome-apply");
        Root.RemoveFromClassList("reel-outcome-scrap");
        Root.RemoveFromClassList("reel-outcome-ignored");
        Root.RemoveFromClassList("reel-outcome-inactive");
    }

    public void SetOutcomeBadge(string text)
    {
        var badge = Root.Q<Label>($"outcome-badge-{_index}") ?? Root.Q<Label>(className: "outcome-badge");
        if (badge == null) return;
        badge.text = text ?? string.Empty;
        badge.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
    }

    public void ShowStaticResult(ReelResult result)
    {
        Root.RemoveFromClassList("reel-spinning");
        Viewport.style.display = DisplayStyle.None;
        ResultOverlay.style.display = DisplayStyle.Flex;
        ApplyResultLabels(result);
    }

    public IEnumerator AnimateToResult(MonoBehaviour host, ReelResult result, float duration)
    {
        Viewport.style.display = DisplayStyle.Flex;
        ResultOverlay.style.display = DisplayStyle.None;
        Root.AddToClassList("reel-spinning");
        BuildSpinStrip(result);

        float targetOffset = (Strip.childCount - 1) * SlotHeight;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float progress = EvaluateDecelCurve(t);
            Strip.style.top = -targetOffset * progress;
            yield return null;
        }

        Strip.style.top = -targetOffset;
        Root.AddToClassList("reel-stop-flash");
        yield return new WaitForSecondsRealtime(0.1f);
        Root.RemoveFromClassList("reel-stop-flash");
        Root.RemoveFromClassList("reel-spinning");
        ShowStaticResult(result);
    }

    /// <summary>前段加速爬升，后段 ease-out 平滑落定，无过冲回弹。</summary>
    private static float EvaluateDecelCurve(float t)
    {
        const float accelPortion = 0.18f;
        if (t <= accelPortion)
        {
            float u = t / accelPortion;
            return 0.22f * (u * u);
        }

        float v = (t - accelPortion) / (1f - accelPortion);
        return 0.22f + 0.78f * (1f - Mathf.Pow(1f - v, 4f));
    }

    private void BuildSpinStrip(ReelResult result)
    {
        Strip.Clear();
        int fakeCount = 12 + _index * 3;
        var pool = BuffManager.Instance != null ? BuffManager.Instance.buffPool : null;

        for (int i = 0; i < fakeCount; i++)
        {
            var fake = PickFakeBuff(pool);
            int r = fake != null ? (int)fake.rarity : Random.Range(0, 3);
            AddStripSlot(fake?.buffName ?? "???", r, false);
        }

        AddStripSlot(
            GetResultTitle(result),
            result.isEmptySpin ? -1 : (int)result.rarity,
            true);
    }

    private void AddStripSlot(string title, int rarityIdx, bool isFinal)
    {
        var slot = new VisualElement();
        slot.AddToClassList("reel-slot");
        if (isFinal) slot.AddToClassList("reel-slot-final");
        ApplyRarityBorder(slot, rarityIdx);

        var nLabel = new Label(title);
        nLabel.AddToClassList("reel-slot-name");
        slot.Add(nLabel);
        Strip.Add(slot);
    }

    private void ApplyResultLabels(ReelResult result)
    {
        if (NameLabel == null) return;

        if (result.isEmptySpin)
        {
            NameLabel.text = "空转";
            Root.EnableInClassList("card-empty", true);
            ApplyRarityBorder(Root, result.rarity == ReelRarity.Mystery ? 3 : -1);
            return;
        }

        Root.EnableInClassList("card-empty", false);
        ApplyRarityBorder(Root, (int)result.rarity);
        NameLabel.text = result.buff != null ? result.buff.buffName : GetResultTitle(result);
    }

    private static void ClearRarityBorder(VisualElement element)
    {
        foreach (var cls in ReelBorderClasses)
            element.RemoveFromClassList(cls);
    }

    private static void ApplyRarityBorder(VisualElement element, int rarityIdx)
    {
        ClearRarityBorder(element);
        if (rarityIdx < 0 || rarityIdx > 3)
        {
            element.AddToClassList("reel-border-neutral");
            return;
        }

        element.AddToClassList(SlotMachineBuffRoller.GetReelBorderClass((ReelRarity)rarityIdx));
    }

    private static BuffDefinition PickFakeBuff(List<BuffDefinition> pool)
    {
        if (pool == null || pool.Count == 0) return null;
        for (int attempt = 0; attempt < 6; attempt++)
        {
            var b = pool[Random.Range(0, pool.Count)];
            if (b != null) return b;
        }
        return null;
    }

    private static string GetResultTitle(ReelResult result)
    {
        if (result.isEmptySpin)
            return "空转";
        if (result.rarity == ReelRarity.Mystery && result.buff == null)
            return "凶兆";
        return result.buff != null ? result.buff.buffName : "???";
    }
}
