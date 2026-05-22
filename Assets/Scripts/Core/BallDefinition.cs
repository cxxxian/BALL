using UnityEngine;

public enum BallType
{
    Thunder,
    Fire,
    Ice,
    Water,
    Shadow
}

[CreateAssetMenu(fileName = "BallDefinition", menuName = "PinballGame/BallDefinition")]
public class BallDefinition : ScriptableObject
{
    public BallType ballType = BallType.Thunder;
    public GameObject activeSkillPrefab;
    public Color trailColor = new Color(0.5f, 0.2f, 1f);
    public Color glowColor = new Color(0.3f, 0.7f, 1f);
    public float skillCooldown = 12f;
    [TextArea(2, 4)]
    public string skillDescription = "8道闪电弹射至最近敌人，各造成3次伤害";
}
