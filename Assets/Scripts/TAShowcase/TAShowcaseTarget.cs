using UnityEngine;

// Presentation specimen: uses production status/visual components without gameplay movement.
public sealed class TAShowcaseTarget : EnemyBase
{
    protected override void Awake()
    {
        base.Awake();
        checkBottomLine = false;
        moveSpeed = 1f; // Allows the real short-freeze state to be demonstrated.
        maxHits = 9999;
        // These production state classes are created at runtime in gameplay too.
        if (!TryGetComponent<EnemyFrostState>(out _)) gameObject.AddComponent<EnemyFrostState>();
        if (!TryGetComponent<EnemyElectricState>(out _)) gameObject.AddComponent<EnemyElectricState>();
    }
    protected override void FixedUpdate() { }
    protected override void LateUpdate() { }
}
