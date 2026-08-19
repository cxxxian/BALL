using UnityEngine;
using UnityEngine.UI;

/// <summary>战斗 HUD 右上角暂停钮，与 Esc 共用 PauseMenuController。</summary>
public class PauseHudButton : MonoBehaviour
{
    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
        if (_button != null)
            _button.onClick.AddListener(OnClicked);
    }

    private void Start()
    {
        UpdateVisibility();
        if (GameManager.Instance != null)
        {
            GameManager.Instance.onGameStart.AddListener(UpdateVisibility);
            GameManager.Instance.onGameOver.AddListener(UpdateVisibility);
            GameManager.Instance.onBuffSelection.AddListener(HideForOverlay);
        }
    }

    private void OnDestroy()
    {
        if (_button != null)
            _button.onClick.RemoveListener(OnClicked);
        if (GameManager.Instance != null)
        {
            GameManager.Instance.onGameStart.RemoveListener(UpdateVisibility);
            GameManager.Instance.onGameOver.RemoveListener(UpdateVisibility);
            GameManager.Instance.onBuffSelection.RemoveListener(HideForOverlay);
        }
    }

    private void Update()
    {
        if (_button == null) return;
        bool show = GameManager.Instance != null && GameManager.Instance.IsWaveSimActive();
        if (PauseMenuController.Instance != null && PauseMenuController.Instance.IsOpen)
            show = true;
        if (gameObject.activeSelf != show)
            gameObject.SetActive(show);
    }

    private void UpdateVisibility() => Update();

    private void HideForOverlay() => gameObject.SetActive(false);

    private void OnClicked()
    {
        PauseMenuController.Instance?.Toggle();
    }
}
