using UnityEngine;

public struct CrateOpenResult
{
    public bool IsDuplicate;
    public string BallId;
    public string BallDisplayName;
    public BallCrateRarity Rarity;
    public int CreditsRefund;
    public bool IsNewUnlock;
}

public static class ShopService
{
    public static bool TryPurchaseDirect(string ballId, out string error)
    {
        error = null;
        PlayerProfile.Load();
        var runCatalog = RunCatalog.Load();
        var shopCatalog = ShopCatalog.Load();

        if (runCatalog == null || shopCatalog == null)
        {
            error = "商店数据未配置";
            return false;
        }

        var ball = runCatalog.GetBall(ballId);
        if (ball == null)
        {
            error = "弹珠不存在";
            return false;
        }

        if (ball.acquisitionType != BallAcquisitionType.DirectPurchase)
        {
            error = "该弹珠不可直购";
            return false;
        }

        if (PlayerProfile.IsBallUnlocked(ballId))
        {
            error = "已拥有该弹珠";
            return false;
        }

        int price = shopCatalog.GetDirectPrice(ball);
        if (PlayerProfile.Credits < price)
        {
            error = "协议币不足";
            return false;
        }

        PlayerProfile.AddCredits(-price);
        PlayerProfile.UnlockBall(ballId);
        PlayerProfile.Save();
        return true;
    }

    public static bool TryOpenCrate(out CrateOpenResult result, out string error)
    {
        result = default;
        error = null;
        PlayerProfile.Load();

        var shopCatalog = ShopCatalog.Load();
        var runCatalog = RunCatalog.Load();
        if (shopCatalog == null || runCatalog == null || shopCatalog.lootTable == null)
        {
            error = "宝箱数据未配置";
            return false;
        }

        if (PlayerProfile.Credits < shopCatalog.crateCost)
        {
            error = "协议币不足";
            return false;
        }

        bool hardPity = PlayerProfile.CrateOpensSinceLastEpic >= ShopCatalog.HardPityOpens
                        && shopCatalog.lootTable.HasUnownedEpic(runCatalog, PlayerProfile.Data);
        bool softPity = !hardPity && PlayerProfile.CrateOpensSinceLastEpic >= ShopCatalog.SoftPityOpens;

        if (!shopCatalog.lootTable.TryRoll(
                runCatalog,
                PlayerProfile.Data,
                hardPity,
                softPity,
                out string ballId,
                out BallCrateRarity rarity))
        {
            error = "宝箱奖池为空";
            return false;
        }

        PlayerProfile.AddCredits(-shopCatalog.crateCost);

        var ball = runCatalog.GetBall(ballId);
        bool alreadyOwned = PlayerProfile.IsBallUnlocked(ballId);
        int refund = 0;

        if (alreadyOwned)
        {
            refund = shopCatalog.lootTable.GetDuplicateRefund(rarity);
            PlayerProfile.AddCredits(refund);
        }
        else
        {
            PlayerProfile.UnlockBall(ballId);
        }

        bool gotEpic = rarity == BallCrateRarity.Epic || rarity == BallCrateRarity.Legendary;
        if (hardPity && gotEpic)
            PlayerProfile.ResetCratePityCounter();
        else
            PlayerProfile.RecordCrateOpen(gotEpic);

        PlayerProfile.Save();

        result = new CrateOpenResult
        {
            IsDuplicate = alreadyOwned,
            BallId = ballId,
            BallDisplayName = ball != null ? ball.displayName : ballId,
            Rarity = rarity,
            CreditsRefund = refund,
            IsNewUnlock = !alreadyOwned
        };
        return true;
    }
}
