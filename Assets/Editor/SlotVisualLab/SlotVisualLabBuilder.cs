using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public static class SlotVisualLabBuilder
{
    public const string ScenePath = "Assets/Scenes/SlotVisualLab/SlotVisualLab.unity";
    private const string Art = "Assets/Art/SlotVisualLab/";
    public static void CreateScene()
    {
        AssetDatabase.Refresh();
        var original = SceneManager.GetActiveScene();
        if (original.isDirty && !string.IsNullOrEmpty(original.path))
        {
            Directory.CreateDirectory("Assets/Scenes/SlotVisualLab/Recovery");
            string recovery = "Assets/Scenes/SlotVisualLab/Recovery/" + original.name + "_Unsaved_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity";
            EditorSceneManager.SaveScene(original, recovery, true);
            Debug.Log("Preserved unsaved original scene as: " + recovery);
        }
        Import(Art + "terminal_backplate.png", false);
        Import(Art + "protocol_glyphs.png", true);
        var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "protocol_glyphs.png");
        var glyphs = new Texture2D[8];
        for (int i = 0; i < 8; i++)
        {
            // Sprite extraction only; transparency comes from the generated source.
            int cw = atlas.width / 4, ch = atlas.height / 2;
            int size = Mathf.Min(cw, ch);
            int x = (i % 4) * cw + (cw - size) / 2;
            int y = (1 - i / 4) * ch + (ch - size) / 2;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            t.SetPixels(atlas.GetPixels(x, y, size, size)); t.Apply();
            string path = Art + "glyph_" + i + ".png";
            File.WriteAllBytes(path, t.EncodeToPNG()); Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path); Import(path, false);
            glyphs[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        CreateOverlay("glass_edge_shade", false);
        CreateOverlay("selection_glow", true);
        var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/SlotVisualLab/SlotVisualLabPanelSettings.asset");
        if (panel == null)
        {
            panel = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/UIShowcase/UIShowcasePanelSettings.asset"));
            panel.name = "SlotVisualLabPanelSettings";
            AssetDatabase.CreateAsset(panel, "Assets/UI/SlotVisualLab/SlotVisualLabPanelSettings.asset");
        }
        panel.scaleMode = PanelScaleMode.ConstantPixelSize; panel.scale = 1;
        panel.clearColor = true; panel.colorClearValue = new Color(.02f, .027f, .04f, 1);
        EditorUtility.SetDirty(panel);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        var camera = new GameObject("Main Camera").AddComponent<Camera>();
        camera.tag = "MainCamera"; camera.orthographic = true; camera.transform.position = new Vector3(0, 0, -10);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
        camera.cullingMask = 0;
        var light = new GameObject("Main Light (UI does not require lighting)").AddComponent<Light>();
        light.type = LightType.Directional; light.intensity = 0;
        var host = new GameObject("Protocol Terminal - Visual Lab");
        var doc = host.AddComponent<UIDocument>();
        doc.panelSettings = panel;
        doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/SlotVisualLab/SlotVisualLab.uxml");
        var controller = host.AddComponent<SlotVisualLabController>();
        controller.backplate = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "terminal_backplate.png");
        controller.glyphs = glyphs;
        controller.edgeShade = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "glass_edge_shade.png");
        controller.selectionGlow = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "selection_glow.png");
        controller.displayFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Orbitron-Bold.ttf");
        controller.Rebuild();
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = host;
        // Keep original scene loaded, including its unsaved edits. User can open the saved lab independently.
        Debug.Log("Slot Visual Lab ready: " + ScenePath);
    }
    private static void Import(string path, bool readable)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        importer.textureType = TextureImporterType.Default;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.isReadable = readable; importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp; importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }
    private static void CreateOverlay(string name, bool glow)
    {
        var t = new Texture2D(128, 256, TextureFormat.RGBA32, false);
        for (int y = 0; y < t.height; y++) for (int x = 0; x < t.width; x++)
        {
            float u = x / 127f, v = y / 255f;
            float edge = Mathf.Pow(Mathf.Abs(v * 2 - 1), 3);
            float side = Mathf.Pow(Mathf.Abs(u * 2 - 1), 5);
            float a = glow ? Mathf.Pow(1 - Mathf.Abs(v * 2 - 1), 4) * .16f : Mathf.Clamp01(edge * .88f + side * .45f);
            t.SetPixel(x, y, glow ? new Color(.1f, .82f, 1, a) : new Color(0, .015f, .025f, a));
        }
        t.Apply(); string path = Art + name + ".png";
        File.WriteAllBytes(path, t.EncodeToPNG()); Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path); Import(path, false);
    }
}
