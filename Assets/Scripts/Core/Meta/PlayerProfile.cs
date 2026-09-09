using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class PlayerProfileData
{
    public int credits;
    public List<string> unlockedBallIds = new List<string>();
    public int crateOpensSinceLastEpic;
    public int totalCrateOpens;
    public bool hasCompletedTutorial;
}

/// <summary>局外持久存档：协议币、已解锁弹珠、宝箱保底计数。</summary>
public static class PlayerProfile
{
    private const string SaveFileName = "player_profile.json";
    private const string DefaultBallId = "standard";

    /// <summary>测试阶段：协议币视为无限，购买/开箱不扣减。</summary>
    public const bool UnlimitedCreditsForTesting = true;
    public const int TestCreditsDisplay = 999999;

    /// <summary>测试阶段：RunCatalog 内所有弹珠视为已解锁，可直接装备。</summary>
    public const bool UnlockAllBallsForTesting = true;

    private static PlayerProfileData _data;
    public static PlayerProfileData Data
    {
        get
        {
            if (_data == null) Load();
            return _data;
        }
    }

    public static int Credits =>
        UnlimitedCreditsForTesting ? TestCreditsDisplay : Data.credits;

    /// <summary>存档内真实协议币（结算 UI / 验收用，不受测试无限显示影响）。</summary>
    public static int StoredCredits => Data.credits;
    public static int CrateOpensSinceLastEpic => Data.crateOpensSinceLastEpic;
    public static int TotalCrateOpens => Data.totalCrateOpens;
    public static bool HasCompletedTutorial => Data.hasCompletedTutorial;

    public static void Load()
    {
        string path = GetSavePath();
        if (File.Exists(path))
        {
            try
            {
                string json = File.ReadAllText(path);
                _data = JsonUtility.FromJson<PlayerProfileData>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PlayerProfile] Load failed, resetting: {e.Message}");
                _data = CreateDefault();
            }
        }
        else
        {
            _data = CreateDefault();
        }

        EnsureDefaults();
        if (UnlockAllBallsForTesting)
            UnlockAllCatalogBalls();
    }

    public static void Save()
    {
        EnsureDefaults();
        try
        {
            File.WriteAllText(GetSavePath(), JsonUtility.ToJson(Data, prettyPrint: true));
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlayerProfile] Save failed: {e.Message}");
        }
    }

    public static bool IsBallUnlocked(string ballId)
    {
        if (string.IsNullOrEmpty(ballId)) return false;
        if (UnlockAllBallsForTesting) return true;
        return Data.unlockedBallIds != null && Data.unlockedBallIds.Contains(ballId);
    }

    public static void UnlockBall(string ballId)
    {
        if (string.IsNullOrEmpty(ballId)) return;
        if (Data.unlockedBallIds == null)
            Data.unlockedBallIds = new List<string>();
        if (!Data.unlockedBallIds.Contains(ballId))
            Data.unlockedBallIds.Add(ballId);
    }

    /// <summary>把 RunCatalog 里所有球写入解锁列表（测试用）。</summary>
    public static void UnlockAllCatalogBalls()
    {
        EnsureDefaults();
        var catalog = RunCatalog.Load();
        if (catalog?.balls == null) return;
        foreach (var ball in catalog.balls)
        {
            if (ball != null && !string.IsNullOrEmpty(ball.ballId))
                UnlockBall(ball.ballId);
        }
    }

    public static void AddCredits(int amount)
    {
        if (UnlimitedCreditsForTesting && amount < 0) return;
        Data.credits = Mathf.Max(0, Data.credits + amount);
    }

    public static void RecordCrateOpen(bool gotEpicOrBetter)
    {
        Data.totalCrateOpens++;
        if (gotEpicOrBetter)
            Data.crateOpensSinceLastEpic = 0;
        else
            Data.crateOpensSinceLastEpic++;
    }

    public static void ResetCratePityCounter()
    {
        Data.crateOpensSinceLastEpic = 0;
    }

    public static void MarkTutorialCompleted()
    {
        Data.hasCompletedTutorial = true;
        Save();
    }

    public static void ClearTutorialCompletedFlag()
    {
        Data.hasCompletedTutorial = false;
        Save();
    }

    private static void EnsureDefaults()
    {
        if (_data == null) _data = CreateDefault();
        if (_data.unlockedBallIds == null)
            _data.unlockedBallIds = new List<string>();
        if (!_data.unlockedBallIds.Contains(DefaultBallId))
            _data.unlockedBallIds.Insert(0, DefaultBallId);
    }

    private static PlayerProfileData CreateDefault()
    {
        return new PlayerProfileData
        {
            credits = 0,
            unlockedBallIds = new List<string> { DefaultBallId },
            crateOpensSinceLastEpic = 0,
            totalCrateOpens = 0,
            hasCompletedTutorial = false
        };
    }

    private static string GetSavePath() =>
        Path.Combine(Application.persistentDataPath, SaveFileName);

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Ball/Meta/Add 9999 Credits (Debug)")]
    private static void DebugAddCredits()
    {
        Load();
        AddCredits(9999);
        Save();
        Debug.Log($"[PlayerProfile] Debug credits = {Credits}");
    }

    [UnityEditor.MenuItem("Ball/Meta/Unlock All Balls (Debug)")]
    private static void DebugUnlockAllBalls()
    {
        Load();
        UnlockAllCatalogBalls();
        Save();
        Debug.Log($"[PlayerProfile] Unlocked balls = {Data.unlockedBallIds.Count}");
    }

    [UnityEditor.MenuItem("Ball/Meta/Reset Profile")]
    private static void DebugResetProfile()
    {
        _data = CreateDefault();
        Save();
        Debug.Log("[PlayerProfile] Profile reset.");
    }

    [UnityEditor.MenuItem("Ball/Meta/Reset Tutorial Flag")]
    private static void DebugResetTutorial()
    {
        Load();
        ClearTutorialCompletedFlag();
        Debug.Log("[PlayerProfile] Tutorial flag cleared.");
    }
#endif
}
