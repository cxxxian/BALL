using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>One-time authoring tool. Runtime never rebuilds or repositions the authored layout.</summary>
public static class ShopCanvasBuilder
{
    private static Font font;
    private static readonly Color Cyan = new Color(0.18f, 0.86f, 1f);
    private static readonly Color Muted = new Color(0.59f, 0.72f, 0.78f);
    public static void Build()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Create shop in Edit mode.");
        var existing = Object.FindObjectOfType<ShopCanvasView>(true);
        if (existing != null) { Selection.activeGameObject = existing.gameObject; return; }
        font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSansCJKsc-Regular.otf");
        if (font == null) throw new System.InvalidOperationException("Missing Chinese font.");
        var root = new GameObject("ShopCanvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster), typeof(CanvasGroup));
        Undo.RegisterCreatedObjectUndo(root, "Create editable shop");
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 20;
        var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920); scaler.matchWidthOrHeight = 1;
        var background = Box(root.transform, "Background", 0, 0, 1080, 1920, new Color(0.008f, 0.022f, 0.032f));
        background.anchorMin = Vector2.zero; background.anchorMax = Vector2.one; background.offsetMin = background.offsetMax = Vector2.zero;
        background.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
        var body = Rect(root.transform, "EditableLayout", 0, 0, 1080, 1920);
        var view = root.AddComponent<ShopCanvasView>();
        var header = Rect(body, "Header", 0, 32, 984, 100);
        var back = Button(header, "BackButton", -414, 8, 148, 72, "‹ 返回", 32, false);
        Text(header, "Title", 0, 0, 350, 76, "协议商店", 54, Color.white, TextAnchor.MiddleCenter);
        var balance = Text(header, "Balance", 327, 12, 326, 62, "协议币 999,999", 30, Muted, TextAnchor.MiddleRight);
        Box(body, "HeaderDivider", 0, 146, 984, 1, new Color(0.18f, 0.7f, 0.8f, 0.25f));
        var tabs = Rect(body, "Navigation", 0, 172, 984, 90);
        var directButton = Button(tabs, "DirectTab", -246, 0, 480, 88, "直购弹珠", 38, true);
        var crateButton = Button(tabs, "CrateTab", 246, 0, 480, 88, "协议宝箱", 38, false);
        var directPage = Rect(body, "DirectPage", 0, 280, 984, 1580);
        var hero = Rect(directPage, "Hero", 0, 0, 984, 500);
        var preview = Raw(hero, "BallPreview", 0, 0, 490, 490);
        var disc = Rect(hero, "NeonIdentityDisc", 0, 400, 720, 140).gameObject.AddComponent<ShopNeonDisc>();
        disc.color = Cyan; disc.raycastTarget = false;
        disc.transform.SetAsFirstSibling(); disc.gameObject.AddComponent<Canvas>();
        var details = Rect(directPage, "BallDetails", 0, 568, 984, 100);
        var name = Text(details, "BallName", -180, 0, 600, 72, "时缓协议球", 54, Color.white);
        var rarity = Text(details, "Rarity", 328, 18, 328, 48, "稀有 / RARE", 27, Cyan, TextAnchor.MiddleRight);
        Text(directPage, "SkillsHeading", 0, 663, 984, 48, "绑定技能", 32, Muted);
        var skills = Rect(directPage, "BoundSkills", 0, 726, 984, 402);
        var skillNames = new UnityEngine.UI.Text[2]; var descriptions = new UnityEngine.UI.Text[2]; var metadata = new UnityEngine.UI.Text[2];
        for (int i = 0; i < 2; i++)
        {
            var card = Box(skills, "SkillSlot" + (i + 1), 0, i * 214, 984, 196, new Color(0.025f, 0.065f, 0.085f));
            Box(card, "Accent", -490, 0, 4, 196, Cyan);
            skillNames[i] = Text(card, "SkillName", -38, 14, 848, 52, i == 0 ? "协议改向" : "时间减速", 38, Color.white);
            metadata[i] = Text(card, "CooldownAndMode", -38, 70, 848, 38, "右键 / Q  ·  8 秒冷却  ·  瞄准释放", 27, Cyan);
            descriptions[i] = Text(card, "SkillDescription", -38, 114, 848, 74, "进入时缓瞄准，在 4 秒内选择方向，左键确认弹珠的新弹道。", 32, new Color(0.84f, 0.9f, 0.93f));
        }
        var purchase = Rect(directPage, "PurchaseArea", 0, 1158, 984, 114);
        Text(purchase, "PriceCaption", -330, 0, 300, 38, "永久解锁", 26, Muted);
        var price = Text(purchase, "Price", -330, 38, 300, 60, "400 币", 43, Color.white);
        var buy = Button(purchase, "PurchaseButton", 166, 10, 652, 94, "解锁弹珠  ›", 34, true);
        var buyLabel = buy.GetComponentInChildren<UnityEngine.UI.Text>();
        Box(directPage, "CarouselDivider", 0, 1310, 984, 1, new Color(0.18f, 0.7f, 0.8f, 0.25f));
        Text(directPage, "OffersHeading", -290, 1330, 404, 48, "可购弹珠", 34, Color.white);
        Text(directPage, "DragHint", 268, 1330, 448, 48, "按住鼠标左右拖动  ↔", 27, Muted, TextAnchor.MiddleRight);
        var scroll = Rect(directPage, "OfferCarousel", 0, 1400, 984, 240).gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
        var viewport = Box(scroll.transform, "Viewport", 0, 0, 984, 240, new Color(0, 0, 0, 0.01f));
        viewport.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
        viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;
        var content = Rect(viewport, "Content", 0, 0, 0, 232);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(0, 1); content.pivot = new Vector2(0, 1); content.anchoredPosition = Vector2.zero;
        var layout = content.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        layout.spacing = 20; layout.childControlHeight = false; layout.childControlWidth = false; layout.childForceExpandHeight = false; layout.childForceExpandWidth = false;
        var fit = content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>(); fit.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport; scroll.content = content; scroll.horizontal = true; scroll.vertical = false;
        scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Elastic; scroll.inertia = true; scroll.decelerationRate = 0.08f; scroll.scrollSensitivity = 70;
        var template = Button(body, "OfferCardTemplate", 0, 0, 286, 232, "", 30, false);
        Object.DestroyImmediate(template.GetComponentInChildren<UnityEngine.UI.Text>().gameObject);
        var icon = Raw(template.transform, "BallIcon", 0, 12, 138, 138);
        var cardTitle = Text(template.transform, "Name", 0, 145, 254, 44, "时缓协议球", 32, Color.white, TextAnchor.MiddleCenter);
        var status = Text(template.transform, "State", 0, 192, 254, 32, "已拥有", 27, Muted, TextAnchor.MiddleCenter);
        var line = Box(template.transform, "SelectedIndicator", 0, 0, 286, 4, Cyan).GetComponent<UnityEngine.UI.Image>();
        var cardView = template.gameObject.AddComponent<ShopOfferCard>();
        Assign(cardView, "title", cardTitle); Assign(cardView, "status", status); Assign(cardView, "icon", icon); Assign(cardView, "button", template); Assign(cardView, "selectionLine", line);
        template.gameObject.SetActive(false);
        var cratePage = Rect(body, "CratePage", 0, 300, 984, 1500);
        Text(cratePage, "CrateTitle", 0, 60, 984, 80, "标准协议宝箱", 54, Color.white, TextAnchor.MiddleCenter);
        Text(cratePage, "CrateIntro", 0, 155, 984, 86, "开启随机弹珠，重复获得将折算协议币。", 32, Muted, TextAnchor.MiddleCenter);
        var crateDisc = Rect(cratePage, "CrateLightDisc", 0, 390, 800, 240).gameObject.AddComponent<ShopNeonDisc>(); crateDisc.color = Cyan; crateDisc.raycastTarget = false;
        var seal = Button(cratePage, "ProtocolSeal", 0, 320, 340, 168, "◇  PROTOCOL", 38, true); seal.interactable = false;
        var pity = Text(cratePage, "PityProgress", 0, 718, 984, 130, "再开启 30 次，必得史诗及以上\n保底进度 0 / 30", 36, Color.white);
        var track = Box(cratePage, "PityTrack", 0, 885, 984, 10, new Color(0.035f, 0.13f, 0.16f));
        var fill = Box(track, "Fill", 0, 0, 984, 10, Cyan).GetComponent<UnityEngine.UI.Image>();
        fill.type = UnityEngine.UI.Image.Type.Filled; fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal; fill.fillAmount = 0;
        var open = Button(cratePage, "OpenButton", 0, 965, 984, 110, "开启宝箱    150 币  ›", 38, true);
        Text(cratePage, "ResultHint", 0, 1110, 984, 120, "开启结果将在独立展示区呈现\n获得的弹珠可在战前配置中装备", 30, Muted, TextAnchor.MiddleCenter);
        Assign(view, "canvas", canvas); Assign(view, "visibility", root.GetComponent<CanvasGroup>());
        Assign(view, "backButton", back); Assign(view, "directButton", directButton); Assign(view, "crateButton", crateButton);
        Assign(view, "purchaseButton", buy); Assign(view, "openButton", open); Assign(view, "directPage", directPage.gameObject); Assign(view, "cratePage", cratePage.gameObject);
        Assign(view, "preview", preview); Assign(view, "balance", balance); Assign(view, "ballName", name); Assign(view, "rarity", rarity); Assign(view, "price", price); Assign(view, "purchaseLabel", buyLabel); Assign(view, "pityLabel", pity); Assign(view, "pityFill", fill);
        Assign(view, "cardsContent", content); Assign(view, "cardTemplate", cardView);
        AssignArray(view, "skillNames", skillNames); AssignArray(view, "skillDescriptions", descriptions); AssignArray(view, "skillMetadata", metadata);
        cratePage.gameObject.SetActive(false);
        PrefabUtility.SaveAsPrefabAssetAndConnect(root, "Assets/UI/Shop/ShopCanvas.prefab", InteractionMode.AutomatedAction);
        var controller = Object.FindObjectOfType<ShopPanelController>(); Assign(controller, "canvasView", view);
        EditorSceneManager.MarkSceneDirty(root.scene); EditorSceneManager.SaveScene(root.scene);
        Selection.activeGameObject = body.gameObject;
        AssetDatabase.SaveAssets();
    }
    private static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
        var r = go.GetComponent<RectTransform>(); r.anchorMin = r.anchorMax = new Vector2(0.5f, 1); r.pivot = new Vector2(0.5f, 1); r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h); return r;
    }
    private static RectTransform Box(Transform parent, string name, float x, float y, float w, float h, Color color)
    {
        var r = Rect(parent, name, x, y, w, h); var image = r.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = color; image.raycastTarget = false; return r;
    }
    private static UnityEngine.UI.Text Text(Transform parent, string name, float x, float y, float w, float h, string value, int size, Color color, TextAnchor align = TextAnchor.UpperLeft)
    {
        var r = Rect(parent, name, x, y, w, h); var text = r.gameObject.AddComponent<UnityEngine.UI.Text>(); text.font = font; text.fontSize = size; text.color = color; text.text = value; text.alignment = align; text.raycastTarget = false; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow; return text;
    }
    private static UnityEngine.UI.RawImage Raw(Transform parent, string name, float x, float y, float w, float h)
    {
        var r = Rect(parent, name, x, y, w, h); var image = r.gameObject.AddComponent<UnityEngine.UI.RawImage>(); image.texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/UI/shop_ball_preview.png"); image.raycastTarget = false; return image;
    }
    private static UnityEngine.UI.Button Button(Transform parent, string name, float x, float y, float w, float h, string label, int size, bool primary)
    {
        var r = Box(parent, name, x, y, w, h, primary ? new Color(0.035f, 0.22f, 0.28f) : new Color(0.025f, 0.06f, 0.08f));
        r.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
        var button = r.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = r.GetComponent<UnityEngine.UI.Image>();
        var colors = button.colors; colors.highlightedColor = new Color(0.68f, 0.95f, 1f); colors.pressedColor = new Color(0.4f, 0.75f, 0.8f); colors.disabledColor = new Color(0.55f, 0.65f, 0.7f); button.colors = colors;
        var frame = Rect(r, "NeonFrame", 0, 0, w, h).gameObject.AddComponent<ShopNeonFrame>();
        frame.color = primary ? Cyan : new Color(0.15f, 0.4f, 0.5f); frame.raycastTarget = false;
        Text(r, "Label", 0, 0, w - 24, h, label, size, Color.white, TextAnchor.MiddleCenter); return button;
    }
    private static void Assign(Object target, string field, Object value)
    {
        var so = new SerializedObject(target); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void AssignArray(Object target, string field, Object[] values)
    {
        var so = new SerializedObject(target); var p = so.FindProperty(field); p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i]; so.ApplyModifiedPropertiesWithoutUndo();
    }
}
