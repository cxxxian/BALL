using UnityEditor;
using UnityEngine;
using System.IO;

public class CreateLevelAssets : MonoBehaviour
{
    [MenuItem("Ball/Create Level Assets")]
    public static void CreateAssets()
    {
        // 1. 确保目标文件夹存在
        string dirPath = "Assets/ScriptableObjects/Levels";
        if (!Directory.Exists(dirPath))
        {
            Directory.CreateDirectory(dirPath);
            AssetDatabase.Refresh();
        }

        // 2. 加载 Boss 配置文件
        BossDefinition bossW1 = AssetDatabase.LoadAssetAtPath<BossDefinition>("Assets/ScriptableObjects/Enemies/Boss_W1.asset");
        BossDefinition bossW2 = AssetDatabase.LoadAssetAtPath<BossDefinition>("Assets/ScriptableObjects/Enemies/Boss_W2.asset");

        if (bossW1 == null || bossW2 == null)
        {
            Debug.LogError("[CreateLevelAssets] 未找到 Boss_W1.asset 或 Boss_W2.asset，请确保其路径位于 Assets/ScriptableObjects/Enemies/ 下。");
            return;
        }

        // 3. 创建 Level 1
        string pathL1 = Path.Combine(dirPath, "Level_01.asset");
        LevelDefinition lvl1 = AssetDatabase.LoadAssetAtPath<LevelDefinition>(pathL1);
        if (lvl1 == null)
        {
            lvl1 = ScriptableObject.CreateInstance<LevelDefinition>();
            lvl1.levelID = 1;
            lvl1.levelName = "石巨像之怒";
            lvl1.description = "石巨像苏醒！它在场地上方徘徊，不断派遣装甲兵压迫底线。碰撞两侧的反射棱镜，可以形成在 BOSS 身后的高频反弹狂暴连穿！";
            lvl1.bossDef = bossW1;
            lvl1.isUnlockedByDefault = true;

            AssetDatabase.CreateAsset(lvl1, pathL1);
            Debug.Log($"[CreateLevelAssets] 成功创建: {pathL1}");
        }

        // 4. 创建 Level 2
        string pathL2 = Path.Combine(dirPath, "Level_02.asset");
        LevelDefinition lvl2 = AssetDatabase.LoadAssetAtPath<LevelDefinition>(pathL2);
        if (lvl2 == null)
        {
            lvl2 = ScriptableObject.CreateInstance<LevelDefinition>();
            lvl2.levelID = 2;
            lvl2.levelName = "电磁核心之灾";
            lvl2.description = "第二波次：电磁核心。它派遣更多的爆弹兵。一旦爆弹兵触底，全场 Bumper 都会被瘫痪 5 秒！必须优先用弹珠拦截爆弹兵！";
            lvl2.bossDef = bossW2;
            lvl2.isUnlockedByDefault = false;

            AssetDatabase.CreateAsset(lvl2, pathL2);
            Debug.Log($"[CreateLevelAssets] 成功创建: {pathL2}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[CreateLevelAssets] 所有关卡 ScriptableObject 资源创建/刷新完毕！");
    }
}
