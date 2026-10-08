using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class TAShowcaseBuilder
{
    public const string ScenePath = "Assets/Scenes/TAShowcase/TAPortfolio.unity";
    private static Font _font;
    private static readonly Color White = new Color(0.86f, 0.93f, 0.96f);
    private static readonly Color Muted = new Color(0.52f, 0.66f, 0.73f);

    public static void Open()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Exit Play mode first.");
        if (System.IO.File.Exists(ScenePath))
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { EditorSceneManager.OpenScene(ScenePath); TAShowcaseEnvironmentBuilder.Install(); }
            return;
        }
        Build();
    }

    public static void Build()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Build in Edit mode.");
        if (SceneManager.GetActiveScene().isDirty) throw new System.InvalidOperationException("Save the current scene before creating the showcase.");
        if (System.IO.File.Exists(ScenePath)) throw new System.InvalidOperationException("Showcase exists. Use Open; never overwrite authored edits.");
        _font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSansCJKsc-Regular.otf");
        if (_font == null) throw new System.InvalidOperationException("Chinese UI font missing.");
        System.IO.Directory.CreateDirectory("Assets/Scenes/TAShowcase");
        System.IO.Directory.CreateDirectory("Assets/Settings/TAShowcase");
        AssetDatabase.Refresh();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("TA Portfolio");
        var controller = root.AddComponent<TAShowcaseController>();
        var cam = new GameObject("Presentation Camera").AddComponent<Camera>();
        cam.tag = "MainCamera"; cam.orthographic = true; cam.orthographicSize = 5.6f;
        cam.transform.position = new Vector3(0, 0, -10); cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black; cam.allowHDR = true;
        cam.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
        controller.presentationCamera = cam;
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        var bloom = profile.Add<Bloom>(true); bloom.intensity.value = 0.65f; bloom.threshold.value = 1f; bloom.scatter.value = 0.55f;
        AssetDatabase.CreateAsset(profile, "Assets/Settings/TAShowcase/PortfolioVolume.asset");
        AssetDatabase.AddObjectToAsset(bloom, profile);
        var volume = new GameObject("Presentation Bloom").AddComponent<Volume>(); volume.isGlobal = true; volume.sharedProfile = profile;
        controller.presentationVolume = volume;
        var mat = new Material(Shader.Find("Custom/SpriteNeonHDR")); mat.SetFloat("_NeonIntensity", 3.5f);
        AssetDatabase.CreateAsset(mat, "Assets/Settings/TAShowcase/PortfolioNeon.mat"); controller.neonMaterial = mat;
        var def = AssetDatabase.LoadAssetAtPath<MinionDefinition>("Assets/ScriptableObjects/Enemies/Minion_Armored.asset");
        if (def == null || def.sprite == null) throw new System.InvalidOperationException("Armored enemy sprite missing.");
        controller.hero = Specimen("Hero · enlarged", def.sprite, new Vector2(2.5f, 0.65f), 4.5f, root.transform, 5f);
        controller.specimens = new TAShowcaseTarget[4];
        for (int i = 0; i < 4; i++) controller.specimens[i] = Specimen("State " + i + " · gameplay size", def.sprite, new Vector2(-1.1f + i * 2.4f, -3.05f), 0.9f, root.transform, 3f);
        controller.impact = new GameObject("Production ImpactFX").AddComponent<ImpactFX>();
        controller.tesla = new GameObject("Production TeslaArcFX").AddComponent<TeslaArcFX>();
        BuildUI(controller);
        TAShowcaseEnvironmentBuilder.Install();
        new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
        EditorSceneManager.SaveScene(scene, ScenePath); AssetDatabase.SaveAssets();
       
        Selection.activeGameObject = root;
    }

    private static TAShowcaseTarget Specimen(string name, Sprite sprite, Vector2 pos, float width, Transform parent, float arcWidth)
    {
        var go = new GameObject(name); go.transform.SetParent(parent); go.transform.position = pos;
        go.transform.localScale = Vector3.one * (width / sprite.bounds.size.x);
        var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = sprite; sr.sortingOrder = 5;
        var target = go.AddComponent<TAShowcaseTarget>();
        EnemyBuildStackVisual.EnsureOn(target);
        var serialized = new SerializedObject(go.GetComponent<EnemyChargeArcVisual>());
        serialized.FindProperty("bodyWidthPixels").floatValue = arcWidth; serialized.ApplyModifiedPropertiesWithoutUndo();
        return target;
    }

    private static void BuildUI(TAShowcaseController c)
    {
        var canvas = new GameObject("Portfolio Interface", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster), typeof(CanvasGroup));
        c.presentationCanvas = canvas.GetComponent<Canvas>(); c.presentationCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        c.presentationCanvas.worldCamera = c.presentationCamera; c.presentationCanvas.planeDistance = 5f;
        c.interfaceGroup = canvas.GetComponent<CanvasGroup>();
        var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 1f;
        var body = canvas.transform;
        Text(body, "Brand", "REBOUND / TECHNICAL ART", .03f, .925f, .8f, .975f, 26, White);
        Text(body, "Subtitle", "实时效果研究  /  Unity URP 2D", .03f, .882f, .6f, .922f, 17, Muted);
        Text(body, "Edition", "PORTFOLIO   /   2026", .79f, .93f, .97f, .97f, 16, Muted);
        Box(body, "Header rule", .03f, .865f, .97f, .867f, new Color(.2f, .36f, .43f));
        Box(body, "Exhibit rail", .025f, .14f, .248f, .84f, new Color(.025f,.035f,.052f,.96f));
        Text(body, "Index caption", "EXHIBITS  /  选择展示", .043f, .78f, .23f, .825f, 17, Muted);
        string[] names = { "01   电荷电弧", "02   冰霜材质", "03   代码雨与流体", "04   冲击与解体", "05   HDR 与辉光", "06   墙体传播脉冲", "07   反应式能量盾" };
        c.exhibitButtons = new UnityEngine.UI.Button[7];
        for (int i = 0; i < 7; i++) c.exhibitButtons[i] = Button(body, "Exhibit " + i, names[i], .043f, .708f-i*.049f, .23f, .75f-i*.049f, 20);
        c.detailText = Text(body, "Technical notes", "", .043f, .185f, .231f, .442f, 20, Muted);
        c.titleText = Text(body, "Exhibit title", "程序化电荷 / ELECTRIC", .285f, .775f, .94f, .835f, 32, White);
        c.stateText = Text(body, "State", "", .285f, .726f, .9f, .771f, 18, Muted);
        Text(body, "Closeup caption", "ENLARGED VIEW  /  放大观察", .285f, .66f, .51f, .713f, 15, Muted);
        Text(body, "Comparison caption", "PRODUCTION SCALE  /  原始尺寸 · 0–3 层对照", .285f, .262f, .96f, .302f, 17, Muted);
        for (int i = 0; i < 4; i++) Text(body, "Specimen label " + i, "0"+i+"  /  STATE", .41f+i*.12f, .148f, .52f+i*.12f, .172f, 15, Muted);
        Box(body, "Control rule", .03f, .13f, .97f, .132f, new Color(.2f,.36f,.43f));
        c.levelButtons = new UnityEngine.UI.Button[4];
        for(int i=0;i<4;i++) c.levelButtons[i] = Button(body,"Level "+i,i.ToString(),.285f+i*.035f,.318f,.315f+i*.035f,.36f,18);
        c.cycleButton = Button(body,"Cycle","循环 ON [C]",.44f,.318f,.565f,.36f,17);
        c.freezeButton = Button(body,"Freeze","短冻 [F]",.575f,.318f,.68f,.36f,17);
        c.triggerButton = Button(body,"Trigger","播放 / 消耗 [SPACE]",.69f,.318f,.945f,.36f,17);

        c.bloomButton = Button(body,"Bloom","Bloom ON [P]",.22f,.063f,.355f,.109f,17);
        c.slowButton = Button(body,"Slow","速度 1× [S]",.37f,.063f,.495f,.109f,17);
        c.cleanButton = Button(body,"Clean","隐藏界面 [H]",.51f,.063f,.65f,.109f,17);
        c.scaleText = Text(body,"Scale caption","",.69f,.084f,.94f,.12f,17,Muted);
        var sliderRect = Rect(body,"Closeup scale",.69f,.047f,.94f,.078f);
        c.scaleSlider = sliderRect.gameObject.AddComponent<UnityEngine.UI.Slider>(); c.scaleSlider.minValue=3f;c.scaleSlider.maxValue=6f;c.scaleSlider.value=5f;
        var track=Box(sliderRect,"Track",0,.4f,1,.6f,new Color(.18f,.29f,.35f));
        var handle=Box(sliderRect,"Handle",0,0,0,1,new Color(.3f,.88f,1f)); handle.sizeDelta=new Vector2(16,0);
        c.scaleSlider.handleRect=handle;c.scaleSlider.targetGraphic=handle.GetComponent<UnityEngine.UI.Image>();
        Text(body,"Footer","1–7 切换展示    ·    C 循环 / 自动轨迹    ·    R 清空流体    ·    H 隐藏界面",.03f,.008f,.97f,.04f,15,Muted);
    }
    private static RectTransform Rect(Transform parent,string name,float x0,float y0,float x1,float y1)
    {
        var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);
        rect.anchorMin=new Vector2(x0,y0);rect.anchorMax=new Vector2(x1,y1);rect.offsetMin=rect.offsetMax=Vector2.zero;return rect;
    }
    private static RectTransform Box(Transform parent,string name,float x0,float y0,float x1,float y1,Color color)
    {
        var rect=Rect(parent,name,x0,y0,x1,y1);var image=rect.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=color;image.raycastTarget=false;return rect;
    }
    private static UnityEngine.UI.Text Text(Transform parent,string name,string value,float x0,float y0,float x1,float y1,int size,Color color)
    {
        var rect=Rect(parent,name,x0,y0,x1,y1);var text=rect.gameObject.AddComponent<UnityEngine.UI.Text>();text.font=_font;text.fontSize=size;text.text=value;text.color=color;
        text.alignment=TextAnchor.UpperLeft;text.raycastTarget=false;text.horizontalOverflow=HorizontalWrapMode.Wrap;return text;
    }
    private static UnityEngine.UI.Button Button(Transform parent,string name,string value,float x0,float y0,float x1,float y1,int size)
    {
        var rect=Box(parent,name,x0,y0,x1,y1,new Color(.075f,.1f,.135f));var image=rect.GetComponent<UnityEngine.UI.Image>();image.raycastTarget=true;
        var button=rect.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=image;
        var colors=button.colors;colors.highlightedColor=new Color(.55f,.85f,1f);colors.selectedColor=colors.highlightedColor;button.colors=colors;
        var label=Text(rect,"Label",value,.05f,0,.95f,1,size,White);label.alignment=TextAnchor.MiddleLeft;return button;
    }
}



