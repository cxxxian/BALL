using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class SlotReelView
{
    private readonly SlotVisualLabV2Controller view;
    private readonly int index;
    public VisualElement Root => view.GameplayReel(index);
    public SlotReelView(SlotVisualLabV2Controller view, int index) { this.view = view; this.index = index; Root.pickingMode = PickingMode.Position; }
    public void ShowIdleEmpty() => view.SetGameplayIdle(index);
    public void SetSelected(bool selected) => view.SetGameplaySelection(index, selected);
    public void SetSelectableHint(bool hint) => view.SetGameplayHint(index, hint);
    public void SetOutcomeHighlight(OutcomeLineKind kind, bool canRerollThisReel = false) => view.SyncGameplay();
    public void ClearOutcomeHighlight() { }
    public void SetOutcomeBadge(string text) => view.SetGameplayBadge(index, text);
    public void ShowStaticResult(ReelResult result) => view.SetGameplayResult(index, result);
    public IEnumerator AnimateToResult(MonoBehaviour host, ReelResult result, float duration) => view.AnimateGameplayReel(index, result, duration);
}
