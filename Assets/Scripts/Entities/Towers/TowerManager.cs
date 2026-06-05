using UnityEngine;
using System.Collections.Generic;
using System;

public class TowerManager : MonoBehaviour
{
    public static TowerManager Instance { get; private set; }

    private TeslaTower _teslaTower;
    private FrostTower _frostTower;

    private List<Vector3> _availableSlots = new List<Vector3>()
    {
        new Vector3(-2.0f, -5.5f, 0f), 
        new Vector3( 2.0f, -5.5f, 0f), 
        new Vector3( 0.0f, -4.5f, 0f), 
        new Vector3(-2.5f, -3.5f, 0f), 
        new Vector3( 2.5f, -3.5f, 0f)  
    };

    public bool IsPlacementPending { get; private set; }
    private Action _onPlacementDone;
    private int _pendingTowerType = 0; // 1 = Tesla, 2 = Frost

    private List<GameObject> _slotIndicators = new List<GameObject>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void UpdateTowers()
    {
        if (BuffManager.Instance == null) return;

        int teslaLevel = BuffManager.Instance.TeslaCoilLevel;
        if (teslaLevel > 0)
        {
            if (_teslaTower == null)
            {
                IsPlacementPending = true;
                _pendingTowerType = 1;
            }
            else
            {
                _teslaTower.level = teslaLevel;
            }
        }

        int frostLevel = BuffManager.Instance.FrostTowerLevel;
        if (frostLevel > 0)
        {
            if (_frostTower == null && !IsPlacementPending) // If both somehow triggered
            {
                IsPlacementPending = true;
                _pendingTowerType = 2;
            }
            else if (_frostTower != null)
            {
                _frostTower.level = frostLevel;
            }
        }
    }

    public void StartPlacement(Action onDone)
    {
        _onPlacementDone = onDone;
        Time.timeScale = 1f; // We need time to run for visual effects and placement
        ShowSlotIndicators();
    }

    private void ShowSlotIndicators()
    {
        ClearSlotIndicators();
        foreach (var pos in _availableSlots)
        {
            var go = new GameObject("BuildSlot");
            go.transform.position = pos;
            
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CreateSlotSprite();
            sr.color = new Color(0.2f, 0.9f, 0.2f, 0.8f); // Neon Green glow
            sr.material = new Material(Shader.Find("Sprites/Default"));
            sr.sortingOrder = 10;
            
            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.6f;
            col.isTrigger = true;

            _slotIndicators.Add(go);
        }
    }

    private void ClearSlotIndicators()
    {
        foreach (var go in _slotIndicators)
            if (go != null) Destroy(go);
        _slotIndicators.Clear();
    }

    private void Update()
    {
        if (!IsPlacementPending || _slotIndicators.Count == 0) return;

        // Make the slots pulse
        float pulse = 0.8f + Mathf.Sin(Time.time * 6f) * 0.2f;
        foreach (var go in _slotIndicators)
        {
            if (go != null) go.transform.localScale = new Vector3(pulse, pulse, 1f);
        }

        if (Input.GetMouseButtonDown(0))
        {
            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Collider2D hit = Physics2D.OverlapPoint(mousePos);
            if (hit != null && _slotIndicators.Contains(hit.gameObject))
            {
                Vector3 chosenPos = hit.transform.position;
                _availableSlots.Remove(chosenPos);
                ClearSlotIndicators();
                
                BuildPendingTower(chosenPos);
                
                IsPlacementPending = false;
                _pendingTowerType = 0;
                
                _onPlacementDone?.Invoke();
                _onPlacementDone = null;
            }
        }
    }

    private void BuildPendingTower(Vector3 pos)
    {
        if (_pendingTowerType == 1)
        {
            var go = new GameObject("TeslaTower_Built");
            go.transform.position = pos;
            _teslaTower = go.AddComponent<TeslaTower>();
            _teslaTower.level = BuffManager.Instance.TeslaCoilLevel;
            SpawnPlacementEffect(pos, new Color(0.2f, 0.9f, 1f));
        }
        else if (_pendingTowerType == 2)
        {
            var go = new GameObject("FrostTower_Built");
            go.transform.position = pos;
            _frostTower = go.AddComponent<FrostTower>();
            _frostTower.level = BuffManager.Instance.FrostTowerLevel;
            SpawnPlacementEffect(pos, new Color(0.6f, 0.9f, 1f));
        }
    }

    private void SpawnPlacementEffect(Vector3 pos, Color color)
    {
        if (ImpactFX.Instance != null)
        {
            ImpactFX.Instance.SpawnHit(pos, color, 1.5f);
        }
        CameraShake.Instance?.Shake(CameraShake.Preset.Medium);
    }

    private static Sprite CreateSlotSprite()
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
                float dx = (x - half);
                float dy = (y - half);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                if (dist <= r1 && dist >= r2)
                {
                    // Dashed circle
                    float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                    if (angle < 0) angle += 360f;
                    if (angle % 45f < 30f)
                        tex.SetPixel(x, y, Color.white);
                    else
                        tex.SetPixel(x, y, Color.clear);
                }
                else
                {
                    tex.SetPixel(x, y, Color.clear);
                }
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);
    }
}
