using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Relights only the cabinet background; overlays retain their original UI rendering.</summary>
[RequireComponent(typeof(UIDocument), typeof(SlotVisualLabV2Controller))]
public sealed class SlotCabinetRelighting : MonoBehaviour
{
    public Shader relightingShader;
    public Texture2D albedo;
    public Texture2D normalMap;
    public bool animateLight;
    [Tooltip("Stylized metal response: increases tinted highlights and slightly reduces diffuse light.")]
    [Range(0, 1)] public float metallic = .2f;
    [Range(0, 2)] public float normalStrength = .65f;
    [Range(0, 2)] public float lampStrength = .85f;
    [Range(0, 1)] public float rimStrength = .18f;
    [Range(1, 8)] public float rimPower = 3f;
    public Vector3 lightDirection = new Vector3(-.4f, .6f, 1f);

    private UIDocument document;
    private SlotVisualLabV2Controller controller;
    private Material material;
    private RenderTexture cabinet, emissionMask;
    private VisualElement plate, dock;
    private StyleBackground savedBackground;
    private StyleLength savedDockHeight;
    private Label modeLabel;
    private Button lightButton;

    private void OnEnable()
    {
        document = GetComponent<UIDocument>();
        controller = GetComponent<SlotVisualLabV2Controller>();
        if (relightingShader == null || albedo == null || normalMap == null) return;
        material = new Material(relightingShader) { hideFlags = HideFlags.DontSave };
        cabinet = CreateTarget("Relit cabinet", albedo.width, albedo.height);
        emissionMask = CreateTarget("Cabinet lamp mask", albedo.width, albedo.height);
        material.SetTexture("_BaseMap", albedo);
        material.SetTexture("_NormalMap", normalMap);
        material.SetTexture("_EmissionMask", emissionMask);
        material.SetTexture("_SourceMap", controller.backplate);
        // Derive a separate lamp mask from colored emitter cores, never from neutral outlines.
        Graphics.Blit(controller.backplate, emissionMask, material, 1);
        RenderCabinet();
    }

    private static RenderTexture CreateTarget(string targetName, int width, int height)
    {
        var target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
        {
            name = targetName, hideFlags = HideFlags.DontSave,
            filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
        };
        target.Create();
        return target;
    }

    private void LateUpdate()
    {
        if (material == null || document.rootVisualElement == null) return;
        var nextPlate = document.rootVisualElement.Q("machineBackplate");
        if (nextPlate != plate)
        {
            if (plate != null) plate.style.backgroundImage = savedBackground;
            plate = nextPlate;
            if (plate != null) savedBackground = plate.style.backgroundImage;
        }
        BindControls();
        if (Input.GetKeyDown(KeyCode.F8)) ToggleLight();
        if (plate == null) return;
        RenderCabinet();
        plate.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(cabinet));
    }

    /// <summary>Assembly faces and the completed UI sample the same live cabinet appearance.</summary>
    public Texture GetAssemblyTexture()
    {
        if (material == null) return albedo;
        RenderCabinet();
        return cabinet;
    }

    private void RenderCabinet()
    {
        Vector3 direction = lightDirection;
        if (animateLight)
        {
            float phase = Time.unscaledTime * .6f;
            direction = new Vector3(Mathf.Sin(phase) * .75f, .5f + Mathf.Cos(phase) * .25f, 1);
        }
        material.SetVector("_LightDirection", direction.normalized);
        material.SetFloat("_NormalStrength", normalStrength);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_LampStrength", lampStrength);
        material.SetFloat("_RimStrength", rimStrength);
        material.SetFloat("_RimPower", rimPower);
        Graphics.Blit(albedo, cabinet, material, 0);
    }

    private void BindControls()
    {
        var nextDock = document.rootVisualElement.Q("testDock");
        if (nextDock == dock) return;
        UnbindControls();
        dock = nextDock;
        if (dock == null) return;
        savedDockHeight = dock.style.height;
        dock.style.height = 128;
        modeLabel = new Label("法线光照") { name = "cabinetMaterialLabel", pickingMode = PickingMode.Ignore };
        modeLabel.AddToClassList("caption");
        modeLabel.style.left = 12;
        modeLabel.style.top = 90;
        modeLabel.style.width = 272;
        modeLabel.style.height = 30;
        modeLabel.style.fontSize = 14;
        dock.Add(modeLabel);
        lightButton = MakeButton("cabinetLightButton", 300, 264, ToggleLight);
        UpdateLabels();
    }

    private Button MakeButton(string elementName, float left, float width, System.Action callback)
    {
        var button = new Button(callback) { name = elementName };
        button.AddToClassList("live-button");
        button.AddToClassList("test-button");
        button.style.left = left;
        button.style.top = 90;
        button.style.width = width;
        button.style.height = 30;
        button.style.fontSize = 14;
        dock.Add(button);
        return button;
    }

    public void ToggleLight() { animateLight = !animateLight; UpdateLabels(); }
    private void UpdateLabels()
    {
        if (lightButton != null) lightButton.text = animateLight ? "移动灯光 · F8" : "固定灯光 · F8";
    }

    private void UnbindControls()
    {
        if (modeLabel != null) modeLabel.RemoveFromHierarchy();
        if (lightButton != null) { lightButton.clicked -= ToggleLight; lightButton.RemoveFromHierarchy(); }
        modeLabel = null;
        lightButton = null;
        if (dock != null) dock.style.height = savedDockHeight;
        dock = null;
    }

    private void OnDisable()
    {
        if (plate != null) plate.style.backgroundImage = savedBackground;
        plate = null;
        UnbindControls();
        if (cabinet != null) { cabinet.Release(); Destroy(cabinet); }
        if (emissionMask != null) { emissionMask.Release(); Destroy(emissionMask); }
        if (material != null) Destroy(material);
    }
}
