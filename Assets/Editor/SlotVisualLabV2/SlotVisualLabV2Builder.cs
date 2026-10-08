using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class SlotVisualLabV2Builder
{
    public const string ScenePath = "Assets/Scenes/SlotVisualLabV2/SlotVisualLabV2.unity";
    private const string Plate = "Assets/Art/SlotVisualLabV2/terminal_portrait_clean_transparent.png";

    public static void RefreshDemoBuffPool()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            Debug.LogWarning("Open SlotVisualLabV2 before refreshing its demo Buff pool.");
            return;
        }
        var controller = Object.FindObjectOfType<SlotVisualLabV2Controller>();
        if (controller == null) return;
        controller.demoBuffPool = LoadDemoBuffPool();
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"V2 demo Buff pool refreshed: {controller.demoBuffPool.Length} assets.");
    }

    [MenuItem("Rebound Protocol/Slot Visual Lab/Edit V2 UI in UI Builder")]
    public static void OpenUILayout()
    {
        AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/SlotVisualLabV2/SlotVisualLabV2.uxml"));
    }

    public static void CreateScene()
    {
        AssetDatabase.Refresh();
        var importer = AssetImporter.GetAtPath(Plate) as TextureImporter;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.isReadable = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        string glyphAtlasPath = "Assets/Art/SlotVisualLabV2/protocol_glyphs_v2.png";
        AssetDatabase.ImportAsset(glyphAtlasPath);
        var glyphImporter = AssetImporter.GetAtPath(glyphAtlasPath) as TextureImporter;
        glyphImporter.isReadable = true;
        glyphImporter.alphaIsTransparency = true;
        glyphImporter.mipmapEnabled = false;
        glyphImporter.textureCompression = TextureImporterCompression.Uncompressed;
        glyphImporter.SaveAndReimport();
        var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(glyphAtlasPath);
        var glyphs = new Texture2D[8];
        var glyphBounds = new Rect[8];
        for (int i = 0; i < 8; i++)
        {
            int cw = atlas.width / 4, ch = atlas.height / 2;
            // Keep the entire atlas cell: square center crops clipped tall symbols.
            var cell = new Texture2D(cw, ch, TextureFormat.RGBA32, false);
            cell.SetPixels(atlas.GetPixels(i % 4 * cw, (1 - i / 4) * ch, cw, ch));
            cell.Apply();
            glyphBounds[i] = VisibleBounds(cell);
            string path = "Assets/Art/SlotVisualLabV2/glyph_" + i + ".png";
            System.IO.File.WriteAllBytes(path, cell.EncodeToPNG());
            Object.DestroyImmediate(cell);
            AssetDatabase.ImportAsset(path);
            var gi = AssetImporter.GetAtPath(path) as TextureImporter;
            gi.alphaIsTransparency = true;
            gi.mipmapEnabled = false;
            gi.wrapMode = TextureWrapMode.Clamp;
            gi.textureCompression = TextureImporterCompression.Uncompressed;
            gi.SaveAndReimport();
            glyphs[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        string panelPath = "Assets/UI/SlotVisualLabV2/SlotVisualLabV2PanelSettings.asset";
        var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(panelPath);
        if (panel == null)
        {
            panel = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/SlotVisualLab/SlotVisualLabPanelSettings.asset"));
            panel.name = "SlotVisualLabV2PanelSettings";
            AssetDatabase.CreateAsset(panel, panelPath);
        }
        panel.targetTexture = null;
        panel.scaleMode = PanelScaleMode.ConstantPixelSize;
        panel.scale = 1;
        panel.clearColor = true;
        panel.colorClearValue = Color.clear;
        EditorUtility.SetDirty(panel);
        // Preserve unsaved work by keeping dirty scenes loaded.
        bool dirty = false;
        for (int i = 0; i < SceneManager.sceneCount; i++) dirty |= SceneManager.GetSceneAt(i).isDirty;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, dirty ? NewSceneMode.Additive : NewSceneMode.Single);
        SceneManager.SetActiveScene(scene);
        var camera = new GameObject("V2 Preview Camera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.orthographic = true;
        camera.allowHDR = true;
        camera.transform.position = new Vector3(0, 0, -10);
        camera.cullingMask = 1 << 5;
        camera.backgroundColor = new Color(.035f, .055f, .075f, 1);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.gameObject.AddComponent<AudioListener>();
        var cameraData = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        cameraData.renderPostProcessing = true;
        cameraData.volumeLayerMask = 1 << 5;
        cameraData.antialiasing = AntialiasingMode.None;
        string profilePath = "Assets/Settings/SlotVisualLabV2/SlotVisualLabV2Bloom.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(.4f);
            bloom.threshold.Override(1f);
            bloom.scatter.Override(.55f);
            bloom.highQualityFiltering.Override(true);
            AssetDatabase.AddObjectToAsset(bloom, profile);
            EditorUtility.SetDirty(profile);
        }
        var volume = new GameObject("Slot V2 / restrained engine Bloom").AddComponent<Volume>();
        volume.gameObject.layer = 5;
        volume.isGlobal = true;
        volume.priority = 100;
        volume.sharedProfile = profile;
        var light = new GameObject("UI Main Light").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 0;
        var host = new GameObject("Protocol Array - V2 Portrait");
        var doc = host.AddComponent<UIDocument>();
        doc.panelSettings = panel;
        doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/SlotVisualLabV2/SlotVisualLabV2.uxml");
        var controller = host.AddComponent<SlotVisualLabV2Controller>();
        controller.backplate = AssetDatabase.LoadAssetAtPath<Texture2D>(Plate);
        controller.glyphs = glyphs;
        controller.glyphBounds = glyphBounds;
        controller.demoBuffPool = LoadDemoBuffPool();
        controller.edgeShade = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/SlotVisualLab/glass_edge_shade.png");
        controller.selectionGlow = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/SlotVisualLab/selection_glow.png");
        controller.displayFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Orbitron-Bold.ttf");
        string materialPath = "Assets/Settings/SlotVisualLabV2/SlotUIHDR.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("ReboundProtocol/SlotVisualLabV2/UIHDR"));
            AssetDatabase.CreateAsset(material, materialPath);
        }
        var engineBloom = host.AddComponent<SlotVisualLabV2Bloom>();
        engineBloom.emissionStrength = 2.4f;
        engineBloom.bloomIntensity = .55f;
        engineBloom.bloomThreshold = .75f;
        engineBloom.bloomScatter = .65f;
        host.AddComponent<SlotMechanicalAssembly3D>();
        engineBloom.outputCamera = camera;
        engineBloom.displayMaterial = material;
        engineBloom.bloomVolume = volume;
        var sfxSource = host.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.spatialBlend = 0f;
        var slotSfx = host.AddComponent<SlotMachineSfx>();
        slotSfx.hover = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SlotMachine/slot_hover_tron_v2.wav");
        slotSfx.press = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SlotMachine/slot_press_tron_v2.wav");
        slotSfx.spinStart = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SlotMachine/slot_spin_start_tron_v2.wav");
        slotSfx.reelStop = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SlotMachine/slot_reel_stop.wav");
        slotSfx.rollConfirm = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SlotMachine/slot_roll_confirm_tron_v2.wav");
        slotSfx.specialReveal = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SlotMachine/slot_special_reveal_tron_v2.wav");
        slotSfx.reelTicks = new AudioClip[3];
        for (int i = 0; i < slotSfx.reelTicks.Length; i++)
            slotSfx.reelTicks[i] = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SlotMachine/slot_reel_tick_" + (i + 1).ToString("00") + ".wav");
        slotSfx.reelStops = new AudioClip[3];
        for (int i = 0; i < slotSfx.reelStops.Length; i++)
            slotSfx.reelStops[i] = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SlotMachine/slot_reel_stop_tron_v2_" + (i + 1).ToString("00") + ".wav");
        controller.sfx = slotSfx;
        controller.Rebuild();
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = host;
        Debug.Log("V2 portrait visual lab ready: " + ScenePath);
    }

    public static Rect VisibleBounds(Texture2D texture)
    {
        var pixels = texture.GetPixels32();
        int minX = texture.width, minY = texture.height, maxX = -1, maxY = -1;
        for (int y = 0; y < texture.height; y++)
            for (int x = 0; x < texture.width; x++)
                if (pixels[y * texture.width + x].a >= 32)
                { minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y); maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y); }
        return maxX < 0 ? new Rect(0, 0, texture.width, texture.height)
            : new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    private static BuffDefinition[] LoadDemoBuffPool()
    {
        var buffGuids = AssetDatabase.FindAssets("t:BuffDefinition", new[] { "Assets/ScriptableObjects/Buffs" });
        var demoBuffs = new System.Collections.Generic.List<BuffDefinition>();
        foreach (var guid in buffGuids)
        {
            var buff = AssetDatabase.LoadAssetAtPath<BuffDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (buff != null) demoBuffs.Add(buff);
        }
        return demoBuffs.ToArray();
    }
}
