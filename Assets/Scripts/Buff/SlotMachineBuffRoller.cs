using System.Collections.Generic;
using UnityEngine;

public static class SlotMachineBuffRoller
{
    private const float EpicPaddingPerMixed = 0.05f;
    private const float EpicPaddingCap = 0.15f;
    private const float RarePaddingPerScrap = 0.03f;

    public static string GetComboDisplayName(SlotCombo combo) => combo switch
    {
        SlotCombo.Jackpot      => "JACKPOT · 头奖",
        SlotCombo.TripleRare   => "三连稀有 · 小吉",
        SlotCombo.DoubleEpic   => "双史诗 · 中吉",
        SlotCombo.DoubleRare   => "双稀有 · 小吉",
        SlotCombo.TripleCommon => "三连普通 · 平顺",
        SlotCombo.Mixed        => "杂色 · 垫刀",
        SlotCombo.Omen         => "凶兆",
        SlotCombo.Smooth       => "平顺",
        _ => combo.ToString()
    };

    /// <summary>组合触发条件（暂停菜单规则卡 / 局内说明用）。</summary>
    public static string GetComboTriggerText(SlotCombo combo) => combo switch
    {
        SlotCombo.Jackpot      => "触发：左、中、右三轮均为史诗",
        SlotCombo.TripleRare   => "触发：三轮均为稀有",
        SlotCombo.DoubleEpic   => "触发：任意两轮为史诗",
        SlotCombo.DoubleRare   => "触发：任意两轮为稀有",
        SlotCombo.TripleCommon => "触发：三轮均为普通",
        SlotCombo.Mixed        => "触发：普通 + 稀有 + 史诗各至少一轮",
        SlotCombo.Omen         => "触发：出现 ? 凶兆轮",
        SlotCombo.Smooth       => "触发：未命中以上固定组合",
        _ => string.Empty
    };

    public static string GetComboRuleText(SlotCombo combo) => combo switch
    {
        SlotCombo.Jackpot      => "三轮 Buff 全额生效 · 轮位税挂 1 个中 Debuff",
        SlotCombo.TripleRare   => "中轮全额生效 · 左右轮折现",
        SlotCombo.DoubleEpic   => "史诗轮 Buff 全部生效 · 赠送 1 次免费重转",
        SlotCombo.DoubleRare   => "免费重转 ×1 · 生效稀有度最高轮",
        SlotCombo.TripleCommon => "左轮生效",
        SlotCombo.Mixed        => "中轮生效 · 下波史诗权重 +5%",
        SlotCombo.Omen         => "? 轮已结算 · 余轮按降级组合生效",
        SlotCombo.Smooth       => "中轮生效",
        _ => string.Empty
    };

    /// <summary>规则卡边框 USS 类名（用颜色区分，不用白/紫/金字眼）。</summary>
    public static string GetComboBorderClass(SlotCombo combo) => combo switch
    {
        SlotCombo.Jackpot or SlotCombo.DoubleEpic => "combo-border-epic",
        SlotCombo.TripleRare or SlotCombo.DoubleRare => "combo-border-rare",
        SlotCombo.TripleCommon => "combo-border-common",
        SlotCombo.Mixed => "combo-border-mixed",
        SlotCombo.Omen => "combo-border-mystery",
        _ => "combo-border-neutral"
    };

    public static string GetReelBorderClass(ReelRarity rarity) => rarity switch
    {
        ReelRarity.Common  => "reel-border-common",
        ReelRarity.Rare    => "reel-border-rare",
        ReelRarity.Epic    => "reel-border-epic",
        ReelRarity.Mystery => "reel-border-mystery",
        _ => "reel-border-neutral"
    };

    public static SlotSpinSession CreateEmptySession(int waveIndex)
    {
        DebuffManager.EnsureExists();
        return new SlotSpinSession
        {
            waveIndex = waveIndex,
            useProgressiveReveal = !RunSession.IsTutorial
        };
    }

    [System.Obsolete("Use CreateEmptySession + SpinAllReels / RevealNextReel from UI")]
    public static SlotSpinSession CreateSession(int waveIndex)
    {
        var session = CreateEmptySession(waveIndex);
        SpinAllReels(session);
        return session;
    }

    /// <summary>
    /// 旧路径：一次三 Reel。渐进模式下请用 <see cref="RevealNextReel"/>。
    /// Mystery 仅解析结果，不立刻 ApplyBuff（Commit 时由 BuildApplyActions 生效）。
    /// </summary>
    public static void SpinAllReels(SlotSpinSession session)
    {
        if (session == null || session.isCommitted) return;

        for (int i = 0; i < 3; i++)
        {
            session.reels[i] = RollSingleReel(session.waveIndex, i);
            var reel = session.reels[i];
            reel.isRevealed = true;
            session.reels[i] = reel;
        }

        ResolveMysteryReels(session, applyBuffImmediately: false);
        session.revealedReelCount = 3;
        session.revealStage = SlotRevealStage.Stage3;
        session.hasSpunOnce = true;
        EvaluateSession(session, grantFreeRerolls: true);
    }

    /// <summary>
    /// 渐进揭示：只 Roll 下一张未揭示 Reel。返回揭示的 reel 下标，失败返回 -1。
    /// 仅 revealedCount==3 时跑 Final Combo。
    /// </summary>
    public static int RevealNextReel(SlotSpinSession session)
    {
        if (session == null || session.isCommitted) return -1;
        if (session.revealedReelCount >= 3) return -1;

        int index = session.revealedReelCount;
        session.reels[index] = RollSingleReel(session.waveIndex, index);
        var reel = session.reels[index];
        reel.isRevealed = true;
        session.reels[index] = reel;

        if (session.reels[index].rarity == ReelRarity.Mystery)
            ResolveMysteryReel(session, index, applyBuffImmediately: false);

        session.revealedReelCount = index + 1;
        session.hasSpunOnce = true;
        session.revealStage = session.revealedReelCount switch
        {
            1 => SlotRevealStage.Stage1,
            2 => SlotRevealStage.Stage2,
            _ => SlotRevealStage.Stage3
        };

        if (session.revealedReelCount == 3)
            EvaluateSession(session, grantFreeRerolls: true);
        else
            session.combo = SlotCombo.Smooth; // Stage1/2 不展示 Final Combo 名

        return index;
    }

    public static bool CanRevealNext(SlotSpinSession session) =>
        session != null
        && !session.isCommitted
        && session.useProgressiveReveal
        && session.revealedReelCount < 3;

    public static bool IsFinalRevealReady(SlotSpinSession session) =>
        session != null && session.revealedReelCount >= 3;

    /// <summary>Stage1→2 花费 1；Stage2→3 花费 2；否则 0。</summary>
    public static int GetContinueChipCost(SlotSpinSession session)
    {
        if (session == null || !session.useProgressiveReveal || session.isCommitted)
            return 0;
        return session.revealedReelCount switch
        {
            1 => 1,
            2 => 2,
            _ => 0
        };
    }

    public static bool CanAffordContinue(SlotSpinSession session)
    {
        int cost = GetContinueChipCost(session);
        return cost > 0 && CanRevealNext(session) && RunSession.Chips >= cost;
    }

    /// <summary>扣除 Continue 筹码；不足返回 false（不改状态）。</summary>
    public static bool TryPayContinue(SlotSpinSession session)
    {
        if (!CanRevealNext(session)) return false;
        int cost = GetContinueChipCost(session);
        if (cost <= 0) return false;
        return RunSession.TrySpendChips(cost);
    }

    /// <summary>Stage1/2 兑现动作（非 Final Combo）。Stage3 请用 <see cref="BuildApplyActions"/>。</summary>
    public static List<ApplyAction> BuildCashOutActions(SlotSpinSession session)
    {
        var actions = new List<ApplyAction>();
        if (session == null || session.isCommitted) return actions;
        if (session.revealedReelCount <= 0 || session.revealedReelCount >= 3)
            return actions;

        if (session.revealedReelCount == 1)
        {
            TryAddFullBuff(actions, session.reels, 0);
            return actions;
        }

        // Stage 2：最高稀有度全额；同稀有度对子 +1 层（MVP；满层由 ApplyBuff 夹紧 / Phase 7 折现）
        int best = ResolveHighestRevealedReel(session.reels, session.revealedReelCount);
        if (best < 0) return actions;

        bool samePair = IsSameRarityPair(session.reels, session.revealedReelCount);
        TryAddFullBuff(actions, session.reels, best, extraStacks: samePair ? 1 : 0);
        return actions;
    }

    public static List<OutcomePreviewLine> BuildCashOutPreview(SlotSpinSession session)
    {
        var lines = new List<OutcomePreviewLine>();
        if (session == null) return lines;

        var actions = BuildCashOutActions(session);
        var covered = new HashSet<int>();

        foreach (var action in actions)
        {
            if (action.kind != ApplyActionKind.FullBuff || action.buff == null) continue;
            covered.Add(action.reelIndex);
            var extra = action.extraStacks > 0 ? $" · 对子 +{action.extraStacks} 层" : string.Empty;
            lines.Add(new OutcomePreviewLine
            {
                kind = OutcomeLineKind.FullApply,
                reelIndex = action.reelIndex,
                text = $"✓ {GetReelName(action.reelIndex)} · {action.buff.buffName} · 兑现{extra}",
                detail = FormatOutcomeDetail(action.buff.GetBriefDescription())
            });
        }

        for (int i = 0; i < session.revealedReelCount && i < 3; i++)
        {
            var reel = session.reels[i];
            if (!reel.isRevealed || reel.isEmptySpin || reel.buff == null) continue;
            if (covered.Contains(i)) continue;
            lines.Add(new OutcomePreviewLine
            {
                kind = OutcomeLineKind.Ignored,
                reelIndex = i,
                text = $"— {GetReelName(i)} · {reel.buff.buffName} · 未兑现",
                detail = FormatOutcomeDetail(reel.buff.GetBriefDescription())
            });
        }

        foreach (var id in session.pendingDebuffs)
        {
            lines.Add(new OutcomePreviewLine
            {
                kind = OutcomeLineKind.DebuffPending,
                reelIndex = -1,
                text = $"⚠ {DebuffManager.GetDisplayName(id)}（{DebuffManager.GetTierLabel(id)}）",
                detail = FormatOutcomeDetail(DebuffManager.GetDescription(id))
            });
        }

        if (lines.Count == 0)
        {
            lines.Add(new OutcomePreviewLine
            {
                kind = OutcomeLineKind.Ignored,
                reelIndex = -1,
                text = "— 当前无可兑现 Buff"
            });
        }

        return lines;
    }

    private static bool IsSameRarityPair(ReelResult[] reels, int revealedCount)
    {
        if (revealedCount < 2) return false;
        var a = reels[0];
        var b = reels[1];
        if (!a.isRevealed || !b.isRevealed) return false;
        if (a.isEmptySpin || b.isEmptySpin || a.buff == null || b.buff == null) return false;
        if (a.rarity == ReelRarity.Mystery || b.rarity == ReelRarity.Mystery) return false;
        return a.rarity == b.rarity;
    }

    private static int ResolveHighestRevealedReel(ReelResult[] reels, int revealedCount)
    {
        int bestRarity = -1;
        var candidates = new List<int>();
        int n = Mathf.Min(3, revealedCount);
        for (int i = 0; i < n; i++)
        {
            if (!reels[i].isRevealed || reels[i].isEmptySpin || reels[i].buff == null) continue;
            if (reels[i].rarity == ReelRarity.Mystery) continue;
            int r = (int)reels[i].rarity;
            if (r > bestRarity)
            {
                bestRarity = r;
                candidates.Clear();
                candidates.Add(i);
            }
            else if (r == bestRarity)
            {
                candidates.Add(i);
            }
        }

        if (candidates.Count == 0) return -1;
        if (candidates.Count == 1) return candidates[0];
        // 对子同稀有度：优先左轮（稳定）
        return candidates[0];
    }

    public static void MarkCommitted(SlotSpinSession session)
    {
        if (session == null) return;
        session.isCommitted = true;
        session.revealStage = SlotRevealStage.Resolved;
    }

    public static bool HasFreeRerollAvailable(SlotSpinSession session) =>
        session != null && session.freeRerollsRemaining > 0;

    public static bool HasPaidRerollAvailable(SlotSpinSession session) =>
        session != null && session.paidRerollsRemaining > 0;

    public static bool HasRerollAvailable(SlotSpinSession session) =>
        HasFreeRerollAvailable(session) || HasPaidRerollAvailable(session);

    public static bool CanRerollReel(SlotSpinSession session, int index)
    {
        if (session == null || index < 0 || index >= 3) return false;
        if (session.isCommitted) return false;
        if (!session.reels[index].isRevealed) return false;
        if (session.reelRerollCount[index] >= 2) return false;
        return HasRerollAvailable(session);
    }

    public static string GetReelName(int index) => index switch
    {
        0 => "左轮",
        1 => "中轮",
        2 => "右轮",
        _ => $"轮{index}"
    };

    public static string GetPaidRerollWarning(SlotSpinSession session)
    {
        if (session == null || session.freeRerollsRemaining > 0) return string.Empty;
        if (session.paidRerollsRemaining <= 0) return string.Empty;
        return session.paidRerollsUsed >= 1
            ? "付费重转将挂中 Debuff"
            : "付费重转将挂轻 Debuff";
    }

    public static bool TryRerollReel(SlotSpinSession session, int reelIndex, bool isFree)
    {
        if (reelIndex < 0 || reelIndex >= 3) return false;
        if (session.reelRerollCount[reelIndex] >= 2) return false;

        if (isFree)
        {
            if (session.freeRerollsRemaining <= 0) return false;
            session.freeRerollsRemaining--;
        }
        else
        {
            if (session.paidRerollsRemaining <= 0) return false;
            session.paidRerollsRemaining--;
            session.paidRerollsUsed++;
            var tier = session.paidRerollsUsed >= 2 ? DebuffTier.Medium : DebuffTier.Light;
            session.pendingDebuffs.Add(DebuffManager.Instance.PickDebuffForTax(tier));
        }

        session.reelRerollCount[reelIndex]++;
        session.reels[reelIndex] = RollSingleReel(session.waveIndex, reelIndex);
        var rolled = session.reels[reelIndex];
        rolled.isRevealed = true;
        session.reels[reelIndex] = rolled;
        if (session.reels[reelIndex].rarity == ReelRarity.Mystery)
            ResolveMysteryReel(session, reelIndex, applyBuffImmediately: false);

        // 未满三 Reel 不跑 Final Combo（避免 Stage1/2 误判 DoubleEpic）
        if (session.revealedReelCount >= 3)
            EvaluateSession(session, grantFreeRerolls: false);
        return true;
    }

    public static void RerollReel(SlotSpinSession session, int reelIndex, bool isFree)
    {
        TryRerollReel(session, reelIndex, isFree);
    }

    public static void EvaluateSession(SlotSpinSession session, bool grantFreeRerolls = false)
    {
        if (session == null) return;
        if (session.revealedReelCount < 3)
        {
            session.combo = SlotCombo.Smooth;
            return;
        }

        session.combo = DetectCombo(session.reels);
        if (grantFreeRerolls)
            session.freeRerollsRemaining = GetFreeRerollsForCombo(session.combo);
    }

    public static List<ApplyAction> BuildApplyActions(SlotSpinSession session, bool commitJackpotTax = true)
    {
        var actions = new List<ApplyAction>();
        var reels = session.reels;
        var combo = session.combo;

        switch (combo)
        {
            case SlotCombo.Jackpot:
                AddAllReelBuffs(actions, reels);
                if (commitJackpotTax)
                {
                    session.pendingDebuffs.Add(
                        DebuffManager.Instance != null
                            ? DebuffManager.Instance.PickDebuffForTax(DebuffTier.Medium, jackpot: true)
                            : DebuffId.D6_NextWaveGloom);
                }
                break;

            case SlotCombo.TripleRare:
                TryAddFullBuff(actions, reels, 1);
                TryAddPurpleScrap(actions, reels, 0);
                TryAddPurpleScrap(actions, reels, 2);
                break;

            case SlotCombo.DoubleEpic:
                for (int i = 0; i < 3; i++)
                    if (reels[i].rarity == ReelRarity.Epic)
                        TryAddFullBuff(actions, reels, i);
                break;

            case SlotCombo.DoubleRare:
                TryAddFullBuff(actions, reels, ResolveHighestRarityReel(reels));
                break;

            case SlotCombo.TripleCommon:
                TryAddFullBuff(actions, reels, 0);
                break;

            case SlotCombo.Mixed:
                TryAddFullBuff(actions, reels, 1);
                actions.Add(new ApplyAction { kind = ApplyActionKind.EpicWeightPadding });
                break;

            case SlotCombo.Omen:
            case SlotCombo.Smooth:
                TryAddFullBuff(actions, reels, 1);
                break;
        }

        return actions;
    }

    public static List<OutcomePreviewLine> BuildOutcomePreview(SlotSpinSession session)
    {
        var lines = new List<OutcomePreviewLine>();
        if (session == null) return lines;

        for (int i = 0; i < 3; i++)
        {
            var reel = session.reels[i];
            if (!reel.alreadyApplied || reel.buff == null) continue;
            lines.Add(new OutcomePreviewLine
            {
                kind = OutcomeLineKind.MysteryApplied,
                reelIndex = i,
                text = $"{GetReelName(i)} · {reel.buff.buffName} · ? 轮已生效",
                detail = FormatOutcomeDetail(reel.buff.GetBriefDescription())
            });
        }

        var actions = BuildApplyActions(session, commitJackpotTax: false);
        var covered = new HashSet<int>();

        foreach (var action in actions)
        {
            switch (action.kind)
            {
                case ApplyActionKind.FullBuff:
                    covered.Add(action.reelIndex);
                    var extra = action.extraStacks > 0 ? $" · +{action.extraStacks} 叠层" : string.Empty;
                    lines.Add(new OutcomePreviewLine
                    {
                        kind = OutcomeLineKind.FullApply,
                        reelIndex = action.reelIndex,
                        text = $"✓ {GetReelName(action.reelIndex)} · {action.buff.buffName} · 全额生效{extra}",
                        detail = FormatOutcomeDetail(action.buff.GetBriefDescription())
                    });
                    break;
                case ApplyActionKind.PurpleScrap:
                    covered.Add(action.reelIndex);
                    lines.Add(new OutcomePreviewLine
                    {
                        kind = OutcomeLineKind.PurpleScrap,
                        reelIndex = action.reelIndex,
                        text = $"◈ {GetReelName(action.reelIndex)} · {action.buff.buffName} · 折现",
                        detail = FormatOutcomeDetail(action.buff.GetBriefDescription(), "本波不整卡生效，折现为半层/垫刀")
                    });
                    break;
                case ApplyActionKind.EpicWeightPadding:
                    lines.Add(new OutcomePreviewLine
                    {
                        kind = OutcomeLineKind.EpicPadding,
                        reelIndex = -1,
                        text = "杂色垫刀 · 下波史诗权重 +5%"
                    });
                    break;
            }
        }

        for (int i = 0; i < 3; i++)
        {
            var reel = session.reels[i];
            if (reel.isEmptySpin || reel.buff == null) continue;
            if (reel.alreadyApplied || covered.Contains(i)) continue;
            lines.Add(new OutcomePreviewLine
            {
                kind = OutcomeLineKind.Ignored,
                reelIndex = i,
                text = $"— {GetReelName(i)} · {reel.buff.buffName} · 未生效",
                detail = FormatOutcomeDetail(reel.buff.GetBriefDescription())
            });
        }

        if (session.combo == SlotCombo.Jackpot)
        {
            lines.Add(new OutcomePreviewLine
            {
                kind = OutcomeLineKind.JackpotTax,
                reelIndex = -1,
                text = "⚠ Jackpot 轮位税 · 将挂 1 个中 Debuff"
            });
        }

        foreach (var id in session.pendingDebuffs)
        {
            lines.Add(new OutcomePreviewLine
            {
                kind = OutcomeLineKind.DebuffPending,
                reelIndex = -1,
                text = $"⚠ {DebuffManager.GetDisplayName(id)}（{DebuffManager.GetTierLabel(id)}）",
                detail = FormatOutcomeDetail(DebuffManager.GetDescription(id))
            });
        }

        return lines;
    }

    public static void ExecuteApplyActions(List<ApplyAction> actions, BuffManager bm)
    {
        if (bm == null) return;

        foreach (var action in actions)
        {
            switch (action.kind)
            {
                case ApplyActionKind.FullBuff:
                    if (action.buff != null)
                        bm.ApplyBuff(action.buff, action.extraStacks);
                    break;
                case ApplyActionKind.PurpleScrap:
                    if (action.buff != null)
                        bm.ApplyPurpleScrap(action.buff);
                    break;
                case ApplyActionKind.EpicWeightPadding:
                    bm.AddEpicWeightPadding(EpicPaddingPerMixed, EpicPaddingCap);
                    break;
            }
        }
    }

    public static void ApplyPendingDebuffs(SlotSpinSession session)
    {
        DebuffManager.EnsureExists();
        if (DebuffManager.Instance == null) return;
        foreach (var id in session.pendingDebuffs)
            DebuffManager.Instance.ApplyDebuff(id);
        session.pendingDebuffs.Clear();
    }

    private static ReelResult RollSingleReel(int waveIndex, int reelIndex)
    {
        var rarity = RollRarity(waveIndex);
        return BuildReelResult(waveIndex, rarity);
    }

    private static ReelResult BuildReelResult(int waveIndex, ReelRarity rarity)
    {
        if (rarity == ReelRarity.Mystery)
            return new ReelResult { rarity = ReelRarity.Mystery, buff = null, isEmptySpin = false };

        var buff = DrawBuffForRarity(waveIndex, rarity, out var resolvedRarity);
        if (buff == null)
            return new ReelResult { rarity = resolvedRarity, buff = null, isEmptySpin = true };

        return new ReelResult { rarity = resolvedRarity, buff = buff, isEmptySpin = false };
    }

    private static void ResolveMysteryReels(SlotSpinSession session, bool applyBuffImmediately)
    {
        for (int i = 0; i < 3; i++)
        {
            if (session.reels[i].rarity == ReelRarity.Mystery && session.reels[i].isRevealed)
                ResolveMysteryReel(session, i, applyBuffImmediately);
        }
    }

    /// <summary>
    /// 解析 ? 轮结果。Phase 1：默认不 ApplyBuff；Commit / Claim 时由 BuildApplyActions 生效。
    /// </summary>
    private static void ResolveMysteryReel(SlotSpinSession session, int reelIndex, bool applyBuffImmediately)
    {
        DebuffManager.EnsureExists();
        var bm = BuffManager.Instance;
        bool wasRevealed = session.reels[reelIndex].isRevealed;

        if (Random.value < 0.7f)
        {
            var baseRarity = RollRarityWithoutMystery(session.waveIndex);
            var upgraded = UpgradeRarity(baseRarity);
            var buff = DrawBuffForRarity(session.waveIndex, upgraded, out var resolved);
            session.reels[reelIndex] = new ReelResult
            {
                rarity = resolved,
                buff = buff,
                isEmptySpin = buff == null,
                mysteryResolved = true,
                alreadyApplied = false,
                isRevealed = wasRevealed
            };

            if (applyBuffImmediately && buff != null && bm != null)
            {
                bm.ApplyBuff(buff);
                var applied = session.reels[reelIndex];
                applied.alreadyApplied = true;
                session.reels[reelIndex] = applied;
            }
        }
        else
        {
            session.reels[reelIndex] = new ReelResult
            {
                rarity = ReelRarity.Mystery,
                buff = null,
                isEmptySpin = true,
                mysteryResolved = true,
                alreadyApplied = false,
                isRevealed = wasRevealed
            };

            if (DebuffManager.Instance != null)
                session.pendingDebuffs.Add(DebuffManager.Instance.PickDebuffForTax(DebuffTier.Light));
        }
    }

    private static ReelRarity RollRarity(int waveIndex)
    {
        GetRarityWeights(waveIndex, out float common, out float rare, out float epic, out float mystery);
        float roll = Random.value * 100f;
        if (roll < common) return ReelRarity.Common;
        roll -= common;
        if (roll < rare) return ReelRarity.Rare;
        roll -= rare;
        if (roll < epic) return ReelRarity.Epic;
        return ReelRarity.Mystery;
    }

    private static ReelRarity RollRarityWithoutMystery(int waveIndex)
    {
        GetRarityWeights(waveIndex, out float common, out float rare, out float epic, out float mystery);
        float total = common + rare + epic;
        float roll = Random.value * total;
        if (roll < common) return ReelRarity.Common;
        roll -= common;
        if (roll < rare) return ReelRarity.Rare;
        return ReelRarity.Epic;
    }

    private static void GetRarityWeights(int waveIndex, out float common, out float rare, out float epic, out float mystery)
    {
        int w = Mathf.Max(1, waveIndex);
        if (w <= 3)
        {
            common = 70f; rare = 25f; epic = 5f; mystery = 0f;
        }
        else if (w <= 6)
        {
            common = 55f; rare = 32f; epic = 10f; mystery = 5f;
        }
        else
        {
            common = 45f; rare = 35f; epic = 15f; mystery = 5f;
        }

        var bm = BuffManager.Instance;
        if (bm != null)
        {
            epic += bm.EpicWeightPadding * 100f;
            rare += bm.ConsumeRareWeightPadding() * 100f;
        }

        if (DebuffManager.Instance != null && DebuffManager.Instance.ConsumeNextWaveCommonWeightBoost())
            common += 10f;

        float total = common + rare + epic + mystery;
        if (total <= 0f) return;
        common = common / total * 100f;
        rare = rare / total * 100f;
        epic = epic / total * 100f;
        mystery = mystery / total * 100f;
    }

    private static ReelRarity UpgradeRarity(ReelRarity rarity) => rarity switch
    {
        ReelRarity.Common => ReelRarity.Rare,
        ReelRarity.Rare => ReelRarity.Epic,
        _ => ReelRarity.Epic
    };

    private static BuffRarity ToBuffRarity(ReelRarity rarity) => (BuffRarity)(int)rarity;

    private static BuffDefinition DrawBuffForRarity(int waveIndex, ReelRarity rarity, out ReelRarity resolvedRarity)
    {
        resolvedRarity = rarity;
        var bm = BuffManager.Instance;
        if (bm == null) return null;

        for (int downgrade = 0; downgrade < 3; downgrade++)
        {
            var buff = bm.DrawRandomFromPool(ToBuffRarity(resolvedRarity), waveIndex);
            if (buff != null) return buff;

            if (resolvedRarity == ReelRarity.Epic) resolvedRarity = ReelRarity.Rare;
            else if (resolvedRarity == ReelRarity.Rare) resolvedRarity = ReelRarity.Common;
            else break;
        }

        return null;
    }

    private static SlotCombo DetectCombo(ReelResult[] reels)
    {
        CountRarities(reels, out int epic, out int rare, out int common, out int mystery, out int active);

        if (mystery > 0) return SlotCombo.Omen;
        if (active == 0) return SlotCombo.Smooth;

        if (epic == 3) return SlotCombo.Jackpot;
        if (rare == 3) return SlotCombo.TripleRare;
        if (epic == 2) return SlotCombo.DoubleEpic;
        if (rare == 2) return SlotCombo.DoubleRare;
        if (common == 3) return SlotCombo.TripleCommon;
        if (epic >= 1 && rare >= 1 && common >= 1) return SlotCombo.Mixed;

        return SlotCombo.Smooth;
    }

    private static void CountRarities(ReelResult[] reels, out int epic, out int rare, out int common, out int mystery, out int active)
    {
        epic = rare = common = mystery = active = 0;
        foreach (var reel in reels)
        {
            if (!reel.isRevealed || reel.isEmptySpin) continue;
            active++;
            switch (reel.rarity)
            {
                case ReelRarity.Epic: epic++; break;
                case ReelRarity.Rare: rare++; break;
                case ReelRarity.Common: common++; break;
                case ReelRarity.Mystery: mystery++; break;
            }
        }
    }

    private static int GetFreeRerollsForCombo(SlotCombo combo) => combo switch
    {
        SlotCombo.DoubleEpic => 1,
        SlotCombo.DoubleRare => 1,
        _ => 0
    };

    private static int ResolveHighestRarityReel(ReelResult[] reels)
    {
        int bestRarity = -1;
        var candidates = new List<int>();

        for (int i = 0; i < 3; i++)
        {
            if (reels[i].isEmptySpin || reels[i].buff == null) continue;
            int r = (int)reels[i].rarity;
            if (r > bestRarity)
            {
                bestRarity = r;
                candidates.Clear();
                candidates.Add(i);
            }
            else if (r == bestRarity)
            {
                candidates.Add(i);
            }
        }

        if (candidates.Count == 0) return 1;
        if (candidates.Count == 1) return candidates[0];
        if (candidates.Contains(1)) return 1;
        return candidates[0];
    }

    private static void AddAllReelBuffs(List<ApplyAction> actions, ReelResult[] reels)
    {
        for (int i = 0; i < 3; i++)
            TryAddFullBuff(actions, reels, i);
    }

    private static void TryAddFullBuff(List<ApplyAction> actions, ReelResult[] reels, int index, int extraStacks = 0)
    {
        if (index < 0 || index >= 3) return;
        if (reels[index].isEmptySpin || reels[index].buff == null || reels[index].alreadyApplied) return;
        actions.Add(new ApplyAction
        {
            kind = ApplyActionKind.FullBuff,
            buff = reels[index].buff,
            reelIndex = index,
            extraStacks = extraStacks
        });
    }

    private static void TryAddPurpleScrap(List<ApplyAction> actions, ReelResult[] reels, int index)
    {
        if (index < 0 || index >= 3) return;
        if (reels[index].isEmptySpin || reels[index].buff == null) return;
        actions.Add(new ApplyAction
        {
            kind = ApplyActionKind.PurpleScrap,
            buff = reels[index].buff,
            reelIndex = index
        });
    }

    private const int OutcomeDetailMaxChars = 36;

    private static string FormatOutcomeDetail(string text, string suffix = null)
    {
        if (!string.IsNullOrWhiteSpace(suffix))
            text = string.IsNullOrWhiteSpace(text) ? suffix : $"{text.Trim()}（{suffix}）";

        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        text = text.Trim();
        return text.Length <= OutcomeDetailMaxChars ? text : text.Substring(0, OutcomeDetailMaxChars) + "…";
    }
}
