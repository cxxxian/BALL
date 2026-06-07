using System;
using System.Collections.Generic;
using UnityEngine;

public class TowerManager : MonoBehaviour
{
    public static TowerManager Instance { get; private set; }

    [Flags]
    public enum TowerUseActions
    {
        None = 0,
        BuildOrReplace = 1 << 0,
        Upgrade = 1 << 1
    }

    private class PlacedTower
    {
        public BuffEffectType type;
        public GameObject instance;
        public int level;
    }

    private readonly Vector3[] _slotPositions =
    {
        new Vector3(-2.0f, -5.5f, 0f),
        new Vector3( 2.0f, -5.5f, 0f),
        new Vector3( 0.0f, -4.5f, 0f),
        new Vector3(-2.5f, -3.5f, 0f),
        new Vector3( 2.5f, -3.5f, 0f)
    };

    private readonly PlacedTower[] _slots = new PlacedTower[5];

    private bool _placementActive;
    private bool _upgradeActive;
    private bool _replaceMode;
    private BuffEffectType _pendingType;
    private Action _onInteractionDone;
    private readonly List<GameObject> _slotIndicators = new List<GameObject>();
    private readonly List<GameObject> _upgradeIndicators = new List<GameObject>();

    public bool HasFreeSlot
    {
        get
        {
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i] == null) return true;
            return false;
        }
    }

    public int PlacedTowerCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i] != null) count++;
            return count;
        }
    }

    public bool IsPlacementPending => _placementActive;
    public bool IsUpgradePending => _upgradeActive;
    public bool IsTowerInteractionPending => _placementActive || _upgradeActive;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.onGameStart.AddListener(ResetForNewGame);
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.onGameStart.RemoveListener(ResetForNewGame);
    }

    public bool HasTowerOfType(BuffEffectType type)
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            var slot = _slots[i];
            if (slot != null && slot.type == type && slot.instance != null)
                return true;
        }
        return false;
    }

    public TowerUseActions GetAvailableActions(BuffEffectType type)
    {
        var actions = TowerUseActions.BuildOrReplace;
        if (HasTowerOfType(type))
            actions |= TowerUseActions.Upgrade;
        return actions;
    }

    public void BeginBuildOrReplace(BuffEffectType type, Action onDone)
    {
        CancelActiveInteraction();
        _pendingType = type;
        _onInteractionDone = onDone;
        _replaceMode = !HasFreeSlot;
        _placementActive = true;
        Time.timeScale = 1f;
        ShowSlotIndicators();

        if (_slotIndicators.Count == 0)
        {
            _placementActive = false;
            _pendingType = 0;
            CompleteInteraction();
        }
    }

    public void BeginSelectTowerToUpgrade(BuffEffectType type, Action onDone)
    {
        CancelActiveInteraction();
        _pendingType = type;
        _onInteractionDone = onDone;

        if (!HasTowerOfType(type))
        {
            _pendingType = 0;
            CompleteInteraction();
            return;
        }

        _upgradeActive = true;
        Time.timeScale = 1f;
        ShowUpgradeIndicators();

        if (_upgradeIndicators.Count == 0)
        {
            _upgradeActive = false;
            _pendingType = 0;
            CompleteInteraction();
        }
    }

    private void Update()
    {
        if (_placementActive)
            UpdatePlacementInput();
        else if (_upgradeActive)
            UpdateUpgradeInput();
    }

    private void UpdatePlacementInput()
    {
        if (_slotIndicators.Count == 0) return;

        float pulse = 0.8f + Mathf.Sin(Time.unscaledTime * 6f) * 0.2f;
        foreach (var go in _slotIndicators)
        {
            if (go != null) go.transform.localScale = new Vector3(pulse, pulse, 1f);
        }

        if (!Input.GetMouseButtonDown(0)) return;

        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Collider2D hit = Physics2D.OverlapPoint(mousePos);
        if (hit == null || !_slotIndicators.Contains(hit.gameObject)) return;

        int slotIndex = hit.gameObject.GetComponent<BuildSlotMarker>()?.slotIndex ?? -1;
        if (slotIndex < 0) return;

        PlaceTowerAtSlot(slotIndex, _pendingType);
        EndPlacement();
    }

    private void UpdateUpgradeInput()
    {
        if (_upgradeIndicators.Count == 0) return;

        float pulse = 0.85f + Mathf.Sin(Time.unscaledTime * 8f) * 0.15f;
        foreach (var go in _upgradeIndicators)
        {
            if (go != null) go.transform.localScale = new Vector3(pulse, pulse, 1f);
        }

        if (!Input.GetMouseButtonDown(0)) return;

        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Collider2D hit = Physics2D.OverlapPoint(mousePos);
        if (hit == null || !_upgradeIndicators.Contains(hit.gameObject)) return;

        int slotIndex = hit.gameObject.GetComponent<TowerUpgradeMarker>()?.slotIndex ?? -1;
        if (slotIndex < 0) return;

        UpgradeTowerAtSlot(slotIndex);
    }

    private void PlaceTowerAtSlot(int slotIndex, BuffEffectType type)
    {
        if (slotIndex < 0 || slotIndex >= _slots.Length) return;

        var existing = _slots[slotIndex];
        if (existing?.instance != null)
            Destroy(existing.instance);

        Vector3 pos = _slotPositions[slotIndex];
        GameObject go;
        const int level = 1;

        if (type == BuffEffectType.DeployTeslaCoil)
        {
            go = new GameObject("TeslaTower_Built");
            go.transform.position = pos;
            var tower = go.AddComponent<TeslaTower>();
            tower.level = level;
            SpawnPlacementEffect(pos, new Color(0.2f, 0.9f, 1f));
        }
        else
        {
            go = new GameObject("FrostTower_Built");
            go.transform.position = pos;
            var tower = go.AddComponent<FrostTower>();
            tower.level = level;
            SpawnPlacementEffect(pos, new Color(0.6f, 0.9f, 1f));
        }

        TowerLevelDisplay.Attach(go, level);
        _slots[slotIndex] = new PlacedTower { type = type, instance = go, level = level };
    }

    private void UpgradeTowerAtSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _slots.Length) return;

        var slot = _slots[slotIndex];
        if (slot == null || slot.type != _pendingType || slot.instance == null) return;

        slot.level++;

        if (slot.instance.TryGetComponent(out TeslaTower tesla))
            tesla.level = slot.level;
        else if (slot.instance.TryGetComponent(out FrostTower frost))
            frost.level = slot.level;

        var display = slot.instance.GetComponent<TowerLevelDisplay>();
        if (display != null)
            display.SetLevel(slot.level);
        else
            TowerLevelDisplay.Attach(slot.instance, slot.level);

        SpawnUpgradeEffect(slot.instance.transform.position);
        EndUpgradeSelection();
    }

    private void EndPlacement()
    {
        ClearSlotIndicators();
        _placementActive = false;
        _pendingType = 0;
        CompleteInteraction();
    }

    private void EndUpgradeSelection()
    {
        ClearUpgradeIndicators();
        _upgradeActive = false;
        _pendingType = 0;
        CompleteInteraction();
    }

    private void CompleteInteraction()
    {
        _onInteractionDone?.Invoke();
        _onInteractionDone = null;
    }

    private void CancelActiveInteraction()
    {
        ClearSlotIndicators();
        ClearUpgradeIndicators();
        _placementActive = false;
        _upgradeActive = false;
        _pendingType = 0;
        _onInteractionDone = null;
    }

    private void ShowSlotIndicators()
    {
        ClearSlotIndicators();

        for (int i = 0; i < _slotPositions.Length; i++)
        {
            bool occupied = _slots[i] != null;
            if (!_replaceMode && occupied) continue;
            if (_replaceMode && !occupied) continue;

            var go = new GameObject($"BuildSlot_{i}");
            go.transform.position = _slotPositions[i];

            var marker = go.AddComponent<BuildSlotMarker>();
            marker.slotIndex = i;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CreateRingSprite();
            sr.color = _replaceMode
                ? new Color(1f, 0.45f, 0.1f, 0.85f)
                : new Color(0.2f, 0.9f, 0.2f, 0.8f);
            sr.material = new Material(Shader.Find("Sprites/Default"));
            sr.sortingOrder = 10;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.6f;
            col.isTrigger = true;

            _slotIndicators.Add(go);
        }
    }

    private void ShowUpgradeIndicators()
    {
        ClearUpgradeIndicators();

        for (int i = 0; i < _slots.Length; i++)
        {
            var slot = _slots[i];
            if (slot == null || slot.type != _pendingType || slot.instance == null) continue;

            var go = new GameObject($"UpgradeSlot_{i}");
            go.transform.position = slot.instance.transform.position;

            var marker = go.AddComponent<TowerUpgradeMarker>();
            marker.slotIndex = i;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CreateRingSprite();
            sr.color = new Color(1f, 0.92f, 0.15f, 0.9f);
            sr.material = new Material(Shader.Find("Sprites/Default"));
            sr.sortingOrder = 11;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.75f;
            col.isTrigger = true;

            _upgradeIndicators.Add(go);
        }
    }

    private void ClearSlotIndicators()
    {
        foreach (var go in _slotIndicators)
            if (go != null) Destroy(go);
        _slotIndicators.Clear();
    }

    private void ClearUpgradeIndicators()
    {
        foreach (var go in _upgradeIndicators)
            if (go != null) Destroy(go);
        _upgradeIndicators.Clear();
    }

    private void SpawnPlacementEffect(Vector3 pos, Color color)
    {
        ImpactFX.Instance?.SpawnHit(pos, color, 1.5f);
        CameraShake.Instance?.Shake(CameraShake.Preset.Medium);
    }

    private void SpawnUpgradeEffect(Vector3 pos)
    {
        ImpactFX.Instance?.SpawnHit(pos, new Color(1f, 0.92f, 0.2f, 1f), 1.2f);
        CameraShake.Instance?.Shake(CameraShake.Preset.Light);
    }

    private void ResetForNewGame()
    {
        CancelActiveInteraction();
        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i]?.instance != null)
                Destroy(_slots[i].instance);
            _slots[i] = null;
        }
    }

    private static Sprite CreateRingSprite()
    {
        int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        float half = size * 0.5f;
        float r1 = half - 2f;
        float r2 = half - 6f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - half;
                float dy = y - half;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                if (dist <= r1 && dist >= r2)
                {
                    float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                    if (angle < 0) angle += 360f;
                    tex.SetPixel(x, y, angle % 45f < 30f ? Color.white : Color.clear);
                }
                else tex.SetPixel(x, y, Color.clear);
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);
    }
}
