using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// URP Renderer Feature：Boss 击杀后的定向色散后处理（仅 VFXDirector 激活时生效）
/// </summary>
public class DirectionalCAFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Shader shader = null;
        public float maxIntensity = 1.0f;
    }

    public Settings settings = new Settings();
    private DirectionalCAPass _pass;
    private Material _material;

    public override void Create()
    {
        if (settings.shader == null)
        {
            Debug.LogWarning("DirectionalCAFeature: Shader is not assigned!");
            return;
        }

        _material = CoreUtils.CreateEngineMaterial(settings.shader);
        _pass = new DirectionalCAPass(_material, settings.maxIntensity);
        _pass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_pass == null || _material == null) return;
        if (!_pass.TryPrepare(ref renderingData)) return;
        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        _pass?.Dispose();
        if (_material != null)
            CoreUtils.Destroy(_material);
    }

    class DirectionalCAPass : ScriptableRenderPass
    {
        private readonly Material _material;
        private readonly float _maxIntensity;
        private RTHandle _tempRT;
        private float _intensity;

        public DirectionalCAPass(Material material, float maxIntensity)
        {
            _material = material;
            _maxIntensity = maxIntensity;
            ConfigureInput(ScriptableRenderPassInput.Color);
        }

        public bool TryPrepare(ref RenderingData renderingData)
        {
            // 仅在 Boss 击杀特效窗口内启用，普通碰撞/高速移动不触发
            if (!VFXDirector.IsChromaticAberrationActive)
            {
                _intensity = 0f;
                return false;
            }

            _intensity = ComputeIntensity(renderingData.cameraData.camera);
            return _intensity > 0.001f;
        }

        private float ComputeIntensity(Camera camera)
        {
            var ball = BallController.Instance;
            if (ball == null) return 0f;

            Vector3 ballScreenPos = camera.WorldToViewportPoint(ball.transform.position);
            Vector2 velocity = ball.Rb.velocity;
            Vector2 velocityDir = velocity.sqrMagnitude > 0.0001f
                ? velocity.normalized
                : Vector2.up;

            _material.SetVector("_BallScreenPos", new Vector2(ballScreenPos.x, ballScreenPos.y));
            _material.SetVector("_BallVelocityDir", velocityDir);
            _material.SetFloat("_CAIntensity", _maxIntensity);
            return _maxIntensity;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            RenderingUtils.ReAllocateIfNeeded(ref _tempRT, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_DirectionalCATemp");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_material == null || _intensity <= 0.001f) return;

            CommandBuffer cmd = CommandBufferPool.Get("DirectionalCA");
            var cameraTarget = renderingData.cameraData.renderer.cameraColorTargetHandle;

            Blitter.BlitCameraTexture(cmd, cameraTarget, _tempRT, _material, 0);
            Blitter.BlitCameraTexture(cmd, _tempRT, cameraTarget);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            _tempRT?.Release();
            _tempRT = null;
        }
    }
}
