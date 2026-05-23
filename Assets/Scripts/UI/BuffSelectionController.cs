using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class BuffSelectionController : MonoBehaviour
{
    private UIDocument  _doc;
    private VisualElement _overlay;

    private Label[]  _rarityLabels = new Label[3];
    private Label[]  _nameLabels   = new Label[3];
    private Label[]  _descLabels   = new Label[3];
    private Button[] _buttons      = new Button[3];

    private BuffDefinition[] _currentSelection;

    private static readonly string[] RarityText  = { "COMMON", "RARE", "EPIC" };
    private static readonly string[] RarityClass = { "rarity-common", "rarity-rare", "rarity-epic" };

    private void Awake()
    {
        _doc = GetComponent<UIDocument>();
    }

    private void Start()
    {
        var root = _doc.rootVisualElement;
        _overlay = root.Q<VisualElement>("overlay");

        for (int i = 0; i < 3; i++)
        {
            _rarityLabels[i] = root.Q<Label>($"rarity-{i}");
            _nameLabels[i]   = root.Q<Label>($"name-{i}");
            _descLabels[i]   = root.Q<Label>($"desc-{i}");
            _buttons[i]      = root.Q<Button>($"btn-{i}");

            int captured = i;
            _buttons[i].clicked += () => OnCardSelected(captured);
        }

        _overlay.style.display = DisplayStyle.None;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.onBuffSelection.AddListener(Show);
            GameManager.Instance.onGameStart.AddListener(Hide);
            GameManager.Instance.onGameOver.AddListener(Hide);
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.onBuffSelection.RemoveListener(Show);
            GameManager.Instance.onGameStart.RemoveListener(Hide);
            GameManager.Instance.onGameOver.RemoveListener(Hide);
        }
    }

    // ── 显示面板 ──────────────────────────────────────────────────────────
    public void Show()
    {
        if (BuffManager.Instance == null) return;

        _currentSelection = BuffManager.Instance.GetRandomSelection(3);

        // 如果 Buff 池不足 3 个，用占位填充
        for (int i = 0; i < 3; i++)
        {
            bool hasCard = i < _currentSelection.Length && _currentSelection[i] != null;
            var card = _overlay.Q<VisualElement>($"card-{i}");

            if (!hasCard)
            {
                if (card != null) card.style.visibility = Visibility.Hidden;
                continue;
            }
            if (card != null) card.style.visibility = Visibility.Visible;

            var def = _currentSelection[i];

            // 稀有度样式
            _rarityLabels[i].text = RarityText[(int)def.rarity];
            _rarityLabels[i].RemoveFromClassList("rarity-common");
            _rarityLabels[i].RemoveFromClassList("rarity-rare");
            _rarityLabels[i].RemoveFromClassList("rarity-epic");
            _rarityLabels[i].AddToClassList(RarityClass[(int)def.rarity]);

            _nameLabels[i].text = def.buffName;
            _descLabels[i].text = def.description;
        }

        _overlay.style.display = DisplayStyle.Flex;
        Time.timeScale = 0f;   // 暂停游戏时间
    }

    // ── 选择某张卡片 ──────────────────────────────────────────────────────
    private void OnCardSelected(int index)
    {
        if (_currentSelection == null || index >= _currentSelection.Length) return;
        var def = _currentSelection[index];
        if (def != null) BuffManager.Instance?.ApplyBuff(def);
        Hide();
    }

    // ── 隐藏面板 ──────────────────────────────────────────────────────────
    private void Hide()
    {
        _overlay.style.display = DisplayStyle.None;
        Time.timeScale = 1f;
        GameManager.Instance?.OnBuffSelectionDone();
    }
}
