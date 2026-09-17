using System.Collections.Generic;
using UnityEngine;

/// <summary>从 Resources/FlipperWeapons 读取挡板武器列表（战前配置 / 战斗装备共用）。</summary>
public static class FlipperWeaponCatalog
{
    private static FlipperWeaponDefinition[] _cached;

    public static FlipperWeaponDefinition[] GetAll()
    {
        if (_cached != null) return _cached;
        _cached = Resources.LoadAll<FlipperWeaponDefinition>("FlipperWeapons");
        if (_cached == null) _cached = System.Array.Empty<FlipperWeaponDefinition>();
        System.Array.Sort(_cached, (a, b) =>
        {
            int ta = a != null ? (int)a.weaponType : 99;
            int tb = b != null ? (int)b.weaponType : 99;
            return ta.CompareTo(tb);
        });
        return _cached;
    }

    public static FlipperWeaponDefinition Get(string weaponId)
    {
        if (string.IsNullOrEmpty(weaponId)) return null;
        foreach (var w in GetAll())
        {
            if (w != null && w.weaponId == weaponId)
                return w;
        }
        return null;
    }

    public static FlipperWeaponDefinition GetDefault() =>
        Get(RunLoadout.DefaultFlipperWeaponId) ?? (GetAll().Length > 0 ? GetAll()[0] : null);

    public static IReadOnlyList<FlipperWeaponDefinition> GetLoadoutWeapons() => GetAll();
}
