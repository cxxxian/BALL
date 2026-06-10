using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// URP Renderer Feature：常驻 CRT + 开场事件扫描（After Post Processing）
/// </summary>
public class ArcadeCRTFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Shader shader = null;
    }

    public Settings settings = new Settings();
    private ArcadeCRTPass _pass;
    private Material _material;

    public override void Create()
    {
        if (settings.shader == null)
        {
            Debug.LogWarning("ArcadeCRTFeature: Shader is not assigned!");
            return;
        }

        _material = CoreUtils.CreateEngineMaterial(settings.shader);
        _pass = new ArcadeCRTPass(_material);
        _pass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
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

    class ArcadeCRTPass : ScriptableRenderPass
    {
        private readonly Material _material;
        private RTHandle _tempRT;

        public ArcadeCRTPass(Material material)
        {
            _material = material;
            ConfigureInput(ScriptableRenderPassInput.Color);
        }

        public bool TryPrepare(ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType != CameraType.Game)
                return false;

            if (!ArcadeCRTController.TryGetRuntimeState(out var state))
                return false;

            if (state.ScanlineOpacity <= 0f
                && state.EffectiveVignette <= 0f
                && state.EventMaster <= 0f)
                return false;

            _material.SetFloat("_ScanlineOpacity", state.ScanlineOpacity);
            _material.SetFloat("_ScanlineCount", state.ScanlineCount);
            _material.SetFloat("_ScanlineWidth", state.ScanlineWidth);
            _material.SetFloat("_VignetteStrength", state.EffectiveVignette);
            _material.SetFloat("_VignettePower", state.VignettePower);
            _material.SetFloat("_VignetteRoundness", state.VignetteRoundness);
            _material.SetFloat("_EventMaster", state.EventMaster);
            _material.SetFloat("_EventHeadY", state.EventHeadY);
            _material.SetFloat("_EventTime", state.EventTime);
            _material.SetFloat("_EventLineIntensity", state.EventLineIntensity);
            _material.SetFloat("_EventWakePx", state.EventWakePx);
            _material.SetFloat("_EventRevealDim", state.EventRevealDim);
            _material.SetFloat("_EventInteractBoost", state.EventInteractBoost);
            _material.SetColor("_EventColor", state.EventColor);

            return true;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            RenderingUtils.ReAllocateIfNeeded(ref _tempRT, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_ArcadeCRTTemp");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_material == null) return;

            CommandBuffer cmd = CommandBufferPool.Get("ArcadeCRT");
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
