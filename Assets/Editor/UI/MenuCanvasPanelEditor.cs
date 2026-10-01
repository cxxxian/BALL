using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MenuCanvasPanel), true)]
public class MenuCanvasPanelEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        using(new EditorGUI.DisabledScope(Application.isPlaying))
        if(GUILayout.Button("在 Scene 中编辑此界面"))
        {
            foreach(var panel in Object.FindObjectsOfType<MenuCanvasPanel>(true)) panel.GetComponent<Canvas>().enabled=false;
            foreach(var shop in Object.FindObjectsOfType<ShopCanvasView>(true)) shop.GetComponent<Canvas>().enabled=false;
            var view=(MenuCanvasPanel)target; view.GetComponent<Canvas>().enabled=true;
            var body=view.transform.Find("EditableLayout"); Selection.activeGameObject=body!=null?body.gameObject:view.gameObject;
            Tools.current=Tool.Rect;
            var scene=SceneView.lastActiveSceneView;
            if(scene!=null){scene.in2DMode=true;scene.FrameSelected();scene.Focus();}
        }
        EditorGUILayout.HelpBox("选择 EditableLayout 下的组件，按 T 移动或调整大小。字号在 Text 组件修改。退出 Play 后再保存。",MessageType.Info);
    }
}
