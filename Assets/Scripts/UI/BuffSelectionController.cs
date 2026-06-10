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
    private VisualElement[] _cards = new VisualElement[3];
    private int[]    _cardRarities = { -1, -1, -1 };

    private BuffDefinition[] _currentSelection;
    private BuffDefinition _pendingTowerBuff;

    private float _borderPulseT;

    private static readonly string[] RarityText      = { "COMMON", "RARE", "EPIC" };
    private static readonly string[] RarityClass     = { "rarity-common", "rarity-rare", "rarity-epic" };
    private static readonly string[] CardRarityClass = { "card-rarity-common", "card-rarity-rare", "card-rarity-epic" };
    private static readonly Color EpicBorderDim   = new Color(0.55f, 0f, 0.28f, 0.55f);
    private static readonly Color EpicBorderBright = new Color(1f, 0.35f, 0.82f, 1f);

    private bool _overlayVisible;

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
            _cards[i]        = GetCardElement(i);
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

    private void Update()
    {
        if (!_overlayVisible) return;

        _borderPulseT += Time.unscaledDeltaTime;
        float wave = Mathf.Sin(_borderPulseT * 3.2f) * 0.5f + 0.5f;

        for (int i = 0; i < 3; i++)
        {
            if (_cards[i] == null || _cardRarities[i] != (int)BuffRarity.Epic) continue;

            float t = wave * wave;
            float scale = Mathf.Lerp(1f, 1.03f, t);
            SetCardBorderPulse(_cards[i], Color.Lerp(EpicBorderDim, EpicBorderBright, t), Mathf.Lerp(2f, 4.5f, t), scale);
        }
    }

    private static void SetCardBorderPulse(VisualElement card, Color color, float width, float scale)
    {
        var c = new StyleColor(color);
        card.style.borderTopColor    = c;
        card.style.borderRightColor  = c;
        card.style.borderBottomColor = c;
        card.style.borderLeftColor   = c;
        card.style.borderTopWidth    = width;
        card.style.borderRightWidth  = width;
        card.style.borderBottomWidth = width;
        card.style.borderLeftWidth   = width;
        card.style.scale             = new StyleScale(new Scale(new Vector3(scale, scale, 1f)));
    }

    public void Show()
    {
        if (BuffManager.Instance == null) return;

        HideTowerChoice();
        _pendingTowerBuff = null;
        _currentSelection = BuffManager.Instance.GetRandomSelection(3);
        _borderPulseT = 0f;

        for (int i = 0; i < 3; i++)
        {
            var card = _cards[i] ?? GetCardElement(i);
            var def  = i < _currentSelection.Length ? _currentSelection[i] : null;
            bool isEmpty = def == null;
            _cardRarities[i] = isEmpty ? -1 : (int)def.rarity;

            if (card != null)
            {
                card.style.display = DisplayStyle.Flex;
                card.style.visibility = Visibility.Visible;
                card.EnableInClassList("card-empty", isEmpty);
                ApplyCardRarityClass(card, isEmpty ? -1 : (int)def.rarity);
            }

            if (_buttons[i] != null)
            {
                _buttons[i].SetEnabled(!isEmpty);
                _buttons[i].text = isEmpty ? "—" : "SELECT";
            }

            if (_rarityLabels[i] == null || _nameLabels[i] == null || _descLabels[i] == null)
                continue;

            if (isEmpty)
            {
                SetEmptySlotLabels(i);
                continue;
            }

            int rarityIdx = Mathf.Clamp((int)def.rarity, 0, RarityText.Length - 1);
            _rarityLabels[i].text = RarityText[rarityIdx];
            _rarityLabels[i].RemoveFromClassList("rarity-common");
            _rarityLabels[i].RemoveFromClassList("rarity-rare");
            _rarityLabels[i].RemoveFromClassList("rarity-epic");
            _rarityLabels[i].AddToClassList(RarityClass[rarityIdx]);
            _rarityLabels[i].style.display = DisplayStyle.Flex;

            _nameLabels[i].text = def.buffName;
            _descLabels[i].text = def.description;
        }

        _cardsContainer.style.display = DisplayStyle.Flex;
        _overlay.style.display = DisplayStyle.Flex;
        _overlayVisible = true;
        Time.timeScale = 0f;
    }

    private void ApplyCardRarityClass(VisualElement card, int rarityIdx)
    {
        for (int r = 0; r < CardRarityClass.Length; r++)
            card.RemoveFromClassList(CardRarityClass[r]);

        if (rarityIdx >= 0 && rarityIdx < CardRarityClass.Length)
            card.AddToClassList(CardRarityClass[rarityIdx]);
    }

    private void SetEmptySlotLabels(int i)
    {
        _cardRarities[i] = -1;
        _rarityLabels[i].text = string.Empty;
        _rarityLabels[i].RemoveFromClassList("rarity-common");
        _rarityLabels[i].RemoveFromClassList("rarity-rare");
        _rarityLabels[i].RemoveFromClassList("rarity-epic");
        _rarityLabels[i].style.display = DisplayStyle.None;

        _nameLabels[i].text = "—";
        _descLabels[i].text = string.Empty;
    }

    private VisualElement GetCardElement(int index) =>
        _cardsContainer?.Q<VisualElement>($"card-{index}") ?? _overlay?.Q<VisualElement>($"card-{index}");

    private void OnCardSelected(int index)
    {
        if (_currentSelection == null || index < 0 || index >= _currentSelection.Length) return;
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
        for (int i = 0; i < 3; i++)
            _cardRarities[i] = -1;
        _overlayVisible = false;
        _overlay.style.display = DisplayStyle.None;
        Time.timeScale = 1f;
        GameManager.Instance?.OnBuffSelectionDone();
    }
}
