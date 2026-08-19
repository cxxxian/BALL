using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "RunCatalog", menuName = "Ball/RunCatalog")]
public class RunCatalog : ScriptableObject
{
    public List<BallDefinition> balls = new List<BallDefinition>();
    public List<SkillDefinition> skills = new List<SkillDefinition>();

    private static RunCatalog _cached;

    public static RunCatalog Load()
    {
        if (_cached != null) return _cached;
        _cached = Resources.Load<RunCatalog>("RunCatalog");
        if (_cached == null)
            Debug.LogError("[RunCatalog] Missing Resources/RunCatalog.asset");
        return _cached;
    }

    public BallDefinition GetBall(string ballId)
    {
        if (string.IsNullOrEmpty(ballId) || balls == null) return null;
        foreach (var b in balls)
        {
            if (b != null && b.ballId == ballId) return b;
        }
        return null;
    }

    public SkillDefinition GetSkill(string skillId)
    {
        if (string.IsNullOrEmpty(skillId) || skills == null) return null;
        foreach (var s in skills)
        {
            if (s != null && s.skillId == skillId) return s;
        }
        return null;
    }

    public List<SkillDefinition> GetAvailableSkills()
    {
        var list = new List<SkillDefinition>();
        if (skills == null) return list;
        foreach (var s in skills)
        {
            if (s != null && s.isAvailable
                && s.implementationType != ActiveSkillType.TestPlaceholder)
                list.Add(s);
        }
        return list;
    }

    public List<BallDefinition> GetAvailableBalls()
    {
        var list = new List<BallDefinition>();
        if (balls == null) return list;
        foreach (var b in balls)
        {
            if (b != null && b.isAvailableInLoadout) list.Add(b);
        }
        return list;
    }

    public BallDefinition GetDefaultBall()
    {
        var available = GetAvailableBalls();
        if (available.Count > 0) return available[0];
        return balls != null && balls.Count > 0 ? balls[0] : null;
    }

    public SkillDefinition GetDefaultSkill(int index)
    {
        var available = GetAvailableSkills();
        if (index >= 0 && index < available.Count) return available[index];
        return null;
    }
}
