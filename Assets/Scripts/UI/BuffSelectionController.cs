using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class BuffSelectionController : MonoBehaviour
{
    private UIDocument  _doc;
    private VisualElement _overlay;
    private VisualElement _cardsContainer;
    private VisualElement _towerChoice;
    private Label _towerChoiceHint;
    private Button _towerBtnUpgrade;
    private Button _towerBtnPlace;

    private Label[]  _rarityLabels = new Label[3];
    private Label[]  _nameLabels   = new Label[3];
    private Label[]  _descLabels   = new Label[3];
    private Button[] _buttons      = new Button[3];

    private BuffDefinition[] _currentSelection;
    private BuffDefinition _pendingTowerBuff;

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
        _cardsContainer = root.Q<VisualElement>("cards-container");
        _towerChoice = root.Q<VisualElement>("tower-choice");
        _towerChoiceHint = root.Q<Label>("tower-choice-hint");
        _towerBtnUpgrade = root.Q<Button>("tower-btn-upgrade");
        _towerBtnPlace = root.Q<Button>("tower-btn-place");

        for (int i = 0; i < 3; i++)
        {
            _rarityLabels[i] = root.Q<Label>($"rarity-{i}");
            _nameLabels[i]   = root.Q<Label>($"name-{i}");
            _descLabels[i]   = root.Q<Label>($"desc-{i}");
            _buttons[i]      = root.Q<Button>($"btn-{i}");

            int captured = i;
            _buttons[i].clicked += () => OnCardSelected(captured);
        }

        _towerBtnUpgrade?.RegisterCallback<ClickEvent>(_ => OnTowerUpgradeChosen());
        _towerBtnPlace?.RegisterCallback<ClickEvent>(_ => OnTowerPlaceChosen());

        _overlay.style.display = DisplayStyle.None;
        HideTowerChoice();

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

    public void Show()
    {
        if (BuffManager.Instance == null) return;

        HideTowerChoice();
        _pendingTowerBuff = null;
        _currentSelection = BuffManager.Instance.GetRandomSelection(3);

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

            _rarityLabels[i].text = RarityText[(int)def.rarity];
            _rarityLabels[i].RemoveFromClassList("rarity-common");
            _rarityLabels[i].RemoveFromClassList("rarity-rare");
            _rarityLabels[i].RemoveFromClassList("rarity-epic");
            _rarityLabels[i].AddToClassList(RarityClass[(int)def.rarity]);

            _nameLabels[i].text = def.buffName;
            _descLabels[i].text = def.description;
        }

        _cardsContainer.style.display = DisplayStyle.Flex;
        _overlay.style.display = DisplayStyle.Flex;
        Time.timeScale = 0f;
    }

    private void OnCardSelected(int index)
    {
        if (_currentSelection == null || index >= _currentSelection.Length) return;
        var def = _currentSelection[index];
        if (def == null) return;

        BuffManager.Instance?.ApplyBuff(def);

        if (IsTowerBuff(def))
        {
            _pendingTowerBuff = def;
            var tm = TowerManager.Instance;
            if (tm != null && tm.HasTowerOfType(def.effectType))
            {
                ShowTowerChoice(def);
                return;
            }

            BeginBuildOrReplaceFlow();
            return;
        }

        Hide();
    }

    private void ShowTowerChoice(BuffDefinition def)
    {
        _cardsContainer.style.display = DisplayStyle.None;

        bool hasFree = TowerManager.Instance != null && TowerManager.Instance.HasFreeSlot;
        if (_towerChoiceHint != null)
        {
            _towerChoiceHint.text = hasFree
                ? "选择放置一座新塔，或升级已有同类型塔。"
                : "场地已满：选择替换某座塔，或升级已有同类型塔。";
        }

        if (_towerBtnPlace != null)
            _towerBtnPlace.text = hasFree ? "放置新塔" : "替换塔";

        if (_towerBtnUpgrade != null)
            _towerBtnUpgrade.style.display = DisplayStyle.Flex;

        if (_towerChoice != null)
            _towerChoice.style.display = DisplayStyle.Flex;
    }

    private void HideTowerChoice()
    {
        if (_towerChoice != null)
            _towerChoice.style.display = DisplayStyle.None;
        if (_cardsContainer != null)
            _cardsContainer.style.display = DisplayStyle.Flex;
    }

    private void OnTowerUpgradeChosen()
    {
        if (_pendingTowerBuff == null || TowerManager.Instance == null)
        {
            FinishTowerFlow();
            return;
        }

        var type = _pendingTowerBuff.effectType;
        _pendingTowerBuff = null;
        _overlay.style.display = DisplayStyle.None;
        HideTowerChoice();
        Time.timeScale = 1f;

        TowerManager.Instance.BeginSelectTowerToUpgrade(type, FinishTowerFlow);
    }

    private void OnTowerPlaceChosen()
    {
        BeginBuildOrReplaceFlow();
    }

    private void BeginBuildOrReplaceFlow()
    {
        if (_pendingTowerBuff == null || TowerManager.Instance == null)
        {
            FinishTowerFlow();
            return;
        }

        var type = _pendingTowerBuff.effectType;
        _pendingTowerBuff = null;
        _overlay.style.display = DisplayStyle.None;
        HideTowerChoice();
        Time.timeScale = 1f;

        TowerManager.Instance.BeginBuildOrReplace(type, FinishTowerFlow);
    }

    private void FinishTowerFlow()
    {
        _pendingTowerBuff = null;
        Hide();
    }

    private static bool IsTowerBuff(BuffDefinition def) =>
        def.effectType == BuffEffectType.DeployTeslaCoil ||
        def.effectType == BuffEffectType.DeployFrostTower;

    private void Hide()
    {
        HideTowerChoice();
        _pendingTowerBuff = null;
        _overlay.style.display = DisplayStyle.None;
        Time.timeScale = 1f;
        GameManager.Instance?.OnBuffSelectionDone();
    }
}
