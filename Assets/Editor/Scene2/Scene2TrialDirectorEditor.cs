using UnityEditor;
using UnityEngine;

// Comparison controls live on the Scene2 component, never in the top menu bar.
[CustomEditor(typeof(Scene2TrialDirector))]
public sealed class Scene2TrialDirectorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Scene2 循环对比", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("停止播放后切换。两版使用相同敌人、Boss 和奖励数值。", MessageType.Info);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("新规则：破局目标 → 弱点 + 改向"))
                SelectConfig("Assets/Resources/Scene2/KeyTargetConfig.asset");
            if (GUILayout.Button("旧规则：全清六只 → 弱点"))
                SelectConfig("Assets/Resources/Scene2/TrialConfig.asset");
        }
    }

    private void SelectConfig(string path)
    {
        var config = AssetDatabase.LoadAssetAtPath<Scene2Config>(path);
        if (config == null) { Debug.LogError("Missing Scene2 comparison config: " + path); return; }
        serializedObject.Update();
        serializedObject.FindProperty("config").objectReferenceValue = config;
        serializedObject.ApplyModifiedProperties();
    }
}
