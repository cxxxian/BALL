using UnityEngine;

/// <summary>
/// 协议宝箱粒子：蓄力尘 → 开盖爆发 → 光柱 → 彩带（皇室战争式节奏）。
/// </summary>
public class CrateOpenVfx : MonoBehaviour
{
    public static CrateOpenVfx Instance { get; private set; }

    [SerializeField] private Camera targetCamera;
    [SerializeField] private float worldZ = 2f;

    private ParticleSystem _charge;
    private ParticleSystem _burst;
    private ParticleSystem _beam;
    private ParticleSystem _confetti;
    private Transform _anchor;

    private void Awake()
    {
        Instance = this;
        if (targetCamera == null)
            targetCamera = Camera.main;
        EnsureSystems();
        StopAll();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static CrateOpenVfx EnsureExists()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("CrateOpenVfx");
        return go.AddComponent<CrateOpenVfx>();
    }

    private void EnsureSystems()
    {
        if (_anchor == null)
        {
            var anchorGo = new GameObject("CrateVfxAnchor");
            anchorGo.transform.SetParent(transform, false);
            _anchor = anchorGo.transform;
        }

        if (_charge == null)
        {
            _charge = CreateSystem("ChargeDust", 64, looping: true, duration: 2f);
            ConfigureCharge(_charge);
        }
        if (_burst == null)
        {
            _burst = CreateSystem("OpenBurst", 96, looping: false, duration: 0.6f);
            ConfigureBurst(_burst);
        }
        if (_beam == null)
        {
            _beam = CreateSystem("LightBeam", 48, looping: false, duration: 0.8f);
            ConfigureBeam(_beam);
        }
        if (_confetti == null)
        {
            _confetti = CreateSystem("Confetti", 80, looping: false, duration: 1.2f);
            ConfigureConfetti(_confetti);
        }

        PlaceAtScreenCenter();
    }

    private ParticleSystem CreateSystem(string name, int max, bool looping, float duration)
    {
        var go = new GameObject(name);
        go.transform.SetParent(_anchor, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = looping;
        main.duration = duration;
        main.maxParticles = max;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingOrder = 90;
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Particles/Standard Unlit")
                     ?? Shader.Find("Sprites/Default");
        if (shader != null)
            renderer.material = new Material(shader);

        return ps;
    }

    private static void ConfigureCharge(ParticleSystem ps)
    {
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.9f);
        main.startColor = new Color(1f, 0.92f, 0.35f, 0.85f);
        main.gravityModifier = -0.08f;

        var emission = ps.emission;
        emission.rateOverTime = 22f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.55f, 0.35f, 0.1f);
    }

    private static void ConfigureBurst(ParticleSystem ps)
    {
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.28f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7.5f);
        main.startColor = Color.white;
        main.gravityModifier = 0.12f;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 64, 88) });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 0.08f;
    }

    private static void ConfigureBeam(ParticleSystem ps)
    {
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.35f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 8f);
        main.startColor = new Color(1f, 1f, 0.85f, 0.9f);
        main.gravityModifier = -0.05f;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 24, 36) });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 8f;
        shape.radius = 0.05f;
        shape.rotation = new Vector3(-90f, 0f, 0f);
    }

    private static void ConfigureConfetti(ParticleSystem ps)
    {
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Gradient
            {
                colorKeys = new[]
                {
                    new GradientColorKey(new Color(0f, 1f, 1f), 0f),
                    new GradientColorKey(new Color(1f, 0f, 1f), 0.5f),
                    new GradientColorKey(new Color(1f, 1f, 0f), 1f)
                },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                }
            });
        main.gravityModifier = 0.35f;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 40, 55) });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 35f;
        shape.radius = 0.15f;
        shape.rotation = new Vector3(-90f, 0f, 0f);
    }

    public void PlaceAtScreenCenter()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
        if (targetCamera == null || _anchor == null) return;

        Vector3 world = targetCamera.ViewportToWorldPoint(new Vector3(0.5f, 0.46f, worldZ));
        _anchor.position = world;
    }

    public void PlayCharge(Color accent)
    {
        EnsureSystems();
        PlaceAtScreenCenter();
        StopAllExceptCharge();
        SetStartColor(_charge, accent);
        _charge.Play(true);
    }

    public void PlayOpen(Color rarityColor, Color ballColor)
    {
        EnsureSystems();
        PlaceAtScreenCenter();
        _charge.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        Color mix = Color.Lerp(rarityColor, ballColor, 0.25f);
        mix.a = 1f;
        _burst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _beam.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        SetStartColor(_burst, Color.Lerp(mix, Color.white, 0.55f));
        SetStartColor(_beam, Color.Lerp(mix, new Color(1f, 1f, 0.7f), 0.4f));
        _burst.Play(true);
        _beam.Play(true);
    }

    public void PlayConfetti(Color rarityColor)
    {
        EnsureSystems();
        PlaceAtScreenCenter();
        _confetti.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        SetStartColor(_confetti, rarityColor);
        _confetti.Play(true);
    }

    public void StopAll()
    {
        if (_charge != null) _charge.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (_burst != null) _burst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (_beam != null) _beam.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (_confetti != null) _confetti.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void StopAllExceptCharge()
    {
        if (_burst != null) _burst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (_beam != null) _beam.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (_confetti != null) _confetti.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private static void SetStartColor(ParticleSystem ps, Color c)
    {
        if (ps == null) return;
        var main = ps.main;
        c.a = Mathf.Clamp01(c.a <= 0.01f ? 0.9f : c.a);
        main.startColor = c;
    }
}
