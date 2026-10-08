using UnityEngine;

// Shape-based target identification, independent of enemy faction/build colors.
[RequireComponent(typeof(Minion))]
[DisallowMultipleComponent]
public sealed class Scene2KeyTargetMarker : MonoBehaviour
{
    private Minion _enemy;
    private Transform _root;
    private LineRenderer _outline;

    private void Awake()
    {
        _enemy = GetComponent<Minion>();
        _root = new GameObject("Scene2KeyTargetDiamond").transform;
        _root.SetParent(transform, false);
        _outline = _root.gameObject.AddComponent<LineRenderer>();
        _outline.useWorldSpace = false;
        _outline.loop = true;
        _outline.positionCount = 4;
        _outline.SetPositions(new[] { new Vector3(0f, 0.72f, 0f), new Vector3(0.72f, 0f, 0f),
            new Vector3(0f, -0.72f, 0f), new Vector3(-0.72f, 0f, 0f) });
        _outline.startWidth = _outline.endWidth = 0.045f;
        _outline.sharedMaterial = CyberVisualFactory.UnlitMaterial;
        _outline.startColor = _outline.endColor = new Color(1f, 0.78f, 0.16f);
        _outline.sortingOrder = 12;
        SyncScale();
    }

    private void LateUpdate()
    {
        _outline.enabled = _enemy != null && !_enemy.IsDead && !_enemy.FrozenForWaveClear;
        SyncScale();
    }

    private void SyncScale()
    {
        Vector3 scale = transform.lossyScale;
        _root.localScale = new Vector3(1f / Mathf.Max(0.001f, Mathf.Abs(scale.x)),
            1f / Mathf.Max(0.001f, Mathf.Abs(scale.y)), 1f);
    }
}
