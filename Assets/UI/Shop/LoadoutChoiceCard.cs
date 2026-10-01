using System;
using UnityEngine;

/// <summary>Reusable card. Browsing, equipment and unlock states are independent.</summary>
public class LoadoutChoiceCard : MonoBehaviour
{
    [SerializeField] private UnityEngine.UI.Button button;
    [SerializeField] private UnityEngine.UI.Text title, state, description;
    [SerializeField] private UnityEngine.UI.RawImage ballIcon;
    [SerializeField] private LoadoutWeaponGlyph weaponIcon;
    [SerializeField] private ShopNeonFrame frame;
    public string Id { get; private set; }
    public void BindBall(BallDefinition ball, Action callback)
    {
        Id = ball.ballId; title.text = ball.displayName;
        if (ballIcon != null) ballIcon.color = Color.Lerp(Color.white, ball.glowColor, 0.35f);
        Bind(callback);
    }
    public void BindWeapon(FlipperWeaponDefinition weapon, Action callback)
    {
        Id = weapon.weaponId; title.text = weapon.displayName;
        if (description != null) description.text = weapon.weaponType == FlipperWeaponType.Cannon ? "集中火力\n对首领造成高额单体伤害" : weapon.weaponType == FlipperWeaponType.Bomb ? "范围爆破\n清理聚集的敌人" : "持续输出\n用光束压制敌人";
        if (weaponIcon != null) weaponIcon.SetWeapon(weapon.weaponType);
        Bind(callback);
    }
    private void Bind(Action callback)
    {
        button.onClick.RemoveAllListeners(); button.onClick.AddListener(() => callback());
    }
    public void RefreshState(bool selected, bool equipped, bool unlocked)
    {
        state.text = equipped ? "✓ 已装备" : unlocked ? "未装备" : "未解锁";
        state.color = equipped ? new Color(0.2f, 0.9f, 1f) : new Color(0.6f, 0.72f, 0.78f);
        frame.color = new Color(0.18f, 0.86f, 1f, selected ? 0.95f : 0.2f);
        button.image.color = selected ? new Color(0.025f, 0.14f, 0.18f) : new Color(0.018f, 0.045f, 0.065f);
        title.color = unlocked ? Color.white : new Color(0.55f, 0.62f, 0.67f);
        if (ballIcon != null) { var c = ballIcon.color; c.a = unlocked ? 1 : 0.35f; ballIcon.color = c; }
        // Locked balls can still be inspected; equipping is validated by RunLoadout.
    }
}
