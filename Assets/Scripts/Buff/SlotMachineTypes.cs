using System.Collections.Generic;

public enum ReelRarity
{
    Common = 0,
    Rare = 1,
    Epic = 2,
    Mystery = 3
}

public enum SlotCombo
{
    Jackpot,
    TripleRare,
    DoubleEpic,
    DoubleRare,
    TripleCommon,
    Mixed,
    Omen,
    Smooth
}

public struct ReelResult
{
    public ReelRarity rarity;
    public BuffDefinition buff;
    public bool isEmptySpin;
    public bool mysteryResolved;
    public bool alreadyApplied;
}

public enum ApplyActionKind
{
    FullBuff,
    PurpleScrap,
    EpicWeightPadding
}

public struct ApplyAction
{
    public ApplyActionKind kind;
    public BuffDefinition buff;
    public int reelIndex;
    public int extraStacks;
}

public class SlotSpinSession
{
    public ReelResult[] reels = new ReelResult[3];
    public SlotCombo combo;
    public int freeRerollsRemaining;
    public int paidRerollsRemaining = 2;
    public int paidRerollsUsed;
    public readonly int[] reelRerollCount = new int[3];
    public readonly List<DebuffId> pendingDebuffs = new List<DebuffId>();
    public int waveIndex;
    public bool hasSpunOnce;
}

public enum OutcomeLineKind
{
    FullApply,
    PurpleScrap,
    Ignored,
    EpicPadding,
    MysteryApplied,
    DebuffPending,
    JackpotTax
}

public struct OutcomePreviewLine
{
    public OutcomeLineKind kind;
    public int reelIndex;
    public string text;
    public string detail;
}
