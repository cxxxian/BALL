/// <summary>
/// 协议缓存盘 / 台面规则层可发放的奖励类型。
/// </summary>
public enum ProtocolRewardType
{
    /// <summary>分数爆发。</summary>
    ScoreBurst = 0,
    /// <summary>全体技能 CD 回退。</summary>
    SkillCooldown = 1,
    /// <summary>在落点释放 Bumper 脉冲清怪。</summary>
    BumperPulse = 2,
    /// <summary>立刻叠若干滞空连击。</summary>
    ComboBoost = 3,
    /// <summary>回复 1 命（受 MaxLives 限制）。</summary>
    Heal = 4,
    /// <summary>协议锁 +1；满锁触发短过载。</summary>
    ProtocolLock = 5,
    /// <summary>短时弹珠伤害加成。</summary>
    DamageBuff = 6,
}
