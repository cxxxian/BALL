using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class LoadoutCanvasView : MenuCanvasPanel
{
    [SerializeField] private UnityEngine.UI.Button backButton, equipButton, shopButton, launchButton;
    [SerializeField] private UnityEngine.UI.Text readyStatus, ballName, ballSummary, equipLabel, launchSummary;
    [SerializeField] private UnityEngine.UI.RawImage ballPreview;
    [SerializeField] private UnityEngine.UI.Text[] skillNames, skillDescriptions, skillMetadata;
    [SerializeField] private RectTransform ballList, weaponList;
    [SerializeField] private LoadoutChoiceCard ballTemplate, weaponTemplate;
    private readonly List<LoadoutChoiceCard> ballCards = new List<LoadoutChoiceCard>();
    private readonly List<LoadoutChoiceCard> weaponCards = new List<LoadoutChoiceCard>();
    private BallDefinition previewBall;
    private RunCatalog catalog;
    protected override void Awake()
    {
        base.Awake();
        backButton.onClick.AddListener(() => LoadoutPanelController.Instance?.Hide());
        shopButton.onClick.AddListener(() => MainMenuController.Instance?.OpenShop());
        equipButton.onClick.AddListener(() => { if (previewBall != null) LoadoutPanelController.Instance?.EquipBall(previewBall.ballId); });
        launchButton.onClick.AddListener(() => MainMenuController.Instance?.LaunchEndless());
    }
    public override void Show()
    {
        catalog = RunCatalog.Load(); RunLoadout.Load();
        previewBall = RunLoadout.GetSelectedBall(catalog);
        base.Show();
    }
    public override void RefreshPresentation()
    {
        catalog = RunCatalog.Load(); PlayerProfile.Load(); RunLoadout.Load();
        if (catalog == null) return;
        if (ballCards.Count == 0)
        {
            foreach (var ball in catalog.GetLoadoutBalls())
            {
                if (ball == null) continue;
                var definition = ball;
                var card = Instantiate(ballTemplate, ballList); card.gameObject.SetActive(true);
                card.BindBall(ball, () => Browse(definition)); ballCards.Add(card);
            }
            foreach (var weapon in FlipperWeaponCatalog.GetLoadoutWeapons())
            {
                if (weapon == null) continue;
                string id = weapon.weaponId;
                var card = Instantiate(weaponTemplate, weaponList); card.gameObject.SetActive(true);
                card.BindWeapon(weapon, () => LoadoutPanelController.Instance?.EquipWeapon(id)); weaponCards.Add(card);
            }
            ResetNewList(ballList);
            ResetNewList(weaponList);
        }
        if (previewBall == null) previewBall = RunLoadout.GetSelectedBall(catalog);
        RefreshPreview();
        foreach (var card in ballCards) card.RefreshState(previewBall != null && card.Id == previewBall.ballId, card.Id == RunLoadout.Data.ballId, PlayerProfile.IsBallUnlocked(card.Id));
        // The current data exposes all three weapons. Do not invent paid or locked weapons.
        foreach (var card in weaponCards) card.RefreshState(card.Id == RunLoadout.Data.flipperWeaponId, card.Id == RunLoadout.Data.flipperWeaponId, true);
        var equipped = RunLoadout.GetSelectedBall(catalog); var weaponEquipped = RunLoadout.GetSelectedFlipperWeapon();
        bool valid = RunLoadout.IsValid(catalog);
        readyStatus.text = valid ? "● 准备就绪" : "配置未完成";
        launchSummary.text = $"出战：{(equipped != null ? equipped.displayName : "未选择弹珠")}  ·  {(weaponEquipped != null ? weaponEquipped.displayName : "未选择武器")}";
        launchButton.interactable = valid;
    }
    private void Browse(BallDefinition ball)
    {
        previewBall = ball; RefreshPresentation();
        ballPreview.rectTransform.DOKill(); ballPreview.rectTransform.localScale = Vector3.one * 0.96f;
        ballPreview.rectTransform.DOScale(1, 0.16f).SetEase(Ease.OutCubic).SetUpdate(true);
    }
    private static void ResetNewList(RectTransform content)
    {
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        content.anchoredPosition = Vector2.zero;
        var scroll = content.GetComponentInParent<UnityEngine.UI.ScrollRect>();
        if (scroll != null) scroll.StopMovement();
    }
    private void RefreshPreview()
    {
        if (previewBall == null) return;
        bool unlocked = PlayerProfile.IsBallUnlocked(previewBall.ballId);
        bool equipped = previewBall.ballId == RunLoadout.Data.ballId;
        ballName.text = previewBall.displayName;
        var identity = RunLoadout.GetBoundSkillForPreview(previewBall, 1, catalog);
        ballSummary.text = identity != null ? $"身份协议：{identity.displayName}\n{(unlocked ? "固定双技能 · 选择后装备" : "未解锁 · 可在协议商店获取")}" : "固定双技能 · 均衡协议核心";
        ballPreview.color = Color.Lerp(Color.white, previewBall.glowColor, 0.35f);
        equipLabel.text = !unlocked ? "未解锁" : equipped ? "✓ 已装备" : "装备此弹珠  ›";
        equipButton.interactable = unlocked && !equipped;
        shopButton.gameObject.SetActive(!unlocked);
        for (int i = 0; i < 2; i++)
        {
            var skill = RunLoadout.GetBoundSkillForPreview(previewBall, i, catalog);
            skillNames[i].text = skill != null ? skill.displayName : "未绑定技能";
            skillMetadata[i].text = skill != null ? $"{skill.GetSlotKeyHint(i)}  ·  {skill.baseCooldown:0.#} 秒冷却\n{(skill.activationMode == SkillActivationMode.Aim ? "瞄准释放" : "即时释放")}" : "—";
            skillDescriptions[i].text = skill != null ? ShopCanvasView.PlayerDescription(skill).Replace("\n", "") : "该弹珠尚未绑定此技能。";
        }
    }
    protected override void OnDestroy()
    {
        base.OnDestroy(); if (ballPreview != null) ballPreview.rectTransform.DOKill();
    }
}
