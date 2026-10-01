using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>Phase 6 独立验收台。复用 BuffSandbox 的球与服务，敌人全部走正式 Minion 路径。</summary>
public sealed class EnemySandboxHarness : MonoBehaviour
{
    public MinionDefinition[] enemyDefinitions;
    public int waveIndex = 3;
    public float spawnY = 5.8f;
    public bool bottomDamagesPlayer;

    private BuffSandboxHarness _base;
    private Minion _lastSpawned;
    private int _selected;
    private Vector2 _scroll;
    private readonly Queue<string> _events = new Queue<string>();
    private LineRenderer _jamZoneLine;

    private void OnEnable() => Phase6EnemyBehaviour.EventRaised += RecordEvent;

    private void RecordEvent(string message)
    {
        _events.Enqueue($"{Time.time:0.0}  {message}");
        while (_events.Count > 9) _events.Dequeue();
    }

    private void Start()
    {
        _base = FindObjectOfType<BuffSandboxHarness>();
        if (_base != null)
        {
            _base.autoSpawn = false;
            _base.showControls = false;
        }
        if (SkillManager.Instance == null)
            new GameObject("EnemySandbox_SkillManager").AddComponent<SkillManager>();
        CreateJamZoneLine();
#if UNITY_EDITOR
        if (enemyDefinitions == null || enemyDefinitions.Length == 0)
        {
            string[] names = { "Minion_Grunt", "Minion_Armored", "Minion_Bomber", "Minion_Splitter", "Minion_Mini", "Minion_Charger", "Minion_Conductor", "Minion_Jammer" };
            enemyDefinitions = new MinionDefinition[names.Length];
            for (int i = 0; i < names.Length; i++)
                enemyDefinitions[i] = AssetDatabase.LoadAssetAtPath<MinionDefinition>($"Assets/ScriptableObjects/Enemies/{names[i]}.asset");
        }
#endif
    }

    private void OnDisable()
    {
        Phase6EnemyBehaviour.EventRaised -= RecordEvent;
        Phase6SplitSpawn.CancelAll();
    }

    private void CreateJamZoneLine()
    {
        var cfg = GameManager.Instance != null ? GameManager.Instance.config : null;
        float height = cfg != null ? cfg.worldHeight : 16f;
        float y = MinionLineRules.GetAttackLineY() + height * 0.35f;
        var go = new GameObject("JammerActivationBoundary");
        _jamZoneLine = go.AddComponent<LineRenderer>();
        _jamZoneLine.material = new Material(Shader.Find("Sprites/Default"));
        _jamZoneLine.useWorldSpace = true;
        _jamZoneLine.positionCount = 2;
        _jamZoneLine.SetPosition(0, new Vector3(-4.3f, y, 0f));
        _jamZoneLine.SetPosition(1, new Vector3(4.3f, y, 0f));
        _jamZoneLine.startWidth = _jamZoneLine.endWidth = 0.025f;
        _jamZoneLine.startColor = _jamZoneLine.endColor = new Color(0.8f, 0.35f, 1f, 0.7f);
        _jamZoneLine.sortingOrder = 1;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.C)) ClearEnemies();
    }

    private Minion Spawn(int index, Vector3 pos)
    {
        if (enemyDefinitions == null || index < 0 || index >= enemyDefinitions.Length || enemyDefinitions[index] == null) return null;
        var enemy = Phase6EnemyFactory.Spawn(enemyDefinitions[index], pos, Mathf.Max(0, waveIndex));
        if (enemy != null) enemy.damageToPlayer = bottomDamagesPlayer ? 1 : 0;
        _lastSpawned = enemy;
        return enemy;
    }

    private void ClearEnemies()
    {
        Phase6SplitSpawn.CancelAll();
        foreach (var enemy in FindObjectsOfType<EnemyBase>())
            if (enemy != null) Destroy(enemy.gameObject);
        _lastSpawned = null;
    }

    private void SpawnMixed()
    {
        ClearEnemies();
        int[] indices = { 3, 5, 6, 7, 0, 1, 2 };
        for (int i = 0; i < indices.Length; i++)
            Spawn(indices[i], new Vector3(-3.1f + i * 1.03f, spawnY + (i % 2) * 0.8f, 0f));
    }

    private void ApplyBuff(string assetName)
    {
        var manager = BuffManager.Instance;
        if (manager == null || _base == null) return;
        foreach (var buff in _base.buffCatalog)
        {
            if (buff == null || buff.name != assetName) continue;
            if (!manager.buffPool.Contains(buff)) manager.buffPool.Add(buff);
            manager.ApplyBuff(buff);
            return;
        }
    }

    private void SimulateBallHit()
    {
        if (_lastSpawned == null || _lastSpawned.IsDead) return;
        _lastSpawned.GetComponent<Phase6EnemyBehaviour>()?.OnMainBallContact();
        _lastSpawned.TakeHit(1, true, _lastSpawned.transform.position);
    }

    private void FreezeLast()
    {
        if (_lastSpawned == null || _lastSpawned.IsDead) return;
        var frost = _lastSpawned.GetComponent<EnemyFrostState>();
        if (frost == null) frost = _lastSpawned.gameObject.AddComponent<EnemyFrostState>();
        frost.ApplyShortFreeze(0.8f);
    }

    private void IgniteLinked()
    {
        ElectricCombat.DevChargeAll(2);
        foreach (var conductor in FindObjectsOfType<Phase6EnemyBehaviour>())
        {
            if (conductor.Kind != MinionSpecialType.Conductor) continue;
            foreach (var enemy in FindObjectsOfType<Minion>())
            {
                if (enemy == null || enemy.IsDead || enemy.gameObject == conductor.gameObject) continue;
                if ((enemy.transform.position - conductor.transform.position).sqrMagnitude > 2.5f * 2.5f) continue;
                ElectricCombat.TrySparkChain(enemy, enemy.transform.position, false, "Sandbox", 2.5f, 2, 1);
                return;
            }
        }
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(8, 8, 380, Screen.height - 16), GUI.skin.box);
        _scroll = GUILayout.BeginScrollView(_scroll);
        GUILayout.Label("PHASE 6 · ENEMY SANDBOX");
        GUILayout.Label("LMB: aim ball  |  RMB/B: reset ball  |  C: clear");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Wave −")) waveIndex = Mathf.Max(0, waveIndex - 1);
        GUILayout.Label($"W{waveIndex + 1}", GUILayout.Width(48));
        if (GUILayout.Button("Wave +")) waveIndex++;
        GUILayout.EndHorizontal();

        if (enemyDefinitions != null)
        {
            for (int i = 0; i < enemyDefinitions.Length; i++)
            {
                var def = enemyDefinitions[i];
                if (def == null) continue;
                if (GUILayout.Button(($"{(i == _selected ? "▶ " : "")} {def.minionName}  HP {def.maxHP}  T {def.threatCost:0.0}")))
                    _selected = i;
            }
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Spawn")) Spawn(_selected, new Vector3(Random.Range(-2.5f, 2.5f), spawnY, 0f));
        if (GUILayout.Button("Near bottom")) Spawn(_selected, new Vector3(0f, MinionLineRules.GetAttackLineY() + 0.8f, 0f));
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Mixed ×7")) SpawnMixed();
        if (GUILayout.Button("Clear")) ClearEnemies();
        GUILayout.EndHorizontal();
        if (GUILayout.Button(bottomDamagesPlayer ? "Bottom damage: ON" : "Bottom damage: OFF"))
        {
            bottomDamagesPlayer = !bottomDamagesPlayer;
            foreach (var enemy in FindObjectsOfType<Minion>()) enemy.damageToPlayer = bottomDamagesPlayer ? 1 : 0;
        }

        GUILayout.Label("Deterministic checks (last spawned)");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Ball hit")) SimulateBallHit();
        if (GUILayout.Button("Freeze 0.8s")) FreezeLast();
        GUILayout.EndHorizontal();
        if (GUILayout.Button("Charge all + ignite linked")) IgniteLinked();

        GUILayout.Label("Build checks");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Electric")) { ApplyBuff("Buff_ElectricCharge"); ApplyBuff("Buff_ElectricIgniter"); }
        if (GUILayout.Button("Frost")) { ApplyBuff("Buff_FrostMark"); ApplyBuff("Buff_FrostBurst"); }
        if (GUILayout.Button("Combo")) { ApplyBuff("Buff_ComboMomentum"); ApplyBuff("Buff_ComboOverload"); }
        GUILayout.EndHorizontal();
        if (GUILayout.Button("Clear Buffs")) BuffManager.Instance?.ClearAllBuffsForTest();

        var skills = SkillManager.Instance;
        if (skills != null && skills.slots != null && skills.slots.Length >= 2)
        {
            GUILayout.Label($"Jammer ×{Phase6EnemyBehaviour.JammerComboMultiplier:0.0}  CD {skills.slots[0].currentCD:0.00} / {skills.slots[1].currentCD:0.00}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Set both CD=10"))
                foreach (var slot in skills.slots) slot.currentCD = 10f;
            if (GUILayout.Button("Combo tick")) ComboSystem.Instance?.onComboChanged.Invoke(1);
            GUILayout.EndHorizontal();
        }

        var active = FindObjectsOfType<Minion>();
        GUILayout.Label($"Active: {active.Length}");
        foreach (var enemy in active)
        {
            if (enemy == null || enemy.IsDead) continue;
            var special = enemy.GetComponent<Phase6EnemyBehaviour>();
            GUILayout.Label($"{enemy.definition.minionName,-11} {enemy.CurrentHits}/{enemy.maxHits}  {(special != null ? special.DebugState : "Normal")}");
        }
        GUILayout.Label("Recent events");
        foreach (var entry in _events) GUILayout.Label(entry);
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }
}
