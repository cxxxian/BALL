using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class TowerLevelDisplay : MonoBehaviour
{
    private const float CanvasWorldScale = 0.015f;
    private static readonly Vector2 CanvasPixelSize = new Vector2(64f, 20f);
    private const int FontSize = 17;
    private const float YOffset = 0.42f;

    private Text _label;

    public void SetLevel(int level)
    {
        EnsureUI(level);
        if (_label != null)
            _label.text = $"Lv.{level}";
    }

    public static TowerLevelDisplay Attach(GameObject tower, int level)
    {
        var display = tower.GetComponent<TowerLevelDisplay>();
        if (display == null)
            display = tower.AddComponent<TowerLevelDisplay>();
        display.EnsureUI(level, forceRebuild: true);
        display.SetLevel(level);
        return display;
    }

    private void Start()
    {
        var canvas = transform.Find("LevelLabelCanvas");
        if (canvas != null && canvas.localScale.x > 0.02f)
        {
            int level = ReadTowerLevel();
            EnsureUI(level, forceRebuild: true);
            SetLevel(level);
        }
    }

    private int ReadTowerLevel()
    {
        if (TryGetComponent(out TeslaTower tesla)) return tesla.level;
        if (TryGetComponent(out FrostTower frost)) return frost.level;
        return 1;
    }

    private void EnsureUI(int level, bool forceRebuild = false)
    {
        var existingCanvas = transform.Find("LevelLabelCanvas");
        if (existingCanvas != null)
        {
            bool isCurrentFormat = existingCanvas.localScale.x <= 0.02f
                && _label != null
                && _label.transform.IsChildOf(existingCanvas);

            if (!forceRebuild && isCurrentFormat)
                return;

            DestroyCanvas(existingCanvas.gameObject);
            _label = null;
        }

        BuildUI(level);
    }

    private static void DestroyCanvas(GameObject canvasObj)
    {
        if (Application.isPlaying)
            Object.Destroy(canvasObj);
        else
            Object.DestroyImmediate(canvasObj);
    }

    private void BuildUI(int level)
    {
        var canvasObj = new GameObject("LevelLabelCanvas");
        canvasObj.transform.SetParent(transform, false);
        canvasObj.transform.localPosition = new Vector3(0f, YOffset, 0f);
        canvasObj.transform.localScale = Vector3.one * CanvasWorldScale;

        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 55;

        var rootRect = canvasObj.GetComponent<RectTransform>();
        rootRect.sizeDelta = CanvasPixelSize;

        var textObj = new GameObject("LevelText");
        textObj.transform.SetParent(canvasObj.transform, false);

        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        _label = textObj.AddComponent<Text>();
        _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _label.fontSize = FontSize;
        _label.alignment = TextAnchor.MiddleCenter;
        _label.color = new Color(1f, 0.95f, 0.2f, 1f);
        _label.horizontalOverflow = HorizontalWrapMode.Overflow;
        _label.verticalOverflow = VerticalWrapMode.Overflow;
        _label.raycastTarget = false;

        var outline = textObj.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(1f, -1f);

        _label.text = $"Lv.{level}";
    }
}
