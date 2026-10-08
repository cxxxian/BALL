using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TAShowcaseEnvironmentBuilder
{
    public static void Install()
    {
        if(Application.isPlaying) throw new System.InvalidOperationException("Install in Edit mode.");
        var c=Object.FindObjectOfType<TAShowcaseController>();
        if(c == null) throw new System.InvalidOperationException("Open the portfolio scene first.");
        if(c.rainStage == null)
        {
            var root=new GameObject("Code Rain and Fluid Stage"); root.SetActive(false);root.transform.SetParent(c.transform,false);
            var stage=root.AddComponent<TAShowcaseRain>(); stage.background=root.AddComponent<MainMenuMatrixBackground>();
            var so=new SerializedObject(stage.background);
            so.FindProperty("midRainShader").objectReferenceValue=Shader.Find("Custom/TronArenaMidRain");
            so.FindProperty("fluidShader").objectReferenceValue=Shader.Find("Hidden/MenuWakeField");
            so.ApplyModifiedPropertiesWithoutUndo();c.rainStage=stage;
        }
        if(c.extraStage == null)
        {
            var root=new GameObject("Wall and Shield Studies");root.SetActive(false);root.transform.SetParent(c.transform,false);
            c.extraStage=root.AddComponent<TAShowcaseExtras>();
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Table/v1/wall_segment.png");
            var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/TAShowcase/PortfolioWall.mat");
            if(material == null)
            {
                material=new Material(Shader.Find("Custom/SpriteNeonHDR"));
                material.mainTexture=sprite.texture;material.SetFloat("_NeonIntensity",6.5f);
                material.SetFloat("_NeonThreshold",.2f);material.SetFloat("_NeonSharpness",1.5f);
                AssetDatabase.CreateAsset(material,"Assets/Settings/TAShowcase/PortfolioWall.mat");
            }
            c.extraStage.wallMaterial=material;
        }
        if(c.exhibitButtons.Length < 7)
        {
            var buttons=new UnityEngine.UI.Button[7];
            c.exhibitButtons.CopyTo(buttons,0);
            for(int i=c.exhibitButtons.Length;i<7;i++)buttons[i]=Object.Instantiate(buttons[0],buttons[0].transform.parent);
            c.exhibitButtons=buttons;
        }
        string[] names={"01   电荷电弧","02   战斗霜痕","03   代码雨与流体","04   冲击与解体","05   HDR 与辉光","06   墙体传播脉冲","07   反应式能量盾"};
        for(int i=0;i<7;i++)
        {
            var button=c.exhibitButtons[i];button.name="Exhibit "+i;
            button.GetComponentInChildren<UnityEngine.UI.Text>().text=names[i];
            var r=button.GetComponent<RectTransform>();r.anchorMin=new Vector2(.043f,.708f-i*.042f);r.anchorMax=new Vector2(.23f,.745f-i*.042f);
        }
        var backgroundButton=c.presentationCanvas.transform.Find("Background");
        if(backgroundButton != null) Object.DestroyImmediate(backgroundButton.gameObject);
        c.presentationCamera.backgroundColor=Color.black;
        foreach(var text in c.presentationCanvas.GetComponentsInChildren<UnityEngine.UI.Text>(true))
            if(text.name == "Footer")text.text="1–7 切换展示    ·    C 循环 / 自动轨迹    ·    R 重置    ·    H 隐藏界面";
        EditorUtility.SetDirty(c);
        EditorSceneManager.MarkSceneDirty(c.gameObject.scene);EditorSceneManager.SaveScene(c.gameObject.scene);AssetDatabase.SaveAssets();
    }
}
