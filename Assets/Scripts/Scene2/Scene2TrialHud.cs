using UnityEngine;
using UnityEngine.UI;

// Small trial-only readout. Existing combat HUD and input remain unchanged.
public sealed class Scene2TrialHud : MonoBehaviour
{
    [SerializeField] private Scene2TrialDirector director;
    [SerializeField] private UnityEngine.UI.Text statusText;
    private float _nextRefresh;
    private void Update()
    {
        if (director == null || statusText == null || Time.unscaledTime < _nextRefresh) return;
        _nextRefresh = Time.unscaledTime + 0.1f;
        string weak = director.WeakSecondsLeft > 0f ? " · " + director.WeakSecondsLeft.ToString("0.0") + "s" : "";
        statusText.text = (director.UsesKeyTarget ? "SCENE 2 · 破局目标 → 弱点爆发\n" :
            "SCENE 2 · 全清编队 → 弱点爆发\n") + director.Status + weak;
        statusText.color = director.WeakSecondsLeft > 0f ? new Color(1f, 0.8f, 0.2f) : new Color(0.5f, 1f, 1f);
    }
}
