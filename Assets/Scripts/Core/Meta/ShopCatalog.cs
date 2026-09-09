using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class DirectOfferEntry
{
    public string ballId;
    [Tooltip("0 = use BallDefinition.directPrice")]
    public int priceOverride;
}

[CreateAssetMenu(fileName = "ShopCatalog", menuName = "Ball/Meta/ShopCatalog")]
public class ShopCatalog : ScriptableObject
{
    public List<DirectOfferEntry> directOffers = new List<DirectOfferEntry>();
    public int crateCost = 150;
    public BallLootTable lootTable;

    public const int SoftPityOpens = 10;
    public const int HardPityOpens = 30;

    private static ShopCatalog _cached;

    public static ShopCatalog Load()
    {
        if (_cached != null) return _cached;
        _cached = Resources.Load<ShopCatalog>("ShopCatalog");
        if (_cached == null)
            Debug.LogError("[ShopCatalog] Missing Resources/ShopCatalog.asset");
        return _cached;
    }

    public int GetDirectPrice(BallDefinition ball)
    {
        if (ball == null) return 0;
        foreach (var offer in directOffers)
        {
            if (offer != null && offer.ballId == ball.ballId && offer.priceOverride > 0)
                return offer.priceOverride;
        }
        return ball.directPrice;
    }
}
