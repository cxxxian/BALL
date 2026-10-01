using System.Collections.Generic;
using UnityEngine;

/// <summary>Phase 6 四种特殊小兵的共享状态机；挂在正式 Minion 上，也供 EnemySandbox 使用。</summary>
public sealed class Phase6EnemyBehaviour : MonoBehaviour
{
    public static System.Action<string> EventRaised;
    public static void Report(string message) => EventRaised?.Invoke(message);
    private enum ActionState { Normal, Telegraph, Dash, Recovery, Stunned, Arming, Active }

    private static readonly HashSet<Phase6EnemyBehaviour> ActiveJammers = new HashSet<Phase6EnemyBehaviour>();
    private static readonly HashSet<Phase6EnemyBehaviour> Conductors = new HashSet<Phase6EnemyBehaviour>();
    private static readonly Collider2D[] Nearby = new Collider2D[40];
    private static readonly Dictionary<MinionSpecialType, Sprite> Markers = new Dictionary<MinionSpecialType, Sprite>();
    private static Material _lineMaterial;

    private Minion _minion;
    private Rigidbody2D _rb;
    private EnemyFrostState _frost;
    private MinionSpecialType _kind;
    private ActionState _state;
    private float _timer;
    private float _nextAction;
    private float _nextOverload;
    private float _bottomGraceUntil;
    private SpriteRenderer _markerRenderer;
    private Minion[] _links = new Minion[2];
    private LineRenderer[] _linkLines;

    public string DebugState => _state.ToString();
    public MinionSpecialType Kind => _kind;
    public bool SuppressBottomCheck => _kind == MinionSpecialType.Mini && Time.time < _bottomGraceUntil;
    public static float JammerComboMultiplier
    {
        get
        {
            foreach (var jammer in ActiveJammers)
                if (jammer != null && jammer.isActiveAndEnabled && jammer._minion != null && !jammer._minion.IsDead && !jammer._minion.FrozenForWaveClear && jammer._state == ActionState.Active)
                    return 0.5f;
            return 1f;
        }
    }

    public void Initialize(Minion minion)
    {
        _minion = minion;
        _rb = GetComponent<Rigidbody2D>();
        _frost = GetComponent<EnemyFrostState>();
        _kind = minion.definition.specialType;
        _state = ActionState.Normal;
        _nextAction = _kind == MinionSpecialType.Charger ? 2.5f : 3.2f;
        if (_kind == MinionSpecialType.Mini) _bottomGraceUntil = Time.time + 0.35f;
        if (_kind == MinionSpecialType.Conductor) Conductors.Add(this);
        ApplyPlaceholderScale();
        CreateMarker();
        if (_kind == MinionSpecialType.Conductor) CreateLinkLines();
        Report($"Spawn {_kind} W{minion.SpawnWaveIndex + 1}");
    }

    private void Update()
    {
        if (_minion == null || _minion.IsDead || _minion.FrozenForWaveClear) return;
        if (GameManager.Instance == null || !GameManager.Instance.IsWaveSimActive()) return;
        if (_frost == null) _frost = GetComponent<EnemyFrostState>();
        float dt = Time.deltaTime * (TimestopAura.Instance != null ? TimestopAura.Instance.GetMinionSpeedScale() : 1f);
        if (_frost != null && _frost.IsFrozen)
        {
            if (_kind == MinionSpecialType.Jammer) UpdateJammer(0f);
            UpdateMarker();
            return;
        }
        switch (_kind)
        {
            case MinionSpecialType.Charger: UpdateCharger(dt); break;
            case MinionSpecialType.Conductor: UpdateConductor(dt); break;
            case MinionSpecialType.Jammer: UpdateJammer(dt); break;
        }
        if (_kind == MinionSpecialType.Conductor) UpdateLinkLines();
        UpdateMarker();
    }

    public bool TryGetSpecialVelocity(out Vector2 velocity)
    {
        velocity = Vector2.zero;
        if (_frost != null && _frost.IsFrozen) return true;
        if (_kind == MinionSpecialType.Charger)
        {
            if (_state == ActionState.Telegraph || _state == ActionState.Recovery) return true;
            if (_state == ActionState.Dash)
            {
                float slow = TimestopAura.Instance != null ? TimestopAura.Instance.GetMinionSpeedScale() : 1f;
                velocity = Vector2.down * 2.2f * slow;
                return true;
            }
        }
        if (_kind == MinionSpecialType.Conductor && (_state == ActionState.Telegraph || _state == ActionState.Stunned)) return true;
        return false;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (_minion == null || _minion.IsDead || !collision.gameObject.CompareTag("Ball")) return;
        if (collision.gameObject.GetComponent<BallController>() == null) return;
        OnMainBallContact();
    }

    /// <summary>测试面板可模拟主球命中；真实碰撞也使用同一打断规则。</summary>
    public void OnMainBallContact()
    {
        if (_minion == null || _minion.IsDead) return;
        if (_kind == MinionSpecialType.Charger && (_state == ActionState.Telegraph || _state == ActionState.Dash))
        {
            _state = ActionState.Recovery;
            _timer = 1f;
            _rb.velocity = Vector2.zero;
            Report("Charger interrupted by main ball");
        }
        else if (_kind == MinionSpecialType.Conductor && _state == ActionState.Telegraph)
            StunConductor();
    }

    private void UpdateCharger(float dt)
    {
        if (_state == ActionState.Normal)
        {
            _nextAction -= dt;
            if (_nextAction <= 0f && transform.position.y > MinionLineRules.GetAttackLineY() + 1.2f)
            {
                _state = ActionState.Telegraph;
                _timer = 0.65f;
                Report("Charger telegraph");
            }
            return;
        }
        _timer -= dt;
        if (_timer > 0f) return;
        switch (_state)
        {
            case ActionState.Telegraph: _state = ActionState.Dash; _timer = 0.35f; Report("Charger dash"); break;
            case ActionState.Dash: _state = ActionState.Recovery; _timer = 0.90f; Report("Charger recovery"); break;
            case ActionState.Recovery: _state = ActionState.Normal; _nextAction = 4f; break;
        }
    }

    private void UpdateConductor(float dt)
    {
        if (_state == ActionState.Stunned)
        {
            _timer -= dt;
            if (_timer <= 0f) { _state = ActionState.Normal; _nextAction = 3.2f; }
            return;
        }
        if (_state == ActionState.Telegraph)
        {
            _timer -= dt;
            if (_timer <= 0f)
            {
                int pushed = 0;
                for (int i = 0; i < _links.Length; i++)
                    if (IsValidLink(_links[i])) { PushLinkedTarget(_links[i]); pushed++; }
                Report($"Conductor pulse: {pushed} targets");
                _state = ActionState.Normal;
                _nextAction = 3.2f;
            }
            return;
        }
        SelectLinks();
        _nextAction -= dt;
        if (_nextAction <= 0f && (_links[0] != null || _links[1] != null))
        {
            _state = ActionState.Telegraph;
            _timer = 0.60f;
            Report("Conductor telegraph");
        }
    }

    private void SelectLinks()
    {
        _links[0] = _links[1] = null;
        float first = 2.5f * 2.5f, second = first;
        int count = Physics2D.OverlapCircleNonAlloc(transform.position, 2.5f, Nearby);
        for (int i = 0; i < count; i++)
        {
            if (Nearby[i] == null || Nearby[i].gameObject == gameObject) continue;
            var target = Nearby[i].GetComponent<Minion>();
            if (!IsValidLink(target)) continue;
            float d = (target.transform.position - transform.position).sqrMagnitude;
            if (d < first)
            {
                second = first; _links[1] = _links[0];
                first = d; _links[0] = target;
            }
            else if (d < second)
            {
                second = d; _links[1] = target;
            }
        }
    }

    private bool IsValidLink(Minion target)
    {
        if (target == null || target.IsDead || target.FrozenForWaveClear) return false;
        var frost = target.GetComponent<EnemyFrostState>();
        if (frost != null && frost.IsFrozen) return false;
        if ((target.transform.position - transform.position).sqrMagnitude > 2.5f * 2.5f) return false;
        if (target.definition != null && target.definition.specialType == MinionSpecialType.Conductor) return false;
        var special = target.GetComponent<Phase6EnemyBehaviour>();
        return special == null || special._kind != MinionSpecialType.Charger ||
            (special._state != ActionState.Telegraph && special._state != ActionState.Dash && special._state != ActionState.Recovery);
    }

    private static void PushLinkedTarget(Minion target)
    {
        var stamp = target.GetComponent<Phase6PushStamp>();
        if (stamp == null) stamp = target.gameObject.AddComponent<Phase6PushStamp>();
        if (stamp.lastFrame == Time.frameCount) return;
        stamp.lastFrame = Time.frameCount;
        var body = target.GetComponent<Rigidbody2D>();
        if (body == null) return;
        Vector2 pos = body.position;
        float targetY = Mathf.Min(pos.y, Mathf.Max(pos.y - 0.35f, MinionLineRules.GetAttackLineY() + 0.55f));
        float distance = pos.y - targetY;
        if (distance <= 0f) return;
        var hits = Physics2D.CircleCastAll(pos, 0.35f, Vector2.down, distance);
        foreach (var hit in hits)
        {
            if (hit.collider == null || hit.collider.gameObject == target.gameObject || hit.collider.isTrigger || hit.collider.CompareTag("Enemy") || hit.collider.CompareTag("Ball")) continue;
            targetY = Mathf.Max(targetY, pos.y - Mathf.Max(0f, hit.distance - 0.02f));
        }
        body.position = new Vector2(pos.x, targetY);
    }

    private void StunConductor()
    {
        _state = ActionState.Stunned;
        _timer = 1f;
        _links[0] = _links[1] = null;
        if (_rb != null) _rb.velocity = Vector2.zero;
        Report("Conductor interrupted");
    }

    public static void NotifyEffectiveIgnition(EnemyBase target)
    {
        foreach (var conductor in new List<Phase6EnemyBehaviour>(Conductors))
        {
            if (conductor == null || conductor._minion == null || conductor._minion.IsDead || Time.time < conductor._nextOverload) continue;
            if (conductor._links[0] != target && conductor._links[1] != target) continue;
            if (!conductor.IsValidLink(target as Minion)) continue;
            conductor._nextOverload = Time.time + 1.5f;
            conductor.StunConductor();
            conductor._minion.TakeHit(1, false, conductor.transform.position);
            Report("Conductor electric overload");
        }
    }

    private void UpdateJammer(float dt)
    {
        var cfg = GameManager.Instance != null ? GameManager.Instance.config : null;
        float height = cfg != null ? cfg.worldHeight : 16f;
        bool inZone = transform.position.y <= MinionLineRules.GetAttackLineY() + height * 0.35f;
        if (!inZone)
        {
            if (_state == ActionState.Active) Report("Jammer inactive");
            _state = ActionState.Normal;
            ActiveJammers.Remove(this);
            return;
        }
        if (_state == ActionState.Normal)
        {
            _state = ActionState.Arming;
            _timer = 0.40f;
            Report("Jammer arming");
        }
        else if (_state == ActionState.Arming)
        {
            _timer -= dt;
            if (_timer <= 0f)
            {
                _state = ActionState.Active;
                ActiveJammers.Add(this);
                Report("Jammer active: Combo CD ×0.5");
            }
        }
    }

    public void OnKilled()
    {
        ActiveJammers.Remove(this);
        Conductors.Remove(this);
        Report($"Killed {_kind}");
        if (_kind == MinionSpecialType.Splitter && !(_minion != null && _minion.FrozenForWaveClear))
            Phase6SplitSpawn.Schedule(_minion.definition.splitChild, transform.position, _minion.SpawnWaveIndex, 0.18f);
    }

    private void OnDisable()
    {
        ActiveJammers.Remove(this);
        Conductors.Remove(this);
    }

    private void ApplyPlaceholderScale()
    {
        float scale = _kind == MinionSpecialType.Splitter ? 1.15f : _kind == MinionSpecialType.Mini ? 0.65f : 1f;
        transform.localScale *= scale;
    }

    private void CreateMarker()
    {
        if (!Markers.TryGetValue(_kind, out var sprite))
            Markers[_kind] = sprite = MakeMarker(_kind);
        var marker = new GameObject("Phase6IdentityMarker");
        marker.transform.SetParent(transform, false);
        marker.transform.localPosition = new Vector3(0f, 0f, -0.01f);
        var sr = marker.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = Color.white;
        sr.sortingOrder = 4;
        _markerRenderer = sr;
    }

    private void UpdateMarker()
    {
        if (_markerRenderer == null) return;
        bool warning = _state == ActionState.Telegraph || _state == ActionState.Arming;
        bool active = _state == ActionState.Dash || _state == ActionState.Active;
        _markerRenderer.color = warning
            ? Color.Lerp(Color.white, new Color(1f, 0.2f, 0.15f), 0.5f + 0.5f * Mathf.Sin(Time.time * 18f))
            : active ? new Color(1f, 0.2f, 0.12f) : Color.white;
        float pulse = warning ? 1f + 0.18f * Mathf.Sin(Time.time * 18f) : 1f;
        _markerRenderer.transform.localScale = Vector3.one * pulse;
    }

    private static Sprite MakeMarker(MinionSpecialType kind)
    {
        const int size = 32;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float xx = (x - 15.5f) / 16f, yy = (y - 15.5f) / 16f;
            bool on = false;
            switch (kind)
            {
                case MinionSpecialType.Splitter: on = Mathf.Abs(xx) < 0.11f && Mathf.Abs(yy) < 0.8f; break;
                case MinionSpecialType.Mini: on = xx * xx + yy * yy < 0.18f; break;
                case MinionSpecialType.Charger: on = yy < 0.75f && yy > -0.65f && Mathf.Abs(xx) < (0.8f - yy) * 0.46f; break;
                case MinionSpecialType.Conductor: on = Mathf.Abs(xx) < 0.12f || Mathf.Abs(yy) < 0.12f; break;
                case MinionSpecialType.Jammer: on = Mathf.Abs(xx * xx + yy * yy - 0.45f) < 0.11f; break;
            }
            pixels[y * size + x] = on ? Color.white : Color.clear;
        }
        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 45f);
    }

    private void CreateLinkLines()
    {
        if (_lineMaterial == null) _lineMaterial = new Material(Shader.Find("Sprites/Default"));
        _linkLines = new LineRenderer[2];
        for (int i = 0; i < 2; i++)
        {
            var line = new GameObject($"ConductorLink{i}").AddComponent<LineRenderer>();
            line.transform.SetParent(transform, false);
            line.material = _lineMaterial;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = line.endWidth = 0.045f;
            line.startColor = line.endColor = new Color(0.45f, 0.9f, 1f, 0.8f);
            line.sortingOrder = 3;
            line.enabled = false;
            _linkLines[i] = line;
        }
    }

    private void UpdateLinkLines()
    {
        if (_linkLines == null) return;
        for (int i = 0; i < 2; i++)
        {
            var line = _linkLines[i];
            bool visible = IsValidLink(_links[i]);
            line.enabled = visible;
            if (!visible) continue;
            line.SetPosition(0, transform.position);
            line.SetPosition(1, _links[i].transform.position);
        }
    }
}

/// <summary>防止多个 Conductor 在同一帧重复推动同一目标。</summary>
public sealed class Phase6PushStamp : MonoBehaviour
{
    public int lastFrame = -1;
}
