using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

/// <summary>2D UI output routed through the scene camera so URP Bloom can process emitter cores.</summary>
[DefaultExecutionOrder(-100), RequireComponent(typeof(UIDocument))]
public sealed class SlotVisualLabV2Bloom : MonoBehaviour
{
    public bool gameplayOutput;
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
    private UIDocument compositor;
    private PanelSettings compositorPanel;
    private RenderTexture gameplayTexture;
    private Image compositeImage;
    private GameObject gameplayCameraHost;
    private VolumeProfile ownedProfile;
    private Coroutine backdropCapture;
    private Image backdropImage;
    private GameObject backdropSurface;
    private Material backdropMaterial, gaussianMaterial;
    private RenderTexture blurredBackdrop;
    private bool backdropReady;
    [Range(.15f, .75f)] public float backdropBrightness = .15f;
    public bool IsBackdropReady => !gameplayOutput || backdropReady;

    public RenderTexture UITexture => uiTexture;
    public VisualElement GameplayOverlayRoot => compositor != null ? compositor.rootVisualElement : null;
    public void SetCabinetFrameVisible(bool visible)
    {
        if (compositeImage != null) compositeImage.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void OnEnable()
    {
        document = GetComponent<UIDocument>();
        if (gameplayOutput) PrepareGameplayOutput();
        if (outputCamera == null || displayMaterial == null || document.panelSettings == null) return;
        savedPanel = document.panelSettings;
        livePanel = Instantiate(savedPanel);
        livePanel.name = "Slot V2 live UI output";
        livePanel.hideFlags = HideFlags.DontSave;
        livePanel.clearColor = true;
        livePanel.colorClearValue = Color.clear;
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

    private void Start() { if (!gameplayOutput) GetComponent<SlotVisualLabV2Controller>().Rebuild(); }

    private void LateUpdate()
    {
        if (livePanel == null) return;
        if (gameplayOutput) { ResizeGameplayOutput(); ResizeBackdropSurface(); }
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

    private void PrepareGameplayOutput()
    {
        gameplayCameraHost = new GameObject("Slot presentation camera") { hideFlags = HideFlags.DontSave };
        gameplayCameraHost.transform.position = new Vector3(10000, 10000, -10);
        outputCamera = gameplayCameraHost.AddComponent<Camera>();
        outputCamera.orthographic = true;
        outputCamera.orthographicSize = 5;
        outputCamera.cullingMask = 1 << 5;
        outputCamera.clearFlags = CameraClearFlags.SolidColor;
        outputCamera.backgroundColor = Color.clear;
        outputCamera.allowHDR = true;
        outputCamera.enabled = false;
        var data = outputCamera.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        data.volumeLayerMask = 1 << 31;
        var volumeObject = new GameObject("Slot presentation bloom") { hideFlags = HideFlags.DontSave, layer = 31 };
        volumeObject.transform.SetParent(gameplayCameraHost.transform, false);
        bloomVolume = volumeObject.AddComponent<Volume>();
        bloomVolume.isGlobal = true;
        ownedProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        ownedProfile.hideFlags = HideFlags.DontSave;
        ownedProfile.Add<Bloom>(true);
        bloomVolume.sharedProfile = ownedProfile;
        var compositorObject = new GameObject("Slot presentation overlay") { hideFlags = HideFlags.DontSave };
        compositor = compositorObject.AddComponent<UIDocument>();
        compositorPanel = Instantiate(document.panelSettings);
        compositorPanel.name = "Slot screen compositor";
        compositorPanel.hideFlags = HideFlags.DontSave;
        compositorPanel.targetTexture = null;
        // This panel overlays the main camera; clearing it erases the game even when hidden.
        compositorPanel.clearColor = false;
        compositorPanel.clearDepthStencil = false;
        compositorPanel.sortingOrder = document.panelSettings.sortingOrder + 1000;
        compositor.panelSettings = compositorPanel;
        compositor.sortingOrder = document.sortingOrder + 1000;
        var root = compositor.rootVisualElement;
        root.pickingMode = PickingMode.Ignore;
        root.style.flexGrow = 1;
        root.style.display = DisplayStyle.None;
        compositeImage = new Image { pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.StretchToFill };
        compositeImage.style.position = Position.Absolute;
        compositeImage.style.left = compositeImage.style.top = compositeImage.style.right = compositeImage.style.bottom = 0;
        backdropImage = new Image { pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.StretchToFill };
        backdropImage.style.position = Position.Absolute;
        backdropImage.style.left = backdropImage.style.top = backdropImage.style.right = backdropImage.style.bottom = 0;
        root.Add(backdropImage);
        root.Add(compositeImage);
        backdropSurface = GameObject.CreatePrimitive(PrimitiveType.Quad);
        backdropSurface.name = "Slot / blurred scene backdrop";
        backdropSurface.hideFlags = HideFlags.DontSave;
        backdropSurface.layer = 5;
        Destroy(backdropSurface.GetComponent<Collider>());
        backdropMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { hideFlags = HideFlags.DontSave };
        backdropSurface.GetComponent<MeshRenderer>().sharedMaterial = backdropMaterial;
        backdropSurface.SetActive(false);
        var gaussian = Resources.Load<Shader>("SlotBackdropGaussian");
        if (gaussian != null) gaussianMaterial = new Material(gaussian) { hideFlags = HideFlags.DontSave };
        ResizeGameplayOutput();
    }

    private void ResizeGameplayOutput()
    {
        int width = Mathf.Max(1, Screen.width), height = Mathf.Max(1, Screen.height);
        if (gameplayTexture != null && gameplayTexture.width == width && gameplayTexture.height == height) return;
        if (gameplayTexture != null) { gameplayTexture.Release(); Destroy(gameplayTexture); }
        gameplayTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGBHalf)
        { name = "Slot composited frame", hideFlags = HideFlags.DontSave };
        gameplayTexture.Create();
        outputCamera.targetTexture = gameplayTexture;
        compositeImage.image = gameplayTexture;
    }

    public void SetGameplayVisible(bool visible)
    {
        if (!gameplayOutput) return;
        if (outputCamera != null) outputCamera.enabled = visible;
        if (backdropCapture != null) { StopCoroutine(backdropCapture); backdropCapture = null; }
        var compositorRoot = GameplayOverlayRoot;
        if (!visible)
        {
            if (compositorRoot != null) compositorRoot.style.display = DisplayStyle.None;
            ReleaseBackdrop();
            return;
        }
        if (compositorRoot == null) return;
        // Capture before displaying any cabinet or transfer sprites. The gameplay is already paused.
        compositorRoot.style.display = DisplayStyle.None;
        backdropReady = false;
        backdropCapture = StartCoroutine(CaptureBackdrop());
    }

    private IEnumerator CaptureBackdrop()
    {
        yield return new WaitForEndOfFrame();
        Texture2D snapshot = ScreenCapture.CaptureScreenshotAsTexture();
        if (snapshot != null)
        {
            int width = Mathf.Max(1, snapshot.width / 2), height = Mathf.Max(1, snapshot.height / 2);
            var scratch = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            scratch.filterMode = FilterMode.Bilinear;
            scratch.wrapMode = TextureWrapMode.Clamp;
            blurredBackdrop = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            { name = "Slot / Gaussian scene snapshot", hideFlags = HideFlags.DontSave,
                filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            blurredBackdrop.Create();
            var previousTarget = RenderTexture.active;
            Graphics.Blit(snapshot, blurredBackdrop);
            if (gaussianMaterial != null)
                for (int pass = 0; pass < 2; pass++)
                {
                    gaussianMaterial.SetVector("_Direction", new Vector4(1.5f, 0, 0, 0));
                    Graphics.Blit(blurredBackdrop, scratch, gaussianMaterial, 0);
                    gaussianMaterial.SetVector("_Direction", new Vector4(0, 1.5f, 0, 0));
                    Graphics.Blit(scratch, blurredBackdrop, gaussianMaterial, 0);
                }
            if (gaussianMaterial != null)
            {
                gaussianMaterial.SetFloat("_Brightness", backdropBrightness);
                Graphics.Blit(blurredBackdrop, scratch, gaussianMaterial, 1);
                Graphics.Blit(scratch, blurredBackdrop);
            }
            RenderTexture.active = previousTarget;
            RenderTexture.ReleaseTemporary(scratch);
            Destroy(snapshot);
            backdropImage.image = blurredBackdrop;
            backdropImage.tintColor = Color.white;
            backdropMaterial.SetTexture("_BaseMap", blurredBackdrop);
            backdropMaterial.SetColor("_BaseColor", Color.white);
            backdropSurface.SetActive(true);
            ResizeBackdropSurface();
        }
        backdropReady = true;
        backdropCapture = null;
        var root = GameplayOverlayRoot;
        if (root != null) root.style.display = DisplayStyle.Flex;
    }

    private void ResizeBackdropSurface()
    {
        if (backdropSurface == null || outputCamera == null) return;
        float height = outputCamera.orthographic ? outputCamera.orthographicSize * 2 :
            40 * Mathf.Tan(outputCamera.fieldOfView * Mathf.Deg2Rad * .5f);
        backdropSurface.transform.position = outputCamera.transform.position + outputCamera.transform.forward * 20;
        backdropSurface.transform.rotation = outputCamera.transform.rotation;
        backdropSurface.transform.localScale = new Vector3(height * outputCamera.aspect, height, 1);
    }

    private void ReleaseBackdrop()
    {
        backdropReady = false;
        if (backdropImage != null) backdropImage.image = null;
        if (backdropMaterial != null) backdropMaterial.SetTexture("_BaseMap", Texture2D.blackTexture);
        if (backdropSurface != null) backdropSurface.SetActive(false);
        if (blurredBackdrop != null) { blurredBackdrop.Release(); Destroy(blurredBackdrop); blurredBackdrop = null; }
    }

    public void ToggleBloom() => bloomEnabled = !bloomEnabled;

    private void OnDisable()
    {
        SetGameplayVisible(false);
        if (document != null && savedPanel != null) document.panelSettings = savedPanel;
        if (uiTexture != null) { uiTexture.Release(); Destroy(uiTexture); }
        if (surface != null) Destroy(surface);
        if (liveMaterial != null) Destroy(liveMaterial);
        if (livePanel != null) Destroy(livePanel);
        livePanel = null;
        if (gameplayTexture != null) { gameplayTexture.Release(); Destroy(gameplayTexture); }
        if (compositor != null) Destroy(compositor.gameObject);
        if (compositorPanel != null) Destroy(compositorPanel);
        if (gameplayCameraHost != null) Destroy(gameplayCameraHost);
        if (ownedProfile != null) Destroy(ownedProfile);
        if (backdropSurface != null) Destroy(backdropSurface);
        if (backdropMaterial != null) Destroy(backdropMaterial);
        if (gaussianMaterial != null) Destroy(gaussianMaterial);
        backdropImage = null; backdropSurface = null; backdropMaterial = null; gaussianMaterial = null;
        // Other components may call SetGameplayVisible during their later OnDestroy.
        compositor = null;
        compositorPanel = null;
        compositeImage = null;
        gameplayTexture = null;
        gameplayCameraHost = null;
        ownedProfile = null;
        surface = null;
        liveMaterial = null;
        uiTexture = null;
        bloom = null;
        savedPanel = null;
        if (gameplayOutput) { outputCamera = null; bloomVolume = null; }
    }
}
