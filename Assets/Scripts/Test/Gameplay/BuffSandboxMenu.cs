#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>快捷打开 Buff 沙盒测试场景。</summary>
public static class BuffSandboxMenu
{
    private const string ScenePath = "Assets/Scenes/Gameplay/BuffSandbox.unity";

    [MenuItem("Ball/Test/Open Buff Sandbox")]
    public static void OpenSandbox()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene(ScenePath);
    }
}
#endif
