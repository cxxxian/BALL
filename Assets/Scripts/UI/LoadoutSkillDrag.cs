using System;
using UnityEngine;
using UnityEngine.UIElements;

public enum LoadoutSkillDragSource { Library, Slot }

/// <summary>战前配置：技能卡片 / 装备槽拖拽与跟随缩略图。</summary>
public sealed class LoadoutSkillDrag
{
    private readonly VisualElement _overlayRoot;
    private readonly VisualElement _slot0;
    private readonly VisualElement _slot1;

    private RunCatalog _catalog;
    private LoadoutSkillDragSource _sourceKind;
    private int _sourceSlot = -1;
    private string _skillId;
    private VisualElement _ghost;
    private VisualElement _sourceElement;
    private int _pointerId = -1;
    private int _hoverSlot = -1;
    private bool _active;

    public bool IsActive => _active;
    public event Action OnDropApplied;

    public LoadoutSkillDrag(VisualElement overlayRoot, VisualElement slot0, VisualElement slot1)
    {
        _overlayRoot = overlayRoot;
        _slot0 = slot0;
        _slot1 = slot1;
    }

    public void SetCatalog(RunCatalog catalog) => _catalog = catalog;

    public void BindLibraryCard(VisualElement element, SkillDefinition def)
    {
        if (element == null || def == null || !def.isAvailable) return;
        element.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button != 0 || _active) return;
            BeginDrag(element, def, LoadoutSkillDragSource.Library, -1, evt);
            evt.StopPropagation();
        });
    }

    public void BindSlot(VisualElement element, int slotIndex, Func<SkillDefinition> getSkill)
    {
        if (element == null || getSkill == null) return;
        element.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button != 0 || _active) return;
            var def = getSkill();
            if (def == null || !def.isAvailable) return;
            BeginDrag(element, def, LoadoutSkillDragSource.Slot, slotIndex, evt);
            evt.StopPropagation();
        });
    }

    public void Cancel()
    {
        if (!_active) return;
        EndDrag(-1, apply: false);
    }

    private void BeginDrag(VisualElement source, SkillDefinition def, LoadoutSkillDragSource kind, int slotIndex, PointerDownEvent evt)
    {
        _active = true;
        _sourceKind = kind;
        _sourceSlot = slotIndex;
        _skillId = def.skillId;
        _sourceElement = source;
        _pointerId = evt.pointerId;

        source.CapturePointer(_pointerId);
        source.AddToClassList("skill-drag-source");

        _ghost = BuildGhost(def);
        _overlayRoot.Add(_ghost);
        MoveGhost(evt.position);

        source.RegisterCallback<PointerMoveEvent>(OnPointerMove);
        source.RegisterCallback<PointerUpEvent>(OnPointerUp);
    }

    private void OnPointerMove(PointerMoveEvent evt)
    {
        if (!_active || evt.pointerId != _pointerId) return;
        MoveGhost(evt.position);
        UpdateDropHighlight(evt.position);
    }

    private void OnPointerUp(PointerUpEvent evt)
    {
        if (!_active || evt.pointerId != _pointerId) return;
        int dropSlot = ResolveDropSlot(evt.position);
        EndDrag(dropSlot, apply: true);
        evt.StopPropagation();
    }

    private void EndDrag(int dropSlot, bool apply)
    {
        UnregisterDragCallbacks();
        if (_sourceElement != null)
        {
            _sourceElement.RemoveFromClassList("skill-drag-source");
            if (_pointerId >= 0 && _sourceElement.HasPointerCapture(_pointerId))
                _sourceElement.ReleasePointer(_pointerId);
        }

        _ghost?.RemoveFromHierarchy();
        _ghost = null;
        SetSlotDropHighlight(-1);

        bool changed = false;
        if (apply && dropSlot >= 0 && _catalog != null)
            changed = ApplyDrop(dropSlot);

        _active = false;
        _sourceSlot = -1;
        _skillId = null;
        _sourceElement = null;
        _pointerId = -1;
        _hoverSlot = -1;

        if (changed)
            OnDropApplied?.Invoke();
    }

    private bool ApplyDrop(int dropSlot)
    {
        if (string.IsNullOrEmpty(_skillId)) return false;

        if (_sourceKind == LoadoutSkillDragSource.Library)
        {
            var skill = _catalog.GetSkill(_skillId);
            if (skill == null) return false;
            return RunLoadout.TryEquipSkill(skill, dropSlot, _catalog);
        }

        if (_sourceKind == LoadoutSkillDragSource.Slot && _sourceSlot >= 0)
        {
            if (_sourceSlot == dropSlot) return false;
            RunLoadout.MoveSkillBetweenSlots(_sourceSlot, dropSlot);
            return true;
        }

        return false;
    }

    private void MoveGhost(Vector2 screenPos)
    {
        if (_ghost == null || _overlayRoot == null) return;
        var local = _overlayRoot.WorldToLocal(screenPos);
        const float w = 168f;
        const float h = 92f;
        _ghost.style.left = local.x - w * 0.5f;
        _ghost.style.top  = local.y - h * 0.5f;
    }

    private void UpdateDropHighlight(Vector2 screenPos)
    {
        int slot = ResolveDropSlot(screenPos);
        if (slot == _hoverSlot) return;
        _hoverSlot = slot;
        SetSlotDropHighlight(slot);
    }

    private void UnregisterDragCallbacks()
    {
        if (_sourceElement == null) return;
        _sourceElement.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
        _sourceElement.UnregisterCallback<PointerUpEvent>(OnPointerUp);
    }

    private int ResolveDropSlot(Vector2 panelPos)
    {
        if (SlotContainsPoint(_slot0, panelPos)) return 0;
        if (SlotContainsPoint(_slot1, panelPos)) return 1;

        var panel = _overlayRoot?.panel;
        if (panel == null) return -1;

        var picked = panel.Pick(panelPos);
        while (picked != null)
        {
            if (picked == _slot0) return 0;
            if (picked == _slot1) return 1;
            picked = picked.parent;
        }

        return -1;
    }

    private static bool SlotContainsPoint(VisualElement slot, Vector2 panelPos)
    {
        if (slot == null || slot.resolvedStyle.display == DisplayStyle.None) return false;
        return slot.worldBound.Contains(panelPos);
    }

    private void SetSlotDropHighlight(int slot)
    {
        _slot0?.EnableInClassList("skill-slot-drop-hover", slot == 0);
        _slot1?.EnableInClassList("skill-slot-drop-hover", slot == 1);
    }

    private static VisualElement BuildGhost(SkillDefinition def)
    {
        var ghost = new VisualElement();
        ghost.AddToClassList("skill-drag-ghost");
        ghost.pickingMode = PickingMode.Ignore;

        var stripe = new VisualElement();
        stripe.AddToClassList("skill-drag-ghost-stripe");
        stripe.style.backgroundColor = GetCategoryColor(def.category);
        ghost.Add(stripe);

        var name = new Label(def.displayName);
        name.AddToClassList("skill-drag-ghost-name");
        ghost.Add(name);

        var meta = new Label($"CD {def.baseCooldown:F0}s");
        meta.AddToClassList("skill-drag-ghost-meta");
        ghost.Add(meta);

        return ghost;
    }

    private static Color GetCategoryColor(SkillCategory category) => category switch
    {
        SkillCategory.Defense => new Color(0.2f, 0.85f, 1f),
        SkillCategory.Control => new Color(0.75f, 0.45f, 1f),
        _ => new Color(1f, 0.35f, 0.45f)
    };
}
