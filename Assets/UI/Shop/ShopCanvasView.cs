using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>Shop presentation only. Economy and crate results remain in ShopPanelController.</summary>
public class ShopCanvasView : MonoBehaviour
{
    [SerializeField] private Canvas canvas;
    [SerializeField] private CanvasGroup visibility;
    [SerializeField] private UnityEngine.UI.Button backButton, directButton, crateButton, purchaseButton, openButton;
    [SerializeField] private GameObject directPage, cratePage;
    [SerializeField] private UnityEngine.UI.RawImage preview;
    [SerializeField] private UnityEngine.UI.Text balance, ballName, rarity, price, purchaseLabel, pityLabel;
    [SerializeField] private UnityEngine.UI.Text[] skillNames, skillDescriptions, skillMetadata;
    [SerializeField] private RectTransform cardsContent;
    [SerializeField] private ShopOfferCard cardTemplate;
    [SerializeField] private UnityEngine.UI.Image pityFill;
    private readonly List<ShopOfferCard> cards = new List<ShopOfferCard>();
    private BallDefinition selected;
    private bool direct = true;
    private bool bound;
    private Tween fade;
    public bool Visible => canvas != null && canvas.enabled;

    private void Awake()
    {
        Bind();
        canvas.enabled = false;
    }
    private void Bind()
    {
        if (bound) return;
        bound = true;
        backButton.onClick.AddListener(() => ShopPanelController.Instance?.Hide());
        directButton.onClick.AddListener(() => SelectTab(true));
        crateButton.onClick.AddListener(() => SelectTab(false));
        purchaseButton.onClick.AddListener(() => { if (selected != null) ShopPanelController.Instance?.PurchaseFromCanvas(selected.ballId); });
        openButton.onClick.AddListener(() => ShopPanelController.Instance?.BeginOpenCrate());
    }
    public void Show()
    {
        Bind();
        canvas.enabled = true;
        SelectTab(true);
        Refresh();
        fade?.Kill(); visibility.alpha = 0;
        fade = visibility.DOFade(1, 0.18f).SetUpdate(true);
    }
    public void Hide()
    {
        fade?.Kill();
        canvas.enabled = false;
    }
    public void Resume()
    {
        canvas.enabled = true; visibility.alpha = 1; Refresh();
    }
    public void SelectTab(bool showDirect)
    {
        direct = showDirect;
        directPage.SetActive(direct); cratePage.SetActive(!direct);
        directButton.image.color = direct ? new Color(0.035f, 0.22f, 0.28f) : new Color(0.025f, 0.06f, 0.08f);
        crateButton.image.color = !direct ? new Color(0.035f, 0.22f, 0.28f) : new Color(0.025f, 0.06f, 0.08f);
        var directFrame = directButton.GetComponentInChildren<ShopNeonFrame>();
        var crateFrame = crateButton.GetComponentInChildren<ShopNeonFrame>();
        if (directFrame != null) directFrame.color = new Color(0.18f, 0.86f, 1f, direct ? 1 : 0.22f);
        if (crateFrame != null) crateFrame.color = new Color(0.18f, 0.86f, 1f, direct ? 0.22f : 1);
    }
    public void Refresh()
    {
        PlayerProfile.Load();
        balance.text = $"协议币  {PlayerProfile.Credits:N0}";
        var shop = ShopCatalog.Load(); var run = RunCatalog.Load();
        if (shop == null || run == null) return;
        if (cards.Count == 0)
        {
            foreach (var offer in shop.directOffers)
            {
                var ball = run.GetBall(offer.ballId); if (ball == null) continue;
                var card = Instantiate(cardTemplate, cardsContent);
                card.gameObject.SetActive(true); card.Bind(ball, SelectBall); cards.Add(card);
                if (selected == null) selected = ball;
            }
        }
        if (selected != null) SelectBall(selected, false);
        foreach (var card in cards) card.Refresh(selected);
        int remaining = Mathf.Max(0, ShopCatalog.HardPityOpens - PlayerProfile.CrateOpensSinceLastEpic);
        pityLabel.text = $"再开启 {remaining} 次，必得史诗及以上\n保底进度  {PlayerProfile.CrateOpensSinceLastEpic} / {ShopCatalog.HardPityOpens}";
        pityFill.fillAmount = Mathf.Clamp01(PlayerProfile.CrateOpensSinceLastEpic / (float)ShopCatalog.HardPityOpens);
        openButton.GetComponentInChildren<UnityEngine.UI.Text>().text = $"开启宝箱    {shop.crateCost} 币  ›";
        openButton.interactable = PlayerProfile.Credits >= shop.crateCost;
    }
    private void SelectBall(BallDefinition ball) => SelectBall(ball, true);
    private void SelectBall(BallDefinition ball, bool animate)
    {
        selected = ball;
        ballName.text = ball.displayName;
        rarity.text = ball.crateRarity == BallCrateRarity.Epic ? "史诗 / EPIC" : ball.crateRarity == BallCrateRarity.Legendary ? "传说 / LEGENDARY" : "稀有 / RARE";
        price.text = $"{ShopCatalog.Load().GetDirectPrice(ball)} 币";
        bool owned = PlayerProfile.IsBallUnlocked(ball.ballId);
        purchaseLabel.text = owned ? "已拥有 · 可在战前配置装备" : "解锁弹珠    ›";
        purchaseButton.interactable = !owned && PlayerProfile.Credits >= ShopCatalog.Load().GetDirectPrice(ball);
        preview.color = Color.Lerp(Color.white, ball.glowColor, 0.18f);
        for (int i = 0; i < 2; i++)
        {
            var skill = ball.GetBoundSkill(i);
            skillNames[i].text = skill != null ? skill.displayName : "未绑定技能";
            skillDescriptions[i].text = skill != null ? PlayerDescription(skill) : "此技能槽暂未配置。";
            skillMetadata[i].text = skill != null ? $"{skill.GetSlotKeyHint(i)}   ·   {skill.baseCooldown:0.#} 秒冷却   ·   {(skill.activationMode == SkillActivationMode.Aim ? "瞄准释放" : "即时释放")}" : "—";
        }
        foreach (var card in cards) card.Refresh(ball);
        if (animate)
        {
            preview.rectTransform.DOKill(); preview.rectTransform.localScale = Vector3.one * 0.96f;
            preview.rectTransform.DOScale(1, 0.18f).SetEase(Ease.OutCubic).SetUpdate(true);
        }
    }
    public static string PlayerDescription(SkillDefinition skill)
    {
        // Presentation copy removes implementation jargon without changing skill rules.
        if (skill.implementationType == ActiveSkillType.TimestopAura) return "减缓敌人行动：普通敌人速度降至 45%，首领降至 60%。";
        if (skill.implementationType == ActiveSkillType.ProtocolRedirect) return "进入时缓瞄准，在4秒内选择方向。\n左键确认弹珠的新弹道。";
        if (skill.implementationType == ActiveSkillType.GravitySpike) return "在指定位置生成持续2.5秒的引力阱。\n聚拢普通敌人，便于配合防御塔集中攻击。";
        if (skill.implementationType == ActiveSkillType.BlockShield) return "激活护盾，抵挡下一名踢底敌人并将其消除。\n连击可缩短技能冷却。";
        if (skill.implementationType == ActiveSkillType.CorePulse) return "以弹珠为中心释放冲击波，高伤并击退近身敌人。\n对首领造成一次普通伤害。";
        if (skill.implementationType == ActiveSkillType.ExecuteChain) return "武装后配合协议改向，最多连锁斩杀3次。首领不受影响。";
        if (skill.implementationType == ActiveSkillType.SplitProtocol) return "分裂为3颗弹珠，持续5秒。每颗造成半额碰撞伤害，期间无法斩杀。";
        return skill.GetBriefDescription();
    }
    private void OnDestroy() { fade?.Kill(); if (preview != null) preview.rectTransform.DOKill(); }
}
