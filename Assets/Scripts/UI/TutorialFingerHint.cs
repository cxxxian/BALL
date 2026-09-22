using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>触屏 Ghost Hand：演示一次后消失。</summary>
public class TutorialFingerHint : MonoBehaviour
{
    public static TutorialFingerHint Instance { get; private set; }

    private Canvas _canvas;
    private Text _label;
    private RectTransform _rt;
    private Coroutine _showCo;

    public static bool ShouldShowTouchHints =>
#if UNITY_ANDROID || UNITY_IOS
        true;
#else
        Application.isMobilePlatform;
#endif

    public static TutorialFingerHint Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("TutorialFingerHint");
        return go.AddComponent<TutorialFingerHint>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Build();
        Hide();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Build()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 510;
        gameObject.AddComponent<GraphicRaycaster>().enabled = false;

        var go = new GameObject("Hint", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        _rt = go.GetComponent<RectTransform>();
        _rt.sizeDelta = new Vector2(220f, 80f);

        _label = go.AddComponent<Text>();
        _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _label.fontSize = 28;
        _label.alignment = TextAnchor.MiddleCenter;
        _label.color = new Color(0.78f, 0.96f, 1f, 0.85f);
        _label.text = "👆";
    }

    public void ShowOnceAtScreen(Vector2 screenPos, string message, float holdSeconds = 1.1f)
    {
        if (!ShouldShowTouchHints) return;

        Hide();
        _showCo = StartCoroutine(ShowRoutine(screenPos, message, holdSeconds));
    }

    public void ShowFlipperLeftOnce()
    {
        ShowOnceAtScreen(new Vector2(Screen.width * 0.28f, Screen.height * 0.12f), "按住左挡板");
    }

    public void ShowFlipperRightOnce()
    {
        ShowOnceAtScreen(new Vector2(Screen.width * 0.72f, Screen.height * 0.12f), "按住右挡板");
    }

    private IEnumerator ShowRoutine(Vector2 screenPos, string message, float holdSeconds)
    {
        _rt.position = screenPos;
        _label.text = string.IsNullOrEmpty(message) ? "👆" : $"👆\n{message}";
        _canvas.enabled = true;

        float t = 0f;
        while (t < holdSeconds)
        {
            t += Time.unscaledDeltaTime;
            float a = 0.85f * (1f - t / holdSeconds);
            _label.color = new Color(0.78f, 0.96f, 1f, a);
            yield return null;
        }

        Hide();
    }

    public void Hide()
    {
        TutorialFingerHint self = this;
        if (self == null) return;

        if (_showCo != null)
        {
            StopCoroutine(_showCo);
            _showCo = null;
        }

        if (_canvas != null) _canvas.enabled = false;
    }
}
