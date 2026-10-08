using UnityEngine;

// All existing damage paths call OnHit after committing damage, before checking death.
// This subclass therefore covers ball, phantom, weapons, pulse, electric and frost
// without changing EnemyBase or duplicating its combat side effects.
public sealed class Scene2Boss : Boss
{
    public bool WeakActive { get; private set; }
    private float _multiplier = 1f;
    private float _bonusCredit;
    private int _observedHits;
    private LineRenderer _weakOutline;
    public int WeakBonusDamage { get; private set; }

    public void SetWeak(bool active, float multiplier)
    {
        WeakActive = active;
        GetComponent<Scene2WeaknessReveal>()?.SetExposed(active);
        _multiplier = active ? Mathf.Max(1f, multiplier) : 1f;
        if (active && _weakOutline == null)
        {
            var go = new GameObject("Scene2WeakPoint");
            go.transform.SetParent(transform, false);
            _weakOutline = go.AddComponent<LineRenderer>();
            _weakOutline.useWorldSpace = false;
            _weakOutline.loop = true;
            _weakOutline.positionCount = 4;
            float radius = 1f / Mathf.Max(0.01f, transform.localScale.x);
            _weakOutline.SetPositions(new[] { new Vector3(0f, radius, 0f), new Vector3(radius, 0f, 0f), new Vector3(0f, -radius, 0f), new Vector3(-radius, 0f, 0f) });
            _weakOutline.startWidth = _weakOutline.endWidth = 0.06f;
            _weakOutline.sharedMaterial = MainSR.sharedMaterial;
            _weakOutline.startColor = _weakOutline.endColor = new Color(1f, 0.82f, 0.1f);
            _weakOutline.sortingOrder = 8;
        }
        if (_weakOutline != null) _weakOutline.enabled = active;
    }

    protected override void OnHit()
    {
        int damage = Mathf.Max(0, CurrentHits - _observedHits);
        if (WeakActive && damage > 0)
        {
            _bonusCredit += damage * (_multiplier - 1f);
            int bonus = Mathf.FloorToInt(_bonusCredit + 0.0001f);
            _bonusCredit -= bonus;
            CurrentHits += bonus;
            WeakBonusDamage += bonus;
            if (bonus > 0) GameManager.Instance?.AddScore(scoreOnHit * bonus);
        }
        _observedHits = CurrentHits;
        base.OnHit();
    }
}
