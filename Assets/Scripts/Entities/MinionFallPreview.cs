using UnityEngine;

/// <summary>小兵贴近底线时显示脚底 → 底线的淡竖向预测线（&lt;1.5 世界单位）。</summary>
[DisallowMultipleComponent]
public class MinionFallPreview : MonoBehaviour
{
    private const float ShowDistance = 1.5f;
    private const float FallbackBottomLineY = -8.5f;

    private EnemyBase _enemy;
    private LineRenderer _line;
    private SpriteRenderer _sr;

    private void Awake()
    {
        _enemy = GetComponent<EnemyBase>();
        _sr    = GetComponent<SpriteRenderer>();
        BuildLine();
        _line.enabled = false;
    }

    private void BuildLine()
    {
        _line = gameObject.AddComponent<LineRenderer>();
        _line.positionCount  = 2;
        _line.useWorldSpace  = true;
        _line.startWidth     = 0.04f;
        _line.endWidth       = 0.04f;
        _line.sortingOrder   = 1;
        _line.material       = new Material(Shader.Find("Sprites/Default"));
        _line.textureMode    = LineTextureMode.Stretch;
    }

    private void LateUpdate()
    {
        if (_enemy == null || _line == null)
        {
            if (_line != null) _line.enabled = false;
            return;
        }

        if (!_enemy.checkBottomLine || _enemy.IsDead)
        {
            _line.enabled = false;
            return;
        }

        float checkY = GetCheckY();
        float dist   = _enemy.transform.position.y - checkY;
        if (dist >= ShowDistance || dist < 0f)
        {
            _line.enabled = false;
            return;
        }

        float footY = GetFootY();
        float x     = _enemy.transform.position.x;

        _line.SetPosition(0, new Vector3(x, footY, 0f));
        _line.SetPosition(1, new Vector3(x, checkY, 0f));

        Color baseCol = NeonColors.Active.GetBase(NeonRole.SkillShield);
        _line.startColor = new Color(baseCol.r, baseCol.g, baseCol.b, 0.25f);
        _line.endColor   = new Color(baseCol.r, baseCol.g, baseCol.b, 0.08f);
        _line.enabled    = true;
    }

    private float GetCheckY()
    {
        if (BlockShield.Instance != null && BlockShield.Instance.IsActive)
            return BlockShield.Instance.shieldY;

        var cfg = GameManager.Instance != null ? GameManager.Instance.config : null;
        return cfg != null ? cfg.minionBottomLineY : FallbackBottomLineY;
    }

    private float GetFootY()
    {
        if (_sr != null && _sr.sprite != null)
            return _sr.bounds.min.y;
        return _enemy.transform.position.y;
    }
}
