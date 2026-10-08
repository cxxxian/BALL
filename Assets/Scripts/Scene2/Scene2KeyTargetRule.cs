using UnityEngine;

public enum Scene2LoopRule { WholeGroup = 0, KeyTarget = 1 }

// A group owns one target and at most one opening. No shared combat code is changed.
public sealed class Scene2KeyTargetRule
{
    private Minion _target;
    private bool _assigned;
    private bool _killed;
    private bool _consumed;
    public Minion Target => _target;
    public bool TargetKilled => _killed;
    public bool TargetMissed => _assigned && !_killed && (_target == null || _target.IsDead);

    public void Reset()
    {
        if (_target != null) _target.onDeath.RemoveListener(OnTargetKilled);
        _target = null;
        _assigned = _killed = _consumed = false;
    }

    public void Assign(Minion target)
    {
        if (_assigned || target == null) return;
        _assigned = true;
        _target = target;
        target.onDeath.AddListener(OnTargetKilled);
        if (target.GetComponent<Scene2KeyTargetMarker>() == null)
            target.gameObject.AddComponent<Scene2KeyTargetMarker>();
    }

    private void OnTargetKilled(EnemyBase enemy)
    {
        // Only combat onDeath qualifies. Bottom exit and system clear never invoke it.
        if (enemy == _target) _killed = true;
    }

    public bool TryConsumeOpening()
    {
        if (!_killed || _consumed) return false;
        _consumed = true;
        return true;
    }

    public static void RefreshRedirect()
    {
        var skills = SkillManager.Instance;
        if (skills == null || skills.slots == null) return;
        for (int i = 0; i < skills.slots.Length; i++)
        {
            var slot = skills.slots[i];
            if (slot == null || slot.definition == null ||
                slot.definition.implementationType != ActiveSkillType.ProtocolRedirect) continue;
            slot.currentCD = 0f;
            skills.onSlotCooldownChanged.Invoke(i, 0f);
        }
    }
}
