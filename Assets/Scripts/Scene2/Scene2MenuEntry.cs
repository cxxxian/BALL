using UnityEngine;
using UnityEngine.SceneManagement;

// Optional menu entry, installed at runtime; MainMenu scene and controller stay intact.
public sealed class Scene2MenuEntry : MonoBehaviour
{
    private MainMenuCanvasView _view;
    private Scene2Config _config;
    private GUIStyle _style;
    private void Start()
    {
        _view = FindObjectOfType<MainMenuCanvasView>();
        _config = Resources.Load<Scene2Config>("Scene2/TrialConfig");
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "MainMenu" && FindObjectOfType<Scene2MenuEntry>() == null)
            new GameObject("Scene2TrialEntry").AddComponent<Scene2MenuEntry>();
    }
    private void OnGUI()
    {
        if (_view == null || !_view.Visible) return;
        if (_style == null)
        {
            _style = new GUIStyle(GUI.skin.button) { fontSize = 18 };
            if (_config != null) _style.font = _config.font;
        }
        if (GUI.Button(new Rect(Screen.width - 270f, Screen.height - 74f, 245f, 48f), "Scene2 · 循环对比试玩", _style))
        {
            Time.timeScale = 1f;
            RunSession.BeginEndless();
            SceneManager.LoadScene("SampleScene2");
        }
    }
}
