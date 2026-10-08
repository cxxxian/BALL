using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

/// <summary>2D UI output routed through the scene camera so URP Bloom can process emitter cores.</summary>
[DefaultExecutionOrder(-100), RequireComponent(typeof(UIDocument))]
public sealed class SlotVisualLabV2Bloom : MonoBehaviour
{
    public Camera outputCamera;
    public Material displayMaterial;
    public Volume bloomVolume;
    [Range(0, 4)] public float emissionStrength = 2.2f;
    [Range(0, 1)] public float bloomIntensity = .4f;
    [Range(.5f, 2f)] public float bloomThreshold = 1f;
    [Range(0, 1)] public float bloomScatter = .55f;
    public bool bloomEnabled = true;
    private UIDocument document;
    private PanelSettings savedPanel, livePanel;
    private RenderTexture uiTexture;
    private Material liveMaterial;
    private GameObject surface;
    private Bloom bloom;

    public RenderTexture UITexture => uiTexture;

    private void OnEnable()
    {
        document = GetComponent<UIDocument>();
        if (outputCamera == null || displayMaterial == null || document.panelSettings == null) return;
        savedPanel = document.panelSettings;
        livePanel = Instantiate(savedPanel);
        livePanel.name = "Slot V2 live UI output";
        livePanel.hideFlags = HideFlags.DontSave;
        livePanel.SetScreenToPanelSpaceFunction(p => new Vector2(p.x * uiTexture.width / Mathf.Max(1, Screen.width), p.y * uiTexture.height / Mathf.Max(1, Screen.height)));
        liveMaterial = new Material(displayMaterial) { hideFlags = HideFlags.DontSave };
        surface = GameObject.CreatePrimitive(PrimitiveType.Quad);
        surface.name = "Live UI 2D display";
        surface.hideFlags = HideFlags.DontSave;
        surface.layer = 5;
        Destroy(surface.GetComponent<Collider>());
        surface.GetComponent<MeshRenderer>().sharedMaterial = liveMaterial;
        if (bloomVolume != null) bloomVolume.profile.TryGet(out bloom);
        Resize();
        document.panelSettings = livePanel;
    }

    private void Start() => GetComponent<SlotVisualLabV2Controller>().Rebuild();

    private void LateUpdate()
    {
        if (livePanel == null) return;
        Resize();
        liveMaterial.SetFloat("_EmissionStrength", emissionStrength);
        if (bloom != null)
        {
            bloom.active = bloomEnabled;
            bloom.intensity.Override(bloomIntensity);
            bloom.threshold.Override(bloomThreshold);
            bloom.scatter.Override(bloomScatter);
        }
    }

    private void Resize()
    {
        int width = outputCamera.targetTexture != null ? outputCamera.targetTexture.width : Mathf.Max(1, Screen.width);
        int height = outputCamera.targetTexture != null ? outputCamera.targetTexture.height : Mathf.Max(1, Screen.height);
        float downscale = Mathf.Min(1, 1920f / Mathf.Max(width, height));
        width = Mathf.Max(1, Mathf.RoundToInt(width * downscale));
        height = Mathf.Max(1, Mathf.RoundToInt(height * downscale));
        if (uiTexture == null || uiTexture.width != width || uiTexture.height != height)
        {
            if (uiTexture != null) { uiTexture.Release(); Destroy(uiTexture); }
            uiTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "Slot V2 UI texture", hideFlags = HideFlags.DontSave,
                filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            uiTexture.Create();
            livePanel.targetTexture = uiTexture;
            liveMaterial.SetTexture("_BaseMap", uiTexture);
        }
        float cameraHeight = outputCamera.orthographic ? outputCamera.orthographicSize * 2
            : 20 * Mathf.Tan(outputCamera.fieldOfView * Mathf.Deg2Rad * .5f);
        surface.transform.position = outputCamera.transform.position + outputCamera.transform.forward * 10;
        surface.transform.rotation = outputCamera.transform.rotation;
        surface.transform.localScale = new Vector3(cameraHeight * width / height, cameraHeight, 1);
    }

    public void ToggleBloom() => bloomEnabled = !bloomEnabled;

    private void OnDisable()
    {
        if (document != null && savedPanel != null) document.panelSettings = savedPanel;
        if (uiTexture != null) { uiTexture.Release(); Destroy(uiTexture); }
        if (surface != null) Destroy(surface);
        if (liveMaterial != null) Destroy(liveMaterial);
        if (livePanel != null) Destroy(livePanel);
        livePanel = null;
    }
}
