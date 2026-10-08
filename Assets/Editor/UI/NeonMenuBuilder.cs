using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Editor authoring only. Existing prefab layouts are never regenerated.</summary>
public static class NeonMenuBuilder
{
    private static Font font;
    private static readonly Color Cyan = new Color(0.18f,0.86f,1f);
    private static readonly Color Muted = new Color(0.59f,0.72f,0.78f);
    public static void Build()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Use Edit mode.");
        font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSansCJKsc-Regular.otf");
        if(font == null) throw new System.InvalidOperationException("Missing menu font.");
        System.IO.Directory.CreateDirectory("Assets/UI/MainMenu"); System.IO.Directory.CreateDirectory("Assets/UI/Loadout");
        var main = Object.FindObjectOfType<MainMenuCanvasView>(true);
        if(main == null) main = BuildMain();
        var loadout = Object.FindObjectOfType<LoadoutCanvasView>(true);
        if(loadout == null) loadout = BuildLoadout();
        Assign(Object.FindObjectOfType<MainMenuController>(),"canvasView",main);
        Assign(Object.FindObjectOfType<LoadoutPanelController>(),"canvasView",loadout);
        main.GetComponent<Canvas>().enabled = true; loadout.GetComponent<Canvas>().enabled = false;
        var shop = Object.FindObjectOfType<ShopCanvasView>(true); if(shop != null) shop.GetComponent<Canvas>().enabled = false;
        EditorSceneManager.MarkSceneDirty(main.gameObject.scene); EditorSceneManager.SaveScene(main.gameObject.scene);
        AssetDatabase.SaveAssets(); Selection.activeGameObject = main.transform.Find("EditableLayout").gameObject;
        Tools.current = Tool.Rect;
    }
    private static MainMenuCanvasView BuildMain()
    {
        var root=Root("MainMenuCanvas"); Rect(root.transform,"EditableLayout",0,0,1080,1920);
        var view=root.AddComponent<MainMenuCanvasView>(); BaseReferences(view,root);
        ApplyTerminalLayout(view);
        PrefabUtility.SaveAsPrefabAssetAndConnect(root,"Assets/UI/MainMenu/MainMenuCanvas.prefab",InteractionMode.AutomatedAction);
        return view;
    }
    public static void RestoreOriginalMainMenu()
    {
        if(Application.isPlaying) throw new System.InvalidOperationException("Use Edit mode.");
        font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSansCJKsc-Regular.otf");
        var view=Object.FindObjectOfType<MainMenuCanvasView>(true);
        if(view==null) throw new System.InvalidOperationException("Main menu canvas missing.");
        ApplyTerminalLayout(view);
        PrefabUtility.ApplyPrefabInstance(view.gameObject,InteractionMode.AutomatedAction);
        EditorSceneManager.SaveScene(view.gameObject.scene); AssetDatabase.SaveAssets();
    }
    private static void ApplyTerminalLayout(MainMenuCanvasView view)
    {
        var root=view.gameObject; var body=root.transform.Find("EditableLayout");
        var background=root.transform.Find("Background").GetComponent<UnityEngine.UI.Image>(); background.color=new Color(2f/255,6f/255,14f/255,0.4f);
        foreach(string name in new[]{"ProtocolCore","LoadoutCaption"}) {var old=body.Find(name); if(old!=null) old.gameObject.SetActive(false);}
        var orbitron=AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Orbitron-Bold.ttf");
        var header=ExistingRect(body,"Header",0,0,1008,94);
        var status=ExistingText(header,"SystemStatus",-320,0,368,94,"SYS:ONLINE",30,Cyan,TextAnchor.MiddleLeft); status.font=orbitron;
        var balance=ExistingText(header,"Balance",428,0,160,94,"999,999",29,new Color(1,0.84f,0.3f),TextAnchor.MiddleRight); balance.font=orbitron;
        var currency=ExistingText(header,"CurrencyKey",324,0,54,94,"CR",26,Cyan,TextAnchor.MiddleCenter); currency.font=orbitron;
        var settingsTop=TerminalButton(header,"HeaderSettingsButton",210,0,220,94,"SETTINGS",26,Muted,orbitron);
        var divider=ExistingRect(body,"HeaderDivider",0,93,1080,1); divider.GetComponent<UnityEngine.UI.Image>().color=new Color(0,0.9f,1,0.22f);
        var brand=ExistingRect(body,"Brand",0,578,984,354);
        var title=ExistingText(brand,"ChineseTitle",0,0,984,100,"回弹协议",72,new Color(0.55f,0.9f,1,0.82f),TextAnchor.MiddleCenter); title.fontStyle=FontStyle.Bold;
        var titleOutline=title.GetComponent<UnityEngine.UI.Outline>(); if(titleOutline==null) titleOutline=title.gameObject.AddComponent<UnityEngine.UI.Outline>(); titleOutline.effectColor=new Color(0.05f,0.3f,0.4f); titleOutline.effectDistance=new Vector2(2,-2);
        var english=ExistingText(brand,"EnglishTitle",0,124,984,144,"REBOUND",112,new Color(0.025f,0.065f,0.09f),TextAnchor.MiddleCenter); english.font=orbitron;
        var glow=english.GetComponent<UnityEngine.UI.Outline>(); if(glow==null) glow=english.gameObject.AddComponent<UnityEngine.UI.Outline>(); glow.effectColor=new Color(0.64f,0.95f,1); glow.effectDistance=new Vector2(2.5f,-2.5f);
        var subtitle=ExistingText(brand,"Subtitle",0,279,984,76,"PROTOCOL",40,new Color(1,0.28f,0.78f),TextAnchor.MiddleCenter); subtitle.font=orbitron;
        var actions=ExistingRect(body,"Actions",0,970,984,250);
        var start=TerminalButton(actions,"StartButton",0,0,984,86,"[  SPACE / CLICK TO RUN  ]",34,new Color(0.82f,0.97f,1),orbitron);
        var tutorial=TerminalButton(actions,"TutorialButton",-270,122,390,78,"协议校准",34,new Color(0.63f,0.9f,1),font);
        var configure=TerminalButton(actions,"LoadoutButton",108,122,242,78,"战前配置",34,new Color(0.63f,0.9f,1),font);
        var shop=TerminalButton(actions,"ShopButton",366,122,160,78,"商店",34,new Color(0.63f,0.9f,1),font);
        ExistingText(actions,"Separator1",-48,122,42,78,"/",30,new Color(0.3f,0.46f,0.6f),TextAnchor.MiddleCenter).font=orbitron;
        ExistingText(actions,"Separator2",272,122,42,78,"/",30,new Color(0.3f,0.46f,0.6f),TextAnchor.MiddleCenter).font=orbitron;
        var summary=ExistingText(body,"EquippedLoadout",0,1198,1032,66,"LOADOUT 标准弹珠 | Q:协议改向 E:斩杀武装 | FLIP:重炮",26,new Color(0.67f,0.86f,0.94f,0.72f),TextAnchor.MiddleCenter);
        var edit=TerminalButton(body,"EditButton",0,1262,180,56,"[ EDIT ]",23,Cyan,orbitron);
        var telemetry=ExistingText(body,"EquippedSkills",0,1332,984,56,"WAVE — · COMBO — · BOSS — · PROTO 1",24,new Color(0.43f,0.55f,0.62f),TextAnchor.MiddleCenter); telemetry.font=orbitron;
        var footer=ExistingRect(body,"Footer",0,1838,1008,82);
        var footerBackground=footer.GetComponent<UnityEngine.UI.Image>(); if(footerBackground==null) footerBackground=footer.gameObject.AddComponent<UnityEngine.UI.Image>(); footerBackground.color=new Color(0.015f,0.025f,0.045f,0.55f); footerBackground.raycastTarget=false;
        var hint=ExistingText(footer,"FooterHint",-166,0,676,82,"> AWAITING OPERATOR INPUT_",22,new Color(0,0.9f,1,0.65f),TextAnchor.MiddleLeft); hint.font=orbitron;
        var settings=TerminalButton(footer,"SettingsButton",364,12,78,58,"设置",22,Muted,font);
        var quit=TerminalButton(footer,"QuitButton",456,12,78,58,"退出",22,Muted,font);
        Assign(view,"startButton",start); Assign(view,"loadoutButton",configure); Assign(view,"shopButton",shop); Assign(view,"tutorialButton",tutorial); Assign(view,"settingsButton",settings); Assign(view,"quitButton",quit); Assign(view,"editButton",edit); Assign(view,"headerSettingsButton",settingsTop);
        Assign(view,"balance",balance); Assign(view,"loadoutSummary",summary); Assign(view,"telemetry",telemetry); Assign(view,"tutorialLabel",tutorial.GetComponentInChildren<UnityEngine.UI.Text>()); Assign(view,"subtitle",subtitle); Assign(view,"startLabel",start.GetComponentInChildren<UnityEngine.UI.Text>());
    }
    private static RectTransform ExistingRect(Transform parent,string name,float x,float y,float w,float h)
    {
        var found=parent.Find(name); var rect=found!=null?found.GetComponent<RectTransform>():Rect(parent,name,x,y,w,h);
        rect.anchorMin=rect.anchorMax=new Vector2(0.5f,1); rect.pivot=new Vector2(0.5f,1); rect.anchoredPosition=new Vector2(x,-y); rect.sizeDelta=new Vector2(w,h); rect.gameObject.SetActive(true); return rect;
    }
    private static UnityEngine.UI.Text ExistingText(Transform parent,string name,float x,float y,float w,float h,string value,int size,Color c,TextAnchor alignment)
    {
        var rect=ExistingRect(parent,name,x,y,w,h); var text=rect.GetComponent<UnityEngine.UI.Text>(); if(text==null) text=rect.gameObject.AddComponent<UnityEngine.UI.Text>();
        text.font=font; text.fontSize=size; text.text=value; text.color=c; text.alignment=alignment; text.raycastTarget=false; text.horizontalOverflow=HorizontalWrapMode.Wrap; text.verticalOverflow=VerticalWrapMode.Overflow; return text;
    }
    private static UnityEngine.UI.Button TerminalButton(Transform parent,string name,float x,float y,float w,float h,string label,int size,Color c,Font labelFont)
    {
        var rect=ExistingRect(parent,name,x,y,w,h); var image=rect.GetComponent<UnityEngine.UI.Image>(); if(image==null) image=rect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color=Color.clear; image.raycastTarget=true;
        var button=rect.GetComponent<UnityEngine.UI.Button>(); if(button==null) button=rect.gameObject.AddComponent<UnityEngine.UI.Button>();
        var text=ExistingText(rect,"Label",0,0,w,h,label,size,c,TextAnchor.MiddleCenter); text.font=labelFont; button.targetGraphic=text;
        var oldFrame=rect.Find("NeonFrame"); if(oldFrame!=null) oldFrame.gameObject.SetActive(false);
        return button;
    }
    private static LoadoutCanvasView BuildLoadout()
    {
        var root = Root("LoadoutCanvas"); var body = Rect(root.transform,"EditableLayout",0,0,1080,1920);
        var view = root.AddComponent<LoadoutCanvasView>(); BaseReferences(view,root);
        var header = Rect(body,"Header",0,48,984,100);
        var back = Button(header,"BackButton",-414,0,148,76,"‹ 返回",32,false);
        Text(header,"Title",0,0,400,90,"战前配置",54,Color.white,TextAnchor.MiddleCenter);
        var ready = Text(header,"ReadyStatus",342,20,300,60,"● 准备就绪",29,Cyan,TextAnchor.MiddleRight);
        Text(body,"Subtitle",0,164,984,50,"选择弹珠与挡板武器，迎接新的挑战",30,Muted,TextAnchor.MiddleCenter);
        Box(body,"HeaderDivider",0,232,984,1,new Color(0.18f,0.7f,0.8f,0.25f));
        var content = Rect(body,"Configuration",0,270,984,914);
        var left = Rect(content,"BallColumn",-382,0,220,914);
        Text(left,"Heading",0,0,220,52,"弹珠列表",34,Color.white);
        var balls = Scroll(left,"BallList",0,70,220,814,false);
        var center = Rect(content,"BallDetail",-50,0,400,914);
        var hero = Rect(center,"Hero",0,82,400,488);
        Disc(hero,"NeonIdentityDisc",0,352,450,118);
        var preview = Ball(hero,"BallPreview",0,0,408,408);
        var ballName = Text(center,"BallName",0,574,422,74,"标准弹珠",42,Color.white,TextAnchor.MiddleCenter);
        var ballSummary = Text(center,"BallSummary",0,655,400,100,"身份协议：斩杀武装\n固定双技能 · 选择后装备",29,Muted,TextAnchor.MiddleCenter);
        var equip = Button(center,"EquipButton",0,780,390,88,"✓ 已装备",34,true);
        var goShop = Button(center,"UnlockButton",0,884,390,56,"前往协议商店  ›",28,false);
        goShop.gameObject.SetActive(false);
        var right = Rect(content,"FixedSkills",324,0,332,914);
        Text(right,"Heading",0,0,332,52,"固定技能",34,Color.white);
        var names = new UnityEngine.UI.Text[2]; var descriptions = new UnityEngine.UI.Text[2]; var metadata = new UnityEngine.UI.Text[2];
        for(int i=0;i<2;i++)
        {
            var card=Box(right,"SkillSlot"+(i+1),0,70+i*358,332,334,new Color(0.021f,0.055f,0.075f));
            Box(card,"Accent",-164,0,3,334,new Color(0.18f,0.86f,1f,0.5f));
            names[i]=Text(card,"SkillName",0,20,284,52,i==0?"协议改向":"斩杀武装",34,Color.white);
            metadata[i]=Text(card,"CooldownAndMode",0,84,284,72,i==0?"右键 / Q · 8 秒冷却\n瞄准释放":"E · 12 秒冷却\n即时释放",26,Cyan);
            descriptions[i]=Text(card,"SkillDescription",0,168,284,150,i==0?"进入时缓瞄准，选择方向后确认弹珠的新弹道。":"武装弹珠，配合协议改向触发连锁斩杀。",28,new Color(0.84f,0.9f,0.93f));
        }
        Text(right,"FixedSkillHint",0,805,332,104,"两项技能随弹珠固定绑定\n无法在此更换",27,Muted);
        Box(body,"WeaponsDivider",0,1220,984,1,new Color(0.18f,0.7f,0.8f,0.25f));
        Text(body,"WeaponsHeading",-258,1246,468,56,"挡板武器",38,Color.white);
        Text(body,"WeaponsHint",272,1246,440,56,"点击卡片直接装备",27,Muted,TextAnchor.MiddleRight);
        var weapons=Scroll(body,"WeaponCarousel",0,1328,984,352,true);
        var ballTemplate=Choice(body,"BallCardTemplate",220,192,true);
        var weaponTemplate=Choice(body,"WeaponCardTemplate",310,352,false);
        var launchSummary=Text(body,"LaunchSummary",0,1710,984,54,"出战：标准弹珠 · 重炮",30,Muted,TextAnchor.MiddleCenter);
        var launch=Button(body,"LaunchButton",0,1784,984,104,"开始挑战   ›",44,true);
        Assign(view,"backButton",back); Assign(view,"equipButton",equip); Assign(view,"shopButton",goShop); Assign(view,"launchButton",launch);
        Assign(view,"readyStatus",ready); Assign(view,"ballName",ballName); Assign(view,"ballSummary",ballSummary); Assign(view,"equipLabel",equip.GetComponentInChildren<UnityEngine.UI.Text>()); Assign(view,"launchSummary",launchSummary); Assign(view,"ballPreview",preview);
        Assign(view,"ballList",balls.content); Assign(view,"weaponList",weapons.content); Assign(view,"ballTemplate",ballTemplate); Assign(view,"weaponTemplate",weaponTemplate);
        AssignArray(view,"skillNames",names); AssignArray(view,"skillDescriptions",descriptions); AssignArray(view,"skillMetadata",metadata);
        balls.content.anchoredPosition=Vector2.zero; weapons.content.anchoredPosition=Vector2.zero;
        PrefabUtility.SaveAsPrefabAssetAndConnect(root,"Assets/UI/Loadout/LoadoutCanvas.prefab",InteractionMode.AutomatedAction);
        return view;
    }
    private static LoadoutChoiceCard Choice(Transform parent,string name,float w,float h,bool ball)
    {
        var button=Button(parent,name,0,0,w,h,"",28,false);
        Object.DestroyImmediate(button.GetComponentInChildren<UnityEngine.UI.Text>().gameObject);
        UnityEngine.UI.RawImage icon=null; LoadoutWeaponGlyph glyph=null; UnityEngine.UI.Text description=null;
        if(ball) icon=Ball(button.transform,"BallIcon",0,8,96,96);
        else
        {
            glyph=Rect(button.transform,"WeaponPreview",0,12,266,128).gameObject.AddComponent<LoadoutWeaponGlyph>(); glyph.color=Cyan; glyph.raycastTarget=false;
            description=Text(button.transform,"Description",0,206,w-36,98,"集中火力\n高额单体伤害",28,Muted,TextAnchor.UpperCenter);
        }
        var title=Text(button.transform,"Name",0,ball?108:152,w-24,52,ball?"标准弹珠":"重炮",ball?29:36,Color.white,TextAnchor.MiddleCenter);
        var state=Text(button.transform,"State",0,ball?154:310,w-24,38,"未装备",ball?26:28,Muted,TextAnchor.MiddleCenter);
        var card=button.gameObject.AddComponent<LoadoutChoiceCard>();
        Assign(card,"button",button); Assign(card,"title",title); Assign(card,"state",state); Assign(card,"description",description); Assign(card,"ballIcon",icon); Assign(card,"weaponIcon",glyph); Assign(card,"frame",button.GetComponentInChildren<ShopNeonFrame>());
        button.gameObject.SetActive(false); return card;
    }
    private static GameObject Root(string name)
    {
        var root=new GameObject(name,typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster),typeof(CanvasGroup));
        Undo.RegisterCreatedObjectUndo(root,"Create editable menus");
        var canvas=root.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=20;
        var scaler=root.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(1080,1920); scaler.matchWidthOrHeight=1;
        var background=Box(root.transform,"Background",0,0,1080,1920,new Color(0.008f,0.022f,0.032f));
        background.anchorMin=Vector2.zero; background.anchorMax=Vector2.one; background.offsetMin=background.offsetMax=Vector2.zero; background.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
        return root;
    }
    private static void BaseReferences(Object view,GameObject root) { Assign(view,"canvas",root.GetComponent<Canvas>()); Assign(view,"visibility",root.GetComponent<CanvasGroup>()); }
    private static void Disc(Transform parent,string name,float x,float y,float w,float h)
    {
        var r=Rect(parent,name,x,y,w,h); r.gameObject.AddComponent<Canvas>(); var disc=r.gameObject.AddComponent<ShopNeonDisc>(); disc.color=Cyan; disc.raycastTarget=false;
    }
    private static UnityEngine.UI.ScrollRect Scroll(Transform parent,string name,float x,float y,float w,float h,bool horizontal)
    {
        var scroll=Rect(parent,name,x,y,w,h).gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
        var viewport=Box(scroll.transform,"Viewport",0,0,w,h,new Color(0,0,0,0.01f)); viewport.GetComponent<UnityEngine.UI.Image>().raycastTarget=true; viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic=false;
        var content=Rect(viewport,"Content",0,0,horizontal?0:w,horizontal?h:0); content.anchorMin=content.anchorMax=content.pivot=new Vector2(0,1); content.anchoredPosition=Vector2.zero;
        UnityEngine.UI.HorizontalOrVerticalLayoutGroup layout=horizontal?(UnityEngine.UI.HorizontalOrVerticalLayoutGroup)content.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>():content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
        layout.spacing=horizontal?20:16; layout.childControlWidth=layout.childControlHeight=false; layout.childForceExpandWidth=layout.childForceExpandHeight=false;
        var fit=content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>(); if(horizontal) fit.horizontalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize; else fit.verticalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        scroll.content=content; scroll.viewport=viewport; scroll.horizontal=horizontal; scroll.vertical=!horizontal; scroll.inertia=true; scroll.decelerationRate=0.08f; scroll.scrollSensitivity=65;
        return scroll;
    }
    private static RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)
    {
        var go=new GameObject(name,typeof(RectTransform)); go.transform.SetParent(parent,false);
        var r=go.GetComponent<RectTransform>(); r.anchorMin=r.anchorMax=new Vector2(0.5f,1); r.pivot=new Vector2(0.5f,1); r.anchoredPosition=new Vector2(x,-y); r.sizeDelta=new Vector2(w,h); return r;
    }
    private static RectTransform Box(Transform parent,string name,float x,float y,float w,float h,Color c)
    {
        var r=Rect(parent,name,x,y,w,h); var image=r.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color=c; image.raycastTarget=false; return r;
    }
    private static UnityEngine.UI.Text Text(Transform parent,string name,float x,float y,float w,float h,string value,int size,Color c,TextAnchor align=TextAnchor.UpperLeft)
    {
        var t=Rect(parent,name,x,y,w,h).gameObject.AddComponent<UnityEngine.UI.Text>(); t.font=font; t.fontSize=size; t.text=value; t.color=c; t.alignment=align; t.raycastTarget=false; t.horizontalOverflow=HorizontalWrapMode.Wrap; t.verticalOverflow=VerticalWrapMode.Overflow; return t;
    }
    private static UnityEngine.UI.RawImage Ball(Transform parent,string name,float x,float y,float w,float h)
    {
        var image=Rect(parent,name,x,y,w,h).gameObject.AddComponent<UnityEngine.UI.RawImage>(); image.texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/UI/shop_ball_preview.png"); image.raycastTarget=false; return image;
    }
    private static UnityEngine.UI.Button Button(Transform parent,string name,float x,float y,float w,float h,string label,int size,bool primary)
    {
        var r=Box(parent,name,x,y,w,h,primary?new Color(0.035f,0.22f,0.28f):new Color(0.025f,0.06f,0.08f)); r.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
        var button=r.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic=r.GetComponent<UnityEngine.UI.Image>();
        var colors=button.colors; colors.highlightedColor=new Color(0.68f,0.95f,1f); colors.pressedColor=new Color(0.4f,0.75f,0.8f); colors.disabledColor=new Color(0.55f,0.65f,0.7f); button.colors=colors;
        var frame=Rect(r,"NeonFrame",0,0,w,h).gameObject.AddComponent<ShopNeonFrame>(); frame.color=primary?Cyan:new Color(0.18f,0.86f,1f,0.25f); frame.raycastTarget=false;
        Text(r,"Label",0,0,w-24,h,label,size,Color.white,TextAnchor.MiddleCenter); return button;
    }
    private static void Assign(Object target,string field,Object value)
    {
        if(target==null) throw new System.InvalidOperationException("Missing menu host for "+field);
        var so=new SerializedObject(target); var p=so.FindProperty(field); if(p==null) throw new System.InvalidOperationException("Missing serialized field "+field); p.objectReferenceValue=value; so.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void AssignArray(Object target,string field,Object[] values)
    {
        var so=new SerializedObject(target); var p=so.FindProperty(field); p.arraySize=values.Length; for(int i=0;i<values.Length;i++) p.GetArrayElementAtIndex(i).objectReferenceValue=values[i]; so.ApplyModifiedPropertiesWithoutUndo();
    }
}
