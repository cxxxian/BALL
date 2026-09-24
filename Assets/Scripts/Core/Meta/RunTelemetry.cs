using UnityEngine;

/// <summary>
/// Phase 3 Telemetry-lite：波次 / Slot 决策观测（Editor 与 Development Build 打日志）。
/// </summary>
public static class RunTelemetry
{
    public static int ContinuesThisSlot { get; private set; }
    public static int CashOutStage { get; private set; } = -1;
    public static bool ClaimedFinal { get; private set; }
    public static SlotCombo FinalCombo { get; private set; }
    public static int ChipsAtSlotOpen { get; private set; }
    public static int ScoreAtWaveStart { get; private set; }
    public static float WaveStartUnscaledTime { get; private set; }

    public static void ResetForNewRun()
    {
        ContinuesThisSlot = 0;
        CashOutStage = -1;
        ClaimedFinal = false;
        FinalCombo = SlotCombo.Smooth;
        ChipsAtSlotOpen = 0;
        ScoreAtWaveStart = 0;
        WaveStartUnscaledTime = 0f;
    }

    public static void OnWaveStarted(int waveIndex, int scoreSnapshot)
    {
        ScoreAtWaveStart = scoreSnapshot;
        WaveStartUnscaledTime = Time.unscaledTime;
        ContinuesThisSlot = 0;
        CashOutStage = -1;
        ClaimedFinal = false;
        FinalCombo = SlotCombo.Smooth;
        _ = waveIndex;
    }

    public static void OnSlotOpened()
    {
        ChipsAtSlotOpen = RunSession.Chips;
        ContinuesThisSlot = 0;
        CashOutStage = -1;
        ClaimedFinal = false;
        FinalCombo = SlotCombo.Smooth;
    }

    public static void RecordContinue(int fromStage, int chipCost, int chipsAfter)
    {
        ContinuesThisSlot++;
        Log($"Continue Stage{fromStage}→{fromStage + 1} cost={chipCost} chips={chipsAfter} continues={ContinuesThisSlot}");
    }

    public static void RecordCashOut(int stage, int chipsRemaining)
    {
        CashOutStage = stage;
        ClaimedFinal = false;
        LogSlotClosed($"CashOut Stage{stage} chips={chipsRemaining}");
    }

    public static void RecordFinalClaim(SlotCombo combo, int chipsRemaining)
    {
        ClaimedFinal = true;
        FinalCombo = combo;
        CashOutStage = 3;
        LogSlotClosed($"FinalClaim combo={combo} chips={chipsRemaining}");
    }

    private static void LogSlotClosed(string decision)
    {
        int wave = GameManager.Instance != null ? GameManager.Instance.Wave : -1;
        int scoreNow = GameManager.Instance != null ? GameManager.Instance.Score : 0;
        int delta = Mathf.Max(0, scoreNow - ScoreAtWaveStart);
        float dur = WaveStartUnscaledTime > 0f ? Time.unscaledTime - WaveStartUnscaledTime : 0f;
        Log(
            $"Wave={wave} {decision} chipsOpen={ChipsAtSlotOpen} chipsNow={RunSession.Chips} " +
            $"continues={ContinuesThisSlot} scoreDelta={delta} waveSec~{dur:0.0}");
    }

    private static void Log(string msg)
    {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        Debug.Log($"[RunTelemetry] {msg}");
#endif
    }
}
