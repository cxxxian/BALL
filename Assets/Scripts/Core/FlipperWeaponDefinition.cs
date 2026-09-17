using UnityEngine;

[CreateAssetMenu(fileName = "FlipperWeapon_New", menuName = "Ball/FlipperWeaponDefinition")]
public class FlipperWeaponDefinition : ScriptableObject
{
    [Header("Identity")]
    public string weaponId;
    public string displayName;
    [TextArea(2, 3)]
    public string loadoutDescription;
    public FlipperWeaponType weaponType = FlipperWeaponType.Cannon;

    [Header("Cannon — 单体爆发")]
    [Tooltip("对 Boss 造成的命中数（直接扣 HP，不吃弹珠 Buff）")]
    public int cannonBossDamage = 3;

    [Header("Bomb — 范围清场")]
    public int bombBossDamage = 1;
    [Tooltip("对小兵伤害；设大值可清场")]
    public int bombMinionDamage = 99;
    public float bombRadius = 8f;

    [Header("Laser — 持续输出")]
    public int laserBossDamagePerTick = 1;
    public float laserDuration = 2f;
    public float laserTickInterval = 0.5f;

    [Header("Presentation")]
    public Color effectColor = new Color(1f, 0.55f, 0.12f, 1f);
}
