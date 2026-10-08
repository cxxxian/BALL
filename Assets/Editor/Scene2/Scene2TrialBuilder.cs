using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class Scene2TrialBuilder
{
    public const string ScenePath = "Assets/Scenes/SampleScene2.unity";
    public static void Open()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(ScenePath);
    }

    // First-time setup only. Never rewrites an existing Scene2 or its tuning assets.
    public static string Create()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            return "Scene2 already exists; setup skipped.";
        EnsureFolder("Assets/Resources/Scene2");
        var cfg = ScriptableObject.CreateInstance<Scene2Config>();
        cfg.boss = Copy<BossDefinition>("Assets/ScriptableObjects/Enemies/Boss_W1.asset", "Boss");
        cfg.grunt = Copy<MinionDefinition>("Assets/ScriptableObjects/Enemies/Minion_Grunt.asset", "Grunt");
        cfg.armored = Copy<MinionDefinition>("Assets/ScriptableObjects/Enemies/Minion_Armored.asset", "Armored");
        cfg.bomber = Copy<MinionDefinition>("Assets/ScriptableObjects/Enemies/Minion_Bomber.asset", "Bomber");
        cfg.grunt.maxHP = 1;
        cfg.armored.maxHP = 3;
        cfg.bomber.maxHP = 1;
        cfg.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSansCJKsc-Regular.otf");
        EditorUtility.SetDirty(cfg.grunt); EditorUtility.SetDirty(cfg.armored); EditorUtility.SetDirty(cfg.bomber);
        AssetDatabase.CreateAsset(cfg, "Assets/Resources/Scene2/TrialConfig.asset");
        AssetDatabase.SaveAssets();
        if (!AssetDatabase.CopyAsset("Assets/Scenes/SampleScene.unity", ScenePath))
            throw new System.InvalidOperationException("Cannot copy SampleScene.");
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var go = new GameObject("Scene2Trial");
        var director = go.AddComponent<Scene2TrialDirector>();
        var so = new SerializedObject(director);
        so.FindProperty("config").objectReferenceValue = cfg;
        so.ApplyModifiedPropertiesWithoutUndo();

        var ui = new GameObject("Scene2TrialReadout", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = ui.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        var scaler = ui.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var textGo = new GameObject("TrialStatus", typeof(RectTransform), typeof(UnityEngine.UI.Text));
        textGo.transform.SetParent(ui.transform, false);
        var rect = textGo.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(24f, -150f);
        rect.sizeDelta = new Vector2(440f, 96f);
        var text = textGo.GetComponent<UnityEngine.UI.Text>();
        text.font = cfg.font; text.fontSize = 22; text.raycastTarget = false;
        text.text = "SCENE 2 · 清编队换爆发";
        text.color = new Color(0.5f, 1f, 1f);
        var hud = ui.AddComponent<Scene2TrialHud>();
        var hs = new SerializedObject(hud);
        hs.FindProperty("director").objectReferenceValue = director;
        hs.FindProperty("statusText").objectReferenceValue = text;
        hs.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(scene);
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!scenes.Exists(s => s.path == ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        return "Created SampleScene2, isolated config and appended build scene.";
    }

    private static T Copy<T>(string source, string name) where T : Object
    {
        string path = "Assets/Resources/Scene2/" + name + ".asset";
        if (!AssetDatabase.CopyAsset(source, path)) throw new System.InvalidOperationException("Cannot copy " + source);
        return AssetDatabase.LoadAssetAtPath<T>(path);
    }
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        EnsureFolder(path.Substring(0, slash));
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }
}
