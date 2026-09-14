using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 以 SampleScene 为底板，用 Bumper / Portal / BoostGear / Prism / Cannon / SpringBoard
/// 编排不同可玩地图。菜单：Ball → Build Mechanism Maps
/// </summary>
public static class BuildMechanismMaps
{
    const string SrcScene = "Assets/Scenes/SampleScene.unity";
    const string OutFolder = "Assets/Scenes/Maps";

    [MenuItem("Ball/Build Mechanism Maps")]
    public static void BuildAll()
    {
        if (!File.Exists(SrcScene))
        {
            Debug.LogError("[BuildMechanismMaps] 找不到 SampleScene");
            return;
        }

        if (!Directory.Exists(OutFolder))
        {
            Directory.CreateDirectory(OutFolder);
            AssetDatabase.Refresh();
        }

        // 先复制再改机关，保留完整玩法系统
        BuildOne("Map_BumperCluster", LayoutBumperCluster);
        BuildOne("Map_PortalCircuit", LayoutPortalCircuit);
        BuildOne("Map_OrbitFarm", LayoutOrbitFarm);
        BuildOne("Map_DivertorRails", LayoutDivertorRails);
        BuildOne("Map_PlayfulToys", LayoutPlayfulToys);

        // 清理已弃用的棱镜/炮塔地图
        AssetDatabase.DeleteAsset($"{OutFolder}/Map_PrismCross.unity");
        AssetDatabase.DeleteAsset($"{OutFolder}/Map_CannonBastion.unity");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[BuildMechanismMaps] 完成 → {OutFolder}");
    }

    static void BuildOne(string sceneName, System.Action layout)
    {
        string path = $"{OutFolder}/{sceneName}.unity";
        AssetDatabase.DeleteAsset(path);
        if (!AssetDatabase.CopyAsset(SrcScene, path))
        {
            Debug.LogError($"[BuildMechanismMaps] 复制失败: {path}");
            return;
        }

        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        ClearMechanisms();
        layout();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[BuildMechanismMaps] {sceneName} OK");
    }

    // ── 清掉可重编排的机关（保留墙、挡板、Slingshot、系统物体）──
    static void ClearMechanisms()
    {
        DestroyAll<Bumper>();
        DestroyAll<BoostGear>();
        DestroyAll<Portal>();
        DestroyAll<ReflectivePrism>();
        DestroyAll<EnergyCannon>();
        DestroyAll<SpringBoard>();
        DestroyAll<OrbitRing>();
        DestroyAll<FlipDivertor>();
        DestroyAll<SlideRail>();
        DestroyAll<ProtocolCachePlate>();
    }

    static void DestroyAll<T>() where T : Component
    {
        foreach (var c in Object.FindObjectsOfType<T>())
            Object.DestroyImmediate(c.gameObject);
    }

    // ═══════════════════════════════════════════════════════
    //  Map 1：Bumper 密巢 — 上三角 + 中菱形 + 双侧加速
    // ═══════════════════════════════════════════════════════
    static void LayoutBumperCluster()
    {
        // 上三角
        MakeBumper("Bumper_T1", new Vector2(-1.9f, 5.1f));
        MakeBumper("Bumper_T2", new Vector2(0f, 5.7f));
        MakeBumper("Bumper_T3", new Vector2(1.9f, 5.1f));

        // 中菱形（更密）
        MakeBumper("Bumper_M1", new Vector2(-2.8f, 3.1f));
        MakeBumper("Bumper_M2", new Vector2(2.8f, 3.1f));
        MakeBumper("Bumper_M3", new Vector2(-1.5f, 1.8f));
        MakeBumper("Bumper_M4", new Vector2(1.5f, 1.8f));
        MakeBumper("Bumper_MC", new Vector2(0f, 2.6f));

        // 下桥
        MakeBumper("Bumper_B1", new Vector2(-2.2f, 0.4f));
        MakeBumper("Bumper_B2", new Vector2(2.2f, 0.4f));
        MakeBumper("Bumper_BC", new Vector2(0f, -0.6f));

        MakeBoostGear("BoostGear_Left", new Vector2(-3.5f, 1.2f));
        MakeBoostGear("BoostGear_Right", new Vector2(3.5f, 1.2f));

        MakeSpringBoard("SpringBoard_Left", new Vector2(-3.88f, -8.35f), new Vector2(0.55f, 1f));
        MakeSpringBoard("SpringBoard_Right", new Vector2(3.88f, -8.35f), new Vector2(-0.55f, 1f));
    }

    // ═══════════════════════════════════════════════════════
    //  Map 2：传送回路 — 两对 Portal + 出口齿轮 + 少量 Bumper
    // ═══════════════════════════════════════════════════════
    static void LayoutPortalCircuit()
    {
        var a = MakePortal("Portal_A", new Vector2(-3.0f, -2.2f), new Color(0f, 0.7f, 2.2f, 1f));
        var b = MakePortal("Portal_B", new Vector2(2.8f, 5.0f), new Color(0f, 0.7f, 2.2f, 1f));
        LinkPortals(a, b);

        var c = MakePortal("Portal_C", new Vector2(3.0f, -2.2f), new Color(0.35f, 1.3f, 2f, 1f));
        var d = MakePortal("Portal_D", new Vector2(-2.8f, 5.0f), new Color(0.35f, 1.3f, 2f, 1f));
        LinkPortals(c, d);

        // 出口加速
        MakeBoostGear("BoostGear_Exit_TR", new Vector2(2.0f, 3.6f));
        MakeBoostGear("BoostGear_Exit_TL", new Vector2(-2.0f, 3.6f));

        // 回路中途缓冲 Bumper
        MakeBumper("Bumper_N", new Vector2(0f, 3.0f));
        MakeBumper("Bumper_S", new Vector2(0f, -0.4f));
        MakeBumper("Bumper_W", new Vector2(-2.1f, 1.2f));
        MakeBumper("Bumper_E", new Vector2(2.1f, 1.2f));

        MakeSpringBoard("SpringBoard_Left", new Vector2(-3.88f, -8.35f), new Vector2(0.55f, 1f));
        MakeSpringBoard("SpringBoard_Right", new Vector2(3.88f, -8.35f), new Vector2(-0.55f, 1f));
    }

    // ═══════════════════════════════════════════════════════
    //  Map 3：环轨农场 — Orbit 加速甩出 + 少量 Bumper
    // ═══════════════════════════════════════════════════════
    static void LayoutOrbitFarm()
    {
        MakeOrbit("Orbit_Mid", new Vector2(0f, 2.2f), 1.4f, 1.35f);
        MakeOrbit("Orbit_High", new Vector2(0f, 5.4f), 1.1f, 1.0f);

        MakeBoostGear("BoostGear_Feed_L", new Vector2(-3.3f, 0.2f));
        MakeBoostGear("BoostGear_Feed_R", new Vector2(3.3f, 0.2f));

        MakeBumper("Bumper_L", new Vector2(-2.4f, 3.6f));
        MakeBumper("Bumper_R", new Vector2(2.4f, 3.6f));
        MakeBumper("Bumper_BL", new Vector2(-2.0f, -0.6f));
        MakeBumper("Bumper_BR", new Vector2(2.0f, -0.6f));

        // 环轨旁缓存盘：压过充能 → 再压领脉冲；另一盘一压武装领锁
        MakeCachePlate("Cache_Pulse", new Vector2(0f, -1.8f));
        MakeCachePlate("Cache_Lock", new Vector2(3.0f, 4.0f));

        MakeSpringBoard("SpringBoard_Left", new Vector2(-3.88f, -8.35f), new Vector2(0.55f, 1f));
        MakeSpringBoard("SpringBoard_Right", new Vector2(3.88f, -8.35f), new Vector2(-0.55f, 1f));
    }

    // ═══════════════════════════════════════════════════════
    //  Map 4：分流 + 滑轨 — 主动选路 + 观光骑行
    // ═══════════════════════════════════════════════════════
    static void LayoutDivertorRails()
    {
        MakeDivertor(
            "Divertor_Core",
            intake: new Vector2(0f, 0.6f),
            exitL: new Vector2(-2.8f, 3.2f),
            exitR: new Vector2(2.8f, 3.2f));

        MakeSlideRail(
            "Rail_LeftClimb",
            entry: new Vector2(-3.2f, -1.2f),
            exit: new Vector2(-1.2f, 5.8f),
            control: new Vector2(-3.6f, 3.0f));

        MakeSlideRail(
            "Rail_RightClimb",
            entry: new Vector2(3.2f, -1.2f),
            exit: new Vector2(1.2f, 5.8f),
            control: new Vector2(3.6f, 3.0f));

        MakeBumper("Bumper_C", new Vector2(0f, 4.2f));
        MakeBumper("Bumper_L", new Vector2(-2.0f, 1.8f));
        MakeBumper("Bumper_R", new Vector2(2.0f, 1.8f));

        MakeCachePlate("Cache_DivertorCD", new Vector2(0f, -1.6f), chargeDuration: 5.5f);

        MakeSpringBoard("SpringBoard_Left", new Vector2(-3.88f, -8.35f), new Vector2(0.55f, 1f));
        MakeSpringBoard("SpringBoard_Right", new Vector2(3.88f, -8.35f), new Vector2(-0.55f, 1f));
    }

    // ═══════════════════════════════════════════════════════
    //  Map 5：玩具箱 — Portal + Orbit + Divertor + Rail 合演
    // ═══════════════════════════════════════════════════════
    static void LayoutPlayfulToys()
    {
        var a = MakePortal("Portal_A", new Vector2(-3.1f, -2.4f), new Color(0f, 0.7f, 2.2f, 1f));
        var b = MakePortal("Portal_B", new Vector2(2.6f, 4.8f), new Color(0f, 0.7f, 2.2f, 1f));
        LinkPortals(a, b);

        MakeOrbit("Orbit_Center", new Vector2(0f, 1.6f), 1.25f, 1.2f);

        MakeDivertor(
            "Divertor_Top",
            intake: new Vector2(0f, 3.8f),
            exitL: new Vector2(-2.4f, 6.2f),
            exitR: new Vector2(2.4f, 6.2f));

        MakeSlideRail(
            "Rail_SideSweep",
            entry: new Vector2(3.2f, -0.8f),
            exit: new Vector2(-2.8f, 5.2f),
            control: new Vector2(0.5f, 6.5f));

        MakeBoostGear("Boost_L", new Vector2(-3.4f, 0.6f));
        MakeBumper("Bumper_Soft", new Vector2(2.2f, 0.4f));

        MakeCachePlate("Cache_Score", new Vector2(-1.6f, -0.2f));
        MakeCachePlate("Cache_Skill", new Vector2(1.8f, 2.8f));
        MakeCachePlate("Cache_Lock", new Vector2(0f, 5.6f), chargeDuration: 5f);

        MakeSpringBoard("SpringBoard_Left", new Vector2(-3.88f, -8.35f), new Vector2(0.55f, 1f));
        MakeSpringBoard("SpringBoard_Right", new Vector2(3.88f, -8.35f), new Vector2(-0.55f, 1f));
    }

    // ═══════════════════════════════════════════════════════
    //  Factories（结构对齐 SampleScene 现有机关）
    // ═══════════════════════════════════════════════════════
    static GameObject MakeBumper(string name, Vector2 pos)
    {
        var go = new GameObject(name);
        go.transform.position = pos;

        var col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.38f;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;

        var sr = go.AddComponent<SpriteRenderer>();
        var color = NeonColors.Active != null
            ? NeonColors.Active.GetBase(NeonRole.Bumper)
            : new Color(0f, 1.8f, 2.5f, 1f);
        sr.sprite = CyberVisualFactory.CreateBumperSprite(color);
        sr.color = color;
        sr.material = CyberVisualFactory.UnlitMaterial;
        sr.sortingOrder = 5;

        go.AddComponent<Bumper>();
        return go;
    }

    static GameObject MakeBoostGear(string name, Vector2 pos)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(0.9f, 0.9f, 1f);

        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.5f;

        var visual = new GameObject("Visual");
        visual.transform.SetParent(go.transform, false);
        var sr = visual.AddComponent<SpriteRenderer>();
        var color = new Color(2f, 1.7f, 0f, 1f);
        sr.sprite = CyberVisualFactory.CreateBoostGearSprite(color);
        sr.color = color;
        sr.material = CyberVisualFactory.UnlitMaterial;
        sr.sortingOrder = 6;

        go.AddComponent<BoostGear>();
        return go;
    }

    static GameObject MakePortal(string name, Vector2 pos, Color color)
    {
        var go = new GameObject(name);
        go.transform.position = pos;

        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.55f;

        var visual = new GameObject("Visual");
        visual.transform.SetParent(go.transform, false);
        var sr = visual.AddComponent<SpriteRenderer>();
        sr.sprite = CyberVisualFactory.CreatePortalSprite(color);
        sr.color = color;
        sr.material = CyberVisualFactory.UnlitMaterial;
        sr.sortingOrder = 6;

        var portal = go.AddComponent<Portal>();
        portal.portalColor = color;
        return go;
    }

    static void LinkPortals(GameObject a, GameObject b)
    {
        var pa = a.GetComponent<Portal>();
        var pb = b.GetComponent<Portal>();
        pa.partnerPortal = pb;
        pb.partnerPortal = pa;
        EditorUtility.SetDirty(pa);
        EditorUtility.SetDirty(pb);
    }

    static GameObject MakePrism(string name, Vector2 pos, Vector2 reflectDir)
    {
        var go = new GameObject(name);
        go.transform.position = pos;

        var col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.4f;

        var visual = new GameObject("Visual");
        visual.transform.SetParent(go.transform, false);
        var sr = visual.AddComponent<SpriteRenderer>();
        var color = new Color(1.4f, 0f, 2f, 1f);
        sr.sprite = CyberVisualFactory.CreatePrismSprite(color);
        sr.color = color;
        sr.material = CyberVisualFactory.UnlitMaterial;
        sr.sortingOrder = 6;

        go.AddComponent<LineRenderer>();
        var prism = go.AddComponent<ReflectivePrism>();
        prism.reflectDirection = reflectDir;
        return go;
    }

    static GameObject MakeCannon(string name, Vector2 intakePos, Vector2 muzzlePos, Vector2 defaultAim, float exitSpeed)
    {
        var root = new GameObject(name);

        // ── Intake 进洞（仅此处有 Trigger）──
        var intake = new GameObject("Intake");
        intake.transform.SetParent(root.transform);
        intake.transform.position = intakePos;

        var intakeCol = intake.AddComponent<CircleCollider2D>();
        intakeCol.isTrigger = true;
        intakeCol.radius = 0.42f;

        var intakeVis = new GameObject("Visual");
        intakeVis.transform.SetParent(intake.transform, false);
        intakeVis.transform.localScale = new Vector3(0.75f, 0.75f, 1f);
        var intakeSr = intakeVis.AddComponent<SpriteRenderer>();
        intakeSr.sprite = CyberVisualFactory.CreatePortalSprite(new Color(0.7f, 0.28f, 0f, 1f));
        intakeSr.color = new Color(0.55f, 0.22f, 0.02f, 0.9f);
        intakeSr.material = CyberVisualFactory.UnlitMaterial;
        intakeSr.sortingOrder = 5;

        // ── Muzzle 炮口（无 Trigger，球只从这里被射出）──
        var muzzle = new GameObject("Muzzle");
        muzzle.transform.SetParent(root.transform);
        muzzle.transform.position = muzzlePos;

        var muzzleVis = new GameObject("Visual");
        muzzleVis.transform.SetParent(muzzle.transform, false);
        muzzleVis.transform.localScale = new Vector3(1.1f, 1.1f, 1f);
        var muzzleSr = muzzleVis.AddComponent<SpriteRenderer>();
        var color = new Color(1.2f, 0.6f, 0f, 1f);
        muzzleSr.sprite = CyberVisualFactory.CreateCannonSprite(color);
        muzzleSr.color = color;
        muzzleSr.material = CyberVisualFactory.UnlitMaterial;
        muzzleSr.sortingOrder = 7;

        var cannon = root.AddComponent<EnergyCannon>();
        cannon.intake = intake.transform;
        cannon.muzzle = muzzle.transform;
        cannon.defaultAim = defaultAim;
        cannon.exitDirection = defaultAim;
        cannon.exitSpeed = exitSpeed;
        cannon.aimConeHalfAngle = 80f;

        intake.AddComponent<CannonIntakeZone>().owner = cannon;
        return root;
    }

    static GameObject MakeSpringBoard(string name, Vector2 pos, Vector2 launchDir)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(0.85f, 0.32f, 1f);

        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;

        var visual = new GameObject("Visual");
        visual.transform.SetParent(go.transform, false);
        var sr = visual.AddComponent<SpriteRenderer>();
        var color = new Color(0f, 2f, 0.6f, 1f);
        sr.sprite = CyberVisualFactory.CreateSpringBoardSprite(color);
        sr.color = color;
        sr.material = CyberVisualFactory.UnlitMaterial;
        sr.sortingOrder = 4;

        var sb = go.AddComponent<SpringBoard>();
        sb.launchDirection = launchDir;
        return go;
    }

    static GameObject MakeOrbit(string name, Vector2 pos, float radius, float revolutions)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var orbit = go.AddComponent<OrbitRing>();
        orbit.radius = radius;
        orbit.revolutions = revolutions;
        return go;
    }

    static GameObject MakeCachePlate(
        string name,
        Vector2 pos,
        float chargeDuration = 6f)
    {
        var go = new GameObject(name);
        go.transform.position = pos;

        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.85f;

        var plate = go.AddComponent<ProtocolCachePlate>();
        plate.chargeDuration = chargeDuration;
        plate.radius = 0.85f;
        return go;
    }

    static GameObject MakeDivertor(string name, Vector2 intake, Vector2 exitL, Vector2 exitR)
    {
        var root = new GameObject(name);

        var inGo = new GameObject("Intake");
        inGo.transform.SetParent(root.transform);
        inGo.transform.position = intake;
        var inCol = inGo.AddComponent<CircleCollider2D>();
        inCol.isTrigger = true;
        inCol.radius = 0.4f;
        var inVis = new GameObject("Visual");
        inVis.transform.SetParent(inGo.transform, false);
        var inSr = inVis.AddComponent<SpriteRenderer>();
        inSr.sprite = CyberVisualFactory.CreatePortalSprite(new Color(1.5f, 0.7f, 0.1f, 1f));
        inSr.color = new Color(1.2f, 0.55f, 0.05f, 0.9f);
        inSr.material = CyberVisualFactory.UnlitMaterial;
        inSr.sortingOrder = 5;
        inVis.transform.localScale = Vector3.one * 0.7f;

        var lGo = new GameObject("Exit_L");
        lGo.transform.SetParent(root.transform);
        lGo.transform.position = exitL;
        var rGo = new GameObject("Exit_R");
        rGo.transform.SetParent(root.transform);
        rGo.transform.position = exitR;

        var div = root.AddComponent<FlipDivertor>();
        div.intake = inGo.transform;
        div.exitLeft = lGo.transform;
        div.exitRight = rGo.transform;
        inGo.AddComponent<DivertorIntakeZone>().owner = div;
        return root;
    }

    static GameObject MakeSlideRail(string name, Vector2 entry, Vector2 exit, Vector2 control)
    {
        var root = new GameObject(name);

        var eGo = new GameObject("Entry");
        eGo.transform.SetParent(root.transform);
        eGo.transform.position = entry;
        var eCol = eGo.AddComponent<CircleCollider2D>();
        eCol.isTrigger = true;
        eCol.radius = 0.38f;
        var eVis = new GameObject("Visual");
        eVis.transform.SetParent(eGo.transform, false);
        var eSr = eVis.AddComponent<SpriteRenderer>();
        eSr.sprite = CyberVisualFactory.CreatePortalSprite(new Color(1.6f, 0.3f, 1.8f, 1f));
        eSr.color = new Color(1.2f, 0.2f, 1.4f, 0.85f);
        eSr.material = CyberVisualFactory.UnlitMaterial;
        eSr.sortingOrder = 5;
        eVis.transform.localScale = Vector3.one * 0.65f;

        var xGo = new GameObject("Exit");
        xGo.transform.SetParent(root.transform);
        xGo.transform.position = exit;

        var cGo = new GameObject("Control");
        cGo.transform.SetParent(root.transform);
        cGo.transform.position = control;

        var rail = root.AddComponent<SlideRail>();
        rail.entry = eGo.transform;
        rail.exit = xGo.transform;
        rail.control = cGo.transform;
        eGo.AddComponent<SlideRailEntryZone>().owner = rail;
        return root;
    }
}
