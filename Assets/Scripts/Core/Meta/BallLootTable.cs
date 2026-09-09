using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class BallLootEntry
{
    public string ballId;
    public int weight = 1;
    public BallCrateRarity crateRarity = BallCrateRarity.Rare;
}

[CreateAssetMenu(fileName = "BallLootTable", menuName = "Ball/Meta/BallLootTable")]
public class BallLootTable : ScriptableObject
{
    public List<BallLootEntry> entries = new List<BallLootEntry>();

    public int GetDuplicateRefund(BallCrateRarity rarity) => rarity switch
    {
        BallCrateRarity.Epic => 80,
        BallCrateRarity.Legendary => 150,
        _ => 30
    };

    public bool TryRoll(
        RunCatalog runCatalog,
        PlayerProfileData profile,
        bool forceUnownedEpic,
        bool epicWeightBoost,
        out string ballId,
        out BallCrateRarity rarity)
    {
        ballId = null;
        rarity = BallCrateRarity.Rare;

        var pool = BuildWeightedPool(runCatalog, profile, forceUnownedEpic, epicWeightBoost);
        if (pool.Count == 0) return false;

        int total = 0;
        foreach (var p in pool) total += p.weight;
        int roll = UnityEngine.Random.Range(0, total);
        int acc = 0;
        foreach (var p in pool)
        {
            acc += p.weight;
            if (roll < acc)
            {
                ballId = p.ballId;
                rarity = p.rarity;
                return true;
            }
        }

        var fallback = pool[pool.Count - 1];
        ballId = fallback.ballId;
        rarity = fallback.rarity;
        return true;
    }

    private struct WeightedBall
    {
        public string ballId;
        public int weight;
        public BallCrateRarity rarity;
    }

    private List<WeightedBall> BuildWeightedPool(
        RunCatalog runCatalog,
        PlayerProfileData profile,
        bool forceUnownedEpic,
        bool epicWeightBoost)
    {
        var result = new List<WeightedBall>();
        if (entries == null || runCatalog == null) return result;

        foreach (var entry in entries)
        {
            if (entry == null || string.IsNullOrEmpty(entry.ballId)) continue;
            var ball = runCatalog.GetBall(entry.ballId);
            if (ball == null || ball.acquisitionType != BallAcquisitionType.CrateOnly) continue;

            if (forceUnownedEpic)
            {
                if (entry.crateRarity != BallCrateRarity.Epic) continue;
                if (profile.unlockedBallIds != null && profile.unlockedBallIds.Contains(entry.ballId)) continue;
            }

            int weight = Mathf.Max(1, entry.weight);
            if (epicWeightBoost && entry.crateRarity == BallCrateRarity.Epic)
                weight *= 3;

            result.Add(new WeightedBall
            {
                ballId = entry.ballId,
                weight = weight,
                rarity = entry.crateRarity
            });
        }

        return result;
    }

    public bool HasUnownedEpic(RunCatalog runCatalog, PlayerProfileData profile)
    {
        if (entries == null || runCatalog == null || profile?.unlockedBallIds == null) return false;
        foreach (var entry in entries)
        {
            if (entry == null || entry.crateRarity != BallCrateRarity.Epic) continue;
            if (string.IsNullOrEmpty(entry.ballId)) continue;
            if (!profile.unlockedBallIds.Contains(entry.ballId))
                return true;
        }
        return false;
    }
}
