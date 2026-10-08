using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class UIWorkbenchWindow : EditorWindow
{
    [Serializable]
    public sealed class Document
    {
        public string path = "", baseline = "", draft = "";
        public bool conflict;
        public bool Dirty { get { return draft != baseline; } }
        public void CheckDisk()
        {
            if (string.IsNullOrEmpty(path)) return;
            if (!File.Exists(path)) { conflict = true; return; }
            string disk = File.ReadAllText(path);
            if (disk == baseline) { conflict = false; return; }
            if (Dirty) conflict = true;
            else { baseline = draft = disk; conflict = false; }
        }
        public bool CanSave { get { return !string.IsNullOrEmpty(path) && File.Exists(path) && File.ReadAllText(path) == baseline; } }
        public string Save()
        {
            if (!Dirty) return "";
            if (!CanSave) throw new IOException("文件已在外部改变，已阻止覆盖：" + path);
            string backupDir = "Library/UIWorkbench/Backups";
            Directory.CreateDirectory(backupDir);
            string backup = backupDir + "/" + Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N") + ".bak";
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temp, draft, new System.Text.UTF8Encoding(false));
                if (!CanSave) throw new IOException("保存前检测到外部修改，已阻止覆盖：" + path);
                File.Replace(temp, path, backup);
                baseline = draft;
                conflict = false;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                return backup;
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }

    [SerializeField] private Document layout = new Document();
    [SerializeField] private Document stylesheet = new Document();
    [SerializeField] private string selector = "";
    [SerializeField] private int previewWidth = 1080, previewHeight = 1920;
    [SerializeField] private bool editLayout;
    private string message = "";
    private string scratchLayout = "", scratchStyle = "";
    private VisualElement preview;
    private ScrollView hierarchy;
    private Vector2 sourceScroll;
    private double nextCheck, previewDue;
    private bool previewPending;
    [SerializeField] private int toolVersion;
    [SerializeField] private bool showSource, fitCanvas = true;
    [SerializeField] private float zoom = .3f;
    private VisualElement canvasViewport, canvasFrame, outline, selectedElement;
    private Vector2 inspectorScroll;
    [SerializeField] private Vector2 pan;
    [SerializeField] private string selectedElementPath = "";
    private bool draggingCanvas;
    private int dragPointer = -1;
    private Vector2 dragPosition, lastPickPosition = new Vector2(float.NaN, float.NaN);
    private int pickCycle;
    private string dependencySignature = "";

    [MenuItem("Rebound Protocol/UI Workbench")]
    public static void Open()
    {
        var window = GetWindow<UIWorkbenchWindow>("UI 调整工具");
        window.minSize = new Vector2(850, 550);
        if (Selection.activeObject is VisualTreeAsset || Selection.activeObject is StyleSheet)
            window.SelectAsset(Selection.activeObject);
        else if (string.IsNullOrEmpty(window.layout.path))
        {
            var doc = UnityEngine.Object.FindObjectOfType<UIDocument>();
            if (doc != null && doc.visualTreeAsset != null) window.SelectAsset(doc.visualTreeAsset);
        }
    }

    [MenuItem("Assets/UI 调整工具", true)]
    private static bool CanOpenAsset() { return Selection.activeObject is VisualTreeAsset || Selection.activeObject is StyleSheet; }
    [MenuItem("Assets/UI 调整工具")]
    private static void OpenAsset() { Open(); }

    private void OnEnable()
    {
        titleContent = new GUIContent("UI 调整工具");
        if (toolVersion < 2) { previewWidth = 1080; previewHeight = 1920; toolVersion = 2; }
        saveChangesMessage = "UI 草稿尚未保存。保存会检查外部冲突并备份原文件。";
        EditorApplication.update += Tick;
        Undo.undoRedoPerformed += SchedulePreview;
        SchedulePreview();
    }

    public void CreateGUI()
    {
        rootVisualElement.Clear();
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.flexGrow = 1;
        rootVisualElement.Add(row);
        var controls = new IMGUIContainer(DrawControls);
        controls.style.width = 280; controls.style.flexShrink = 0;
        row.Add(controls);
        var center = new VisualElement();
        center.style.flexGrow = 1; center.style.minWidth = 100;
        row.Add(center);
        center.Add(new Label("左键选元素（重复点击切换重叠项） · 滚轮缩放 · 右键拖动画布"));
        canvasViewport = new VisualElement();
        canvasViewport.style.flexGrow = 1;
        canvasViewport.style.overflow = Overflow.Hidden;
        canvasViewport.style.backgroundColor = new Color(.10f,.11f,.13f);
        canvasViewport.style.alignItems = Align.Center;
        canvasViewport.style.justifyContent = Justify.Center;
        center.Add(canvasViewport);
        canvasFrame = new VisualElement();
        canvasFrame.style.flexShrink = 0;
        canvasFrame.style.position = Position.Absolute;
        canvasViewport.Add(canvasFrame);
        preview = new VisualElement();
        preview.style.position = Position.Absolute;
        preview.style.left = 0; preview.style.top = 0;
        preview.style.transformOrigin = new TransformOrigin(0,0,0);
        canvasFrame.Add(preview);
        outline = new VisualElement { pickingMode = PickingMode.Ignore };
        outline.style.position = Position.Absolute;
        outline.style.borderLeftWidth = outline.style.borderRightWidth = outline.style.borderTopWidth = outline.style.borderBottomWidth = 2;
        outline.style.borderLeftColor = outline.style.borderRightColor = outline.style.borderTopColor = outline.style.borderBottomColor = new Color(.2f,.8f,1);
        outline.style.display = DisplayStyle.None;
        canvasFrame.Add(outline);
        canvasViewport.RegisterCallback<PointerDownEvent>(OnCanvasPointerDown, TrickleDown.TrickleDown);
        canvasViewport.RegisterCallback<PointerMoveEvent>(OnCanvasPointerMove, TrickleDown.TrickleDown);
        canvasViewport.RegisterCallback<PointerUpEvent>(OnCanvasPointerUp, TrickleDown.TrickleDown);
        canvasViewport.RegisterCallback<PointerCaptureOutEvent>(evt => { draggingCanvas = false; dragPointer = -1; });
        canvasViewport.RegisterCallback<WheelEvent>(OnCanvasWheel, TrickleDown.TrickleDown);
        canvasViewport.RegisterCallback<ContextClickEvent>(evt => { evt.StopImmediatePropagation(); evt.PreventDefault(); });
        canvasViewport.RegisterCallback<GeometryChangedEvent>(evt => FitPreview());
        hierarchy = new ScrollView();
        hierarchy.style.height = 110;
        center.Add(new Label("元素列表（被遮挡的元素也可在这里选择）"));
        center.Add(hierarchy);
        var inspector = new IMGUIContainer(DrawInspector);
        inspector.style.width = 320; inspector.style.flexShrink = 0;
        row.Add(inspector);
        SchedulePreview();
    }

    private void FitPreview()
    {
        if (preview == null || canvasViewport == null) return;
        float availableWidth = canvasViewport.contentRect.width - 24;
        float availableHeight = canvasViewport.contentRect.height - 24;
        float scale = fitCanvas ? Mathf.Min(availableWidth / previewWidth, availableHeight / previewHeight) : zoom;
        if (scale <= 0 || float.IsNaN(scale)) return;
        zoom = Mathf.Clamp(scale, .05f, 4);
        preview.style.scale = new Scale(new Vector3(zoom, zoom, 1));
        canvasFrame.style.width = previewWidth * zoom;
        canvasFrame.style.height = previewHeight * zoom;
        if (fitCanvas) pan = Vector2.zero;
        Vector2 origin = CanvasOrigin(zoom);
        canvasFrame.style.left = origin.x;
        canvasFrame.style.top = origin.y;
        UpdateOutline();
    }

    private Vector2 CanvasOrigin(float scale)
    {
        return canvasViewport.contentRect.center - new Vector2(previewWidth, previewHeight) * scale * .5f + pan;
    }

    private void ZoomAt(Vector2 viewportPosition, float requestedZoom)
    {
        float oldZoom = zoom;
        if (oldZoom <= 0) return;
        Vector2 canvasPoint = (viewportPosition - CanvasOrigin(oldZoom)) / oldZoom;
        fitCanvas = false;
        zoom = Mathf.Clamp(requestedZoom, .05f, 4f);
        pan = viewportPosition - canvasViewport.contentRect.center + new Vector2(previewWidth, previewHeight) * zoom * .5f - canvasPoint * zoom;
        lastPickPosition = new Vector2(float.NaN, float.NaN);
        FitPreview();
        Repaint();
    }

    private void OnCanvasWheel(WheelEvent evt)
    {
        if (Mathf.Abs(evt.delta.y) > .001f)
            ZoomAt(canvasViewport.WorldToLocal(evt.mousePosition), zoom * Mathf.Pow(1.12f, -evt.delta.y / 3f));
        evt.StopImmediatePropagation();
        evt.PreventDefault();
    }

    private void OnCanvasPointerDown(PointerDownEvent evt)
    {
        if (evt.button == 1)
        {
            fitCanvas = false;
            draggingCanvas = true; dragPointer = evt.pointerId;
            dragPosition = evt.position;
            canvasViewport.CapturePointer(dragPointer);
            lastPickPosition = new Vector2(float.NaN, float.NaN);
        }
        else if (evt.button == 0)
            PickCanvasElement(evt.position);
        evt.StopImmediatePropagation();
        evt.PreventDefault();
    }

    private void OnCanvasPointerMove(PointerMoveEvent evt)
    {
        if (!draggingCanvas || evt.pointerId != dragPointer) return;
        if ((evt.pressedButtons & 2) == 0) { EndCanvasDrag(); return; }
        Vector2 current = evt.position;
        pan += current - dragPosition;
        dragPosition = current;
        FitPreview();
        Repaint();
        evt.StopImmediatePropagation();
        evt.PreventDefault();
    }

    private void OnCanvasPointerUp(PointerUpEvent evt)
    {
        if (!draggingCanvas || evt.pointerId != dragPointer || evt.button != 1) return;
        EndCanvasDrag();
        evt.StopImmediatePropagation();
        evt.PreventDefault();
    }

    private void EndCanvasDrag()
    {
        int pointer = dragPointer;
        draggingCanvas = false; dragPointer = -1;
        if (canvasViewport != null && pointer >= 0 && canvasViewport.HasPointerCapture(pointer))
            canvasViewport.ReleasePointer(pointer);
    }

    private string ElementPath(VisualElement element)
    {
        var indices = new List<string>();
        while (element != null && element != preview && element.parent != null)
        {
            indices.Insert(0, element.parent.IndexOf(element).ToString());
            element = element.parent;
        }
        return string.Join("/", indices);
    }

    private void CollectHits(VisualElement element, Vector2 panelPosition, List<VisualElement> hits)
    {
        if (element.resolvedStyle.display == DisplayStyle.None || element.resolvedStyle.visibility == Visibility.Hidden || element.resolvedStyle.opacity <= 0) return;
        Vector2 local = element.WorldToLocal(panelPosition);
        bool contains = new Rect(Vector2.zero, element.layout.size).Contains(local);
        if (element.style.overflow.value == Overflow.Hidden && !contains) return;
        // Reverse paint order, descendants before containers; ignore UI pickingMode.
        for (int i = element.childCount - 1; i >= 0; i--)
            CollectHits(element[i], panelPosition, hits);
        if (element != preview && contains && ElementSelector(element) != "")
            hits.Add(element);
    }

    private static bool HasPaintedContent(VisualElement element)
    {
        var style = element.resolvedStyle;
        var text = element as TextElement;
        if (text != null && !string.IsNullOrEmpty(text.text) && style.color.a > 0) return true;
        var image = element as Image;
        if (image != null && (image.image != null || image.vectorImage != null)) return true;
        var background = style.backgroundImage;
        if (style.backgroundColor.a > 0 || background.texture != null || background.sprite != null || background.vectorImage != null) return true;
        return (style.borderLeftWidth > 0 && style.borderLeftColor.a > 0) ||
            (style.borderRightWidth > 0 && style.borderRightColor.a > 0) ||
            (style.borderTopWidth > 0 && style.borderTopColor.a > 0) ||
            (style.borderBottomWidth > 0 && style.borderBottomColor.a > 0);
    }

    private void PickCanvasElement(Vector2 panelPosition)
    {
        var hits = new List<VisualElement>();
        CollectHits(preview, panelPosition, hits);
        // Prefer small, specific UI controls over large overlapping containers.
        // Preserve reverse paint order when areas match.
        var drawOrder = new Dictionary<VisualElement, int>();
        for (int i = 0; i < hits.Count; i++) drawOrder[hits[i]] = i;
        hits.Sort((a, b) =>
        {
            int paintedComparison = HasPaintedContent(b).CompareTo(HasPaintedContent(a));
            if (paintedComparison != 0) return paintedComparison;
            float aArea = a.worldBound.width * a.worldBound.height;
            float bArea = b.worldBound.width * b.worldBound.height;
            int comparison = aArea.CompareTo(bArea);
            return comparison != 0 ? comparison : drawOrder[a].CompareTo(drawOrder[b]);
        });
        if (hits.Count == 0)
        {
            message = "此位置没有可编辑元素。可从元素列表选择被遮挡或隐藏的元素。";
            Repaint();
            return;
        }
        if (!float.IsNaN(lastPickPosition.x) && Vector2.Distance(panelPosition, lastPickPosition) < 4)
            pickCycle = (pickCycle + 1) % hits.Count;
        else pickCycle = 0;
        lastPickPosition = panelPosition;
        SelectElement(hits[pickCycle], ElementSelector(hits[pickCycle]));
        if (hits.Count > 1) message = "重叠候选 " + (pickCycle + 1) + " / " + hits.Count + "，再次点击此位置切换。";
    }

    private void UpdateOutline()
    {
        if (outline == null || selectedElement == null || selectedElement.panel == null) { if (outline != null) outline.style.display = DisplayStyle.None; return; }
        Rect bounds = selectedElement.worldBound;
        Vector2 topLeft = canvasFrame.WorldToLocal(bounds.position);
        outline.style.left = topLeft.x; outline.style.top = topLeft.y;
        outline.style.width = bounds.width; outline.style.height = bounds.height;
        outline.style.display = DisplayStyle.Flex;
    }

    private static string ElementSelector(VisualElement element)
    {
        if (!string.IsNullOrEmpty(element.name) && Regex.IsMatch(element.name, @"^[A-Za-z_][A-Za-z0-9_-]*$")) return "#" + element.name;
        foreach (string c in element.GetClasses())
            if (!c.StartsWith("unity-") && Regex.IsMatch(c, @"^[A-Za-z_][A-Za-z0-9_-]*$")) return "." + c;
        return "";
    }

    private void SelectElement(VisualElement element, string target)
    {
        selectedElement = element;
        selectedElementPath = ElementPath(element);
        selector = target;
        message = "";
        UpdateOutline();
        Repaint();
    }

    private void Tick()
    {
        if (EditorApplication.timeSinceStartup >= nextCheck)
        {
            nextCheck = EditorApplication.timeSinceStartup + .5;
            string oldLayout = layout.draft, oldStyle = stylesheet.draft;
            try
            {
                layout.CheckDisk(); stylesheet.CheckDisk();
                if (oldLayout != layout.draft || oldStyle != stylesheet.draft)
                {
                    if (File.Exists(layout.path)) AssetDatabase.ImportAsset(layout.path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                    if (File.Exists(stylesheet.path)) AssetDatabase.ImportAsset(stylesheet.path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                    message = "已读取外部文件的最新版本。";
                    SchedulePreview();
                }
                string signature = DependencySignature();
                if (signature != dependencySignature)
                {
                    dependencySignature = signature;
                    SchedulePreview();
                }
            }
            catch (Exception e) { message = e.Message; }
            hasUnsavedChanges = layout.Dirty || stylesheet.Dirty;
            UpdateOutline(); Repaint();
        }
        if (previewPending && EditorApplication.timeSinceStartup >= previewDue && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
        {
            previewPending = false;
            RebuildPreview();
        }
    }

    private string DependencySignature()
    {
        var signature = new System.Text.StringBuilder();
        var paths = string.IsNullOrEmpty(layout.path) ? new string[0] : AssetDatabase.GetDependencies(layout.path, true);
        foreach (string path in paths)
        {
            if (path == layout.path || path == stylesheet.path || !File.Exists(path)) continue;
            signature.Append(path).Append(File.GetLastWriteTimeUtc(path).Ticks).Append(new FileInfo(path).Length);
        }
        return signature.ToString();
    }

    private void DrawControls()
    {
        EditorGUILayout.LabelField("通用 UXML / USS 调整工具", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("源文件每 0.5 秒检查。调整先进入草稿；关闭、退出或刷新不会自动写回。预览不执行游戏脚本。", MessageType.Info);
        DrawAsset("UXML", layout, typeof(VisualTreeAsset));
        DrawAsset("USS", stylesheet, typeof(StyleSheet));
        if (GUILayout.Button("从当前 Project 选择打开")) SelectAsset(Selection.activeObject);
        if (layout.conflict || stylesheet.conflict)
            EditorGUILayout.HelpBox("外部文件已修改或删除。草稿已保留，保存被阻止。可导出草稿，或重新加载最新文件。", MessageType.Warning);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("保存到源文件")) SaveDocuments();
        if (GUILayout.Button("重新加载")) ReloadFromDisk();
        if (GUILayout.Button("导出草稿")) ExportDraft();
        EditorGUILayout.EndHorizontal();
        if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.Info);

        EditorGUI.BeginChangeCheck();
        previewWidth = Mathf.Clamp(EditorGUILayout.IntField("预览宽度", previewWidth), 100, 4096);
        previewHeight = Mathf.Clamp(EditorGUILayout.IntField("预览高度", previewHeight), 100, 4096);
        if (EditorGUI.EndChangeCheck()) SchedulePreview();
        fitCanvas = EditorGUILayout.Toggle("自动适应窗口", fitCanvas);
        if (GUILayout.Button("适应并居中"))
        {
            fitCanvas = true; pan = Vector2.zero;
            lastPickPosition = new Vector2(float.NaN, float.NaN);
            FitPreview();
        }
        using (new EditorGUI.DisabledScope(fitCanvas))
        {
            EditorGUI.BeginChangeCheck();
            zoom = EditorGUILayout.Slider("画布缩放", zoom, .05f, 4f);
            if (EditorGUI.EndChangeCheck()) FitPreview();
        }
        FitPreview();
        EditorGUILayout.Space();
        showSource = EditorGUILayout.Foldout(showSource, "高级：源码编辑", true);
        if (!showSource) return;
        editLayout = GUILayout.Toolbar(editLayout ? 0 : 1, new[] { "UXML 源码", "USS 源码" }) == 0;
        var doc = editLayout ? layout : stylesheet;
        EditorGUILayout.LabelField(doc.path + (doc.Dirty ? "  * 未保存" : ""), EditorStyles.wordWrappedMiniLabel);
        using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(doc.path)))
        {
            sourceScroll = EditorGUILayout.BeginScrollView(sourceScroll, GUILayout.Height(Mathf.Max(130, position.height - 395 - (string.IsNullOrEmpty(message) ? 0 : 50) - (layout.conflict || stylesheet.conflict ? 65 : 0))));
            EditorGUI.BeginChangeCheck();
            string text = EditorGUILayout.TextArea(doc.draft, GUILayout.ExpandHeight(true));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(this, "Edit UI draft");
                doc.draft = text;
                hasUnsavedChanges = layout.Dirty || stylesheet.Dirty;
                SchedulePreview();
            }
            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawInspector()
    {
        EditorGUILayout.LabelField("元素参数", EditorStyles.boldLabel);
        if (selectedElement == null)
        {
            EditorGUILayout.HelpBox("直接点击画布中的 UI，或从下方元素列表选择。", MessageType.Info);
            return;
        }
        EditorGUILayout.LabelField(selectedElement.GetType().Name + "  " + selector, EditorStyles.wordWrappedLabel);
        if (selector.StartsWith(".")) EditorGUILayout.HelpBox("此元素使用共享样式类，调整会作用于同类元素。单独调整请为元素设置唯一 name。", MessageType.Info);
        if (string.IsNullOrEmpty(stylesheet.path))
        {
            EditorGUILayout.HelpBox("请先在左侧选择 USS，或点击下方按钮创建样式文件。", MessageType.Info);
            if (GUILayout.Button("创建关联 USS")) CreateStylesheet();
            return;
        }
        EditorGUILayout.LabelField("修改即时预览 · ↶ 恢复原样式", EditorStyles.miniLabel);
        inspectorScroll = EditorGUILayout.BeginScrollView(inspectorScroll, GUILayout.Height(Mathf.Max(200, position.height - 115 - (selector.StartsWith(".") ? 65 : 0))));
        var e = selectedElement;
        var r = e.resolvedStyle;
        Section("尺寸");
        NumberControl("宽度", "width", r.width, v => e.style.width = v);
        NumberControl("高度", "height", r.height, v => e.style.height = v);
        Section("布局");
        EnumControl("排列方向", "flex-direction", r.flexDirection, v => e.style.flexDirection = v,
            new[] { FlexDirection.Column, FlexDirection.Row, FlexDirection.ColumnReverse, FlexDirection.RowReverse }, new[] { "竖直", "水平", "竖直反向", "水平反向" });
        EnumControl("主轴对齐", "justify-content", r.justifyContent, v => e.style.justifyContent = v,
            new[] { Justify.FlexStart, Justify.Center, Justify.FlexEnd, Justify.SpaceBetween, Justify.SpaceAround },
            new[] { "起点", "居中", "终点", "两端对齐", "均匀环绕" });
        EnumControl("交叉轴对齐", "align-items", r.alignItems, v => e.style.alignItems = v,
            new[] { Align.FlexStart, Align.Center, Align.FlexEnd, Align.Stretch },
            new[] { "起点", "居中", "终点", "拉伸" });
        ScalarControl("剩余空间占比", "flex-grow", r.flexGrow, v => e.style.flexGrow = v);
        ScalarControl("空间不足时收缩", "flex-shrink", r.flexShrink, v => e.style.flexShrink = v);
        Section("位置");
        EnumControl("定位方式", "position", r.position, v => e.style.position = v,
            new[] { Position.Relative, Position.Absolute }, new[] { "随布局排列", "绝对定位" });
        NumberControl("左", "left", r.left, v => e.style.left = v);
        NumberControl("上", "top", r.top, v => e.style.top = v);
        NumberControl("右", "right", r.right, v => e.style.right = v);
        NumberControl("下", "bottom", r.bottom, v => e.style.bottom = v);
        Section("外间距");
        NumberControl("左外距", "margin-left", r.marginLeft, v => e.style.marginLeft = v);
        NumberControl("上外距", "margin-top", r.marginTop, v => e.style.marginTop = v);
        NumberControl("右外距", "margin-right", r.marginRight, v => e.style.marginRight = v);
        NumberControl("下外距", "margin-bottom", r.marginBottom, v => e.style.marginBottom = v);
        Section("内间距");
        NumberControl("左内距", "padding-left", r.paddingLeft, v => e.style.paddingLeft = v, false);
        NumberControl("上内距", "padding-top", r.paddingTop, v => e.style.paddingTop = v, false);
        NumberControl("右内距", "padding-right", r.paddingRight, v => e.style.paddingRight = v, false);
        NumberControl("下内距", "padding-bottom", r.paddingBottom, v => e.style.paddingBottom = v, false);
        Section("文字");
        var textElement = e as TextElement;
        if (textElement != null && selector.StartsWith("#"))
        {
            EditorGUI.BeginChangeCheck();
            string text = EditorGUILayout.TextField("显示文字", textElement.text);
            if (EditorGUI.EndChangeCheck()) SetElementText(textElement, text);
        }
        NumberControl("字号", "font-size", r.fontSize, v => e.style.fontSize = v, false);
        ColorControl("文字颜色", "color", r.color, v => e.style.color = v);
        Section("外观");
        ColorControl("背景颜色", "background-color", r.backgroundColor, v => e.style.backgroundColor = v);
        EditorGUI.BeginChangeCheck();
        float opacity = EditorGUILayout.Slider("不透明度", r.opacity, 0, 1);
        if (EditorGUI.EndChangeCheck()) ApplyParameter("opacity", F(opacity), () => e.style.opacity = opacity);
        EnumControl("是否显示", "display", r.display, v => e.style.display = v,
            new[] { DisplayStyle.Flex, DisplayStyle.None }, new[] { "显示", "隐藏" });
        NumberControl("左上圆角", "border-top-left-radius", r.borderTopLeftRadius, v => e.style.borderTopLeftRadius = v, false);
        NumberControl("右上圆角", "border-top-right-radius", r.borderTopRightRadius, v => e.style.borderTopRightRadius = v, false);
        NumberControl("左下圆角", "border-bottom-left-radius", r.borderBottomLeftRadius, v => e.style.borderBottomLeftRadius = v, false);
        NumberControl("右下圆角", "border-bottom-right-radius", r.borderBottomRightRadius, v => e.style.borderBottomRightRadius = v, false);
        EditorGUILayout.EndScrollView();
    }

    private static void Section(string title)
    {
        EditorGUILayout.Space(7);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
    }

    private static string F(float number) { return number.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture); }

    private string OverrideValue(string key)
    {
        string begin = "/* UIWorkbench: " + selector + " */";
        string end = "/* /UIWorkbench: " + selector + " */";
        int a = stylesheet.draft.IndexOf(begin, StringComparison.Ordinal);
        int b = a < 0 ? -1 : stylesheet.draft.IndexOf(end, a, StringComparison.Ordinal);
        if (b < 0) return "";
        var match = Regex.Match(stylesheet.draft.Substring(a, b - a), @"(?<![\w-])" + Regex.Escape(key) + @"\s*:\s*([^;{}]+);");
        return match.Success ? match.Groups[1].Value.Trim() : "";
    }

    private void NumberControl(string label, string key, float computed, Action<StyleLength> apply, bool allowAuto = true)
    {
        string source = OverrideValue(key);
        int unit = source == "auto" || (source == "" && float.IsNaN(computed)) ? 2 : source.EndsWith("%") ? 1 : 0;
        if (!allowAuto && unit == 2) unit = 0;
        float number = float.IsNaN(computed) || float.IsInfinity(computed) ? 0 : computed;
        float parsed;
        if (float.TryParse(source.Replace("px", "").Replace("%", ""), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out parsed)) number = parsed;
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        using (new EditorGUI.DisabledScope(unit == 2))
            number = EditorGUILayout.FloatField(label, number);
        unit = EditorGUILayout.Popup(unit, allowAuto ? new[] { "px", "%", "自动" } : new[] { "px", "%" }, GUILayout.Width(52));
        if (EditorGUI.EndChangeCheck())
        {
            string css = unit == 2 ? "auto" : F(number) + (unit == 1 ? "%" : "px");
            StyleLength length = unit == 2 ? new StyleLength(StyleKeyword.Auto) : new StyleLength(new Length(number, unit == 1 ? LengthUnit.Percent : LengthUnit.Pixel));
            ApplyParameter(key, css, () => apply(length));
        }
        if (GUILayout.Button("↶", GUILayout.Width(24)))
            RemoveParameter(key, () => apply(new StyleLength(StyleKeyword.Null)));
        EditorGUILayout.EndHorizontal();
    }

    private void ScalarControl(string label, string key, float computed, Action<float> apply)
    {
        EditorGUI.BeginChangeCheck();
        float number = EditorGUILayout.FloatField(label, computed);
        if (EditorGUI.EndChangeCheck()) ApplyParameter(key, F(Mathf.Max(0, number)), () => apply(Mathf.Max(0, number)));
    }

    private void ColorControl(string label, string key, Color computed, Action<Color> apply)
    {
        EditorGUI.BeginChangeCheck();
        var color = EditorGUILayout.ColorField(new GUIContent(label), computed, true, true, false);
        if (EditorGUI.EndChangeCheck()) ApplyParameter(key, "#" + ColorUtility.ToHtmlStringRGBA(color), () => apply(color));
    }

    private void EnumControl<T>(string label, string key, T current, Action<T> apply, T[] options, string[] labels) where T : struct
    {
        int index = Array.IndexOf(options, current);
        EditorGUI.BeginChangeCheck();
        int choice = EditorGUILayout.Popup(label, Mathf.Max(0, index), labels);
        if (EditorGUI.EndChangeCheck())
        {
            T chosen = options[choice];
            string css = Regex.Replace(chosen.ToString(), "([a-z])([A-Z])", "$1-$2").ToLowerInvariant();
            ApplyParameter(key, css, () => apply(chosen));
        }
    }

    private void ApplyParameter(string key, string css, Action previewChange)
    {
        try
        {
            Undo.RecordObject(this, "Adjust " + key);
            stylesheet.draft = UpdateOverride(stylesheet.draft, selector, key, css, false);
            RemoveInlineConflict(key);
            previewChange();
            hasUnsavedChanges = layout.Dirty || stylesheet.Dirty;
            SchedulePreview();
            preview.schedule.Execute(UpdateOutline);
        }
        catch (Exception e) { message = e.Message; }
    }

    private void RemoveInlineConflict(string key)
    {
        if (!selector.StartsWith("#") || string.IsNullOrEmpty(layout.draft)) return;
        var xml = System.Xml.Linq.XDocument.Parse(layout.draft, System.Xml.Linq.LoadOptions.PreserveWhitespace);
        System.Xml.Linq.XElement target = null;
        int matches = 0;
        foreach (var node in xml.Descendants())
        {
            var name = node.Attribute("name");
            if (name != null && name.Value == selector.Substring(1)) { target = node; matches++; }
        }
        if (matches != 1 || target.Attribute("style") == null) return;
        string inline = target.Attribute("style").Value;
        string updated = Regex.Replace(inline, @"(?<![\w-])" + Regex.Escape(key) + @"\s*:[^;]+;?", "");
        if (inline == updated) return;
        target.SetAttributeValue("style", string.IsNullOrWhiteSpace(updated) ? null : updated);
        layout.draft = xml.ToString(System.Xml.Linq.SaveOptions.DisableFormatting);
    }

    private void RestoreInlineOriginal(string key)
    {
        if (!selector.StartsWith("#") || string.IsNullOrEmpty(layout.baseline)) return;
        var baselineXml = System.Xml.Linq.XDocument.Parse(layout.baseline, System.Xml.Linq.LoadOptions.PreserveWhitespace);
        var draftXml = System.Xml.Linq.XDocument.Parse(layout.draft, System.Xml.Linq.LoadOptions.PreserveWhitespace);
        string original = "";
        foreach (var node in baselineXml.Descendants())
        {
            if ((string)node.Attribute("name") != selector.Substring(1)) continue;
            var match = Regex.Match((string)node.Attribute("style") ?? "", @"(?<![\w-])" + Regex.Escape(key) + @"\s*:[^;]+;?");
            if (match.Success) original = match.Value.Trim().TrimEnd(';') + ";";
        }
        if (original == "") return;
        foreach (var node in draftXml.Descendants())
        {
            if ((string)node.Attribute("name") != selector.Substring(1)) continue;
            string inline = (string)node.Attribute("style") ?? "";
            inline = Regex.Replace(inline, @"(?<![\w-])" + Regex.Escape(key) + @"\s*:[^;]+;?", "");
            node.SetAttributeValue("style", (string.IsNullOrWhiteSpace(inline) ? "" : inline.Trim().TrimEnd(';') + "; ") + original);
        }
        layout.draft = draftXml.ToString(System.Xml.Linq.SaveOptions.DisableFormatting);
    }

    private void RemoveParameter(string key, Action previewChange)
    {
        Undo.RecordObject(this, "Reset " + key);
        stylesheet.draft = UpdateOverride(stylesheet.draft, selector, key, "", true);
        RestoreInlineOriginal(key);
        previewChange();
        SchedulePreview();
        hasUnsavedChanges = layout.Dirty || stylesheet.Dirty;
    }

    private void SetElementText(TextElement element, string text)
    {
        try
        {
            var xml = System.Xml.Linq.XDocument.Parse(layout.draft, System.Xml.Linq.LoadOptions.PreserveWhitespace);
            System.Xml.Linq.XElement target = null;
            int matches = 0;
            foreach (var node in xml.Descendants())
            {
                var name = node.Attribute("name");
                if (name != null && name.Value == selector.Substring(1)) { target = node; matches++; }
            }
            if (matches != 1) { message = "文字来自模板或重复 name，请在对应 UXML 中修改。"; return; }
            Undo.RecordObject(this, "Change UI text");
            target.SetAttributeValue("text", text);
            layout.draft = xml.ToString(System.Xml.Linq.SaveOptions.DisableFormatting);
            element.text = text;
            hasUnsavedChanges = true;
            SchedulePreview();
        }
        catch (Exception ex) { message = ex.Message; }
    }

    private void CreateStylesheet()
    {
        string path = EditorUtility.SaveFilePanelInProject("创建 USS", Path.GetFileNameWithoutExtension(layout.path) + "_Styles", "uss", "选择样式文件位置");
        if (string.IsNullOrEmpty(path)) return;
        if (File.Exists(path)) { message = "请选择新的文件名。"; return; }
        File.WriteAllText(path, "/* UI 样式 */\n");
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        stylesheet.path = path; stylesheet.baseline = stylesheet.draft = File.ReadAllText(path);
        if (!string.IsNullOrEmpty(layout.path))
        {
            var xml = System.Xml.Linq.XDocument.Parse(layout.draft, System.Xml.Linq.LoadOptions.PreserveWhitespace);
            xml.Root.AddFirst(new System.Xml.Linq.XElement(xml.Root.Name.Namespace + "Style", new System.Xml.Linq.XAttribute("src", "project://database/" + path)));
            layout.draft = xml.ToString(System.Xml.Linq.SaveOptions.DisableFormatting);
            hasUnsavedChanges = true;
        }
        SchedulePreview();
    }

    private void DrawAsset(string label, Document doc, Type type)
    {
        var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(doc.path);
        var chosen = EditorGUILayout.ObjectField(label, asset, type, false);
        if (chosen != asset) SelectAsset(chosen, type == typeof(VisualTreeAsset));
    }

    private bool ResolveDraft(Document doc)
    {
        if (!doc.Dirty) return true;
        int answer = EditorUtility.DisplayDialogComplex("切换文件", "当前文件有未保存草稿：" + doc.path, "保存并切换", "取消", "放弃草稿");
        if (answer == 1) return false;
        if (answer == 0) return SaveDocuments();
        return true;
    }

    private void SelectAsset(UnityEngine.Object asset, bool? isLayout = null)
    {
        bool uxml = isLayout ?? asset is VisualTreeAsset;
        if (asset != null && !(asset is VisualTreeAsset) && !(asset is StyleSheet)) { message = "请在 Project 中选择 UXML 或 USS。"; return; }
        var doc = uxml ? layout : stylesheet;
        string path = asset == null ? "" : AssetDatabase.GetAssetPath(asset);
        if (path == doc.path) return;
        if (!string.IsNullOrEmpty(path) && !path.StartsWith("Assets/", StringComparison.Ordinal))
        { message = "请先将 Package 中的文件复制到 Assets，再编辑。"; return; }
        if (!ResolveDraft(doc)) return;
        CleanupScratch();
        doc.path = path;
        doc.baseline = doc.draft = string.IsNullOrEmpty(path) ? "" : File.ReadAllText(path);
        doc.conflict = false;
        editLayout = uxml;
        message = "";
        // Keep unsaved stylesheet drafts when switching documents.
        if (uxml && !stylesheet.Dirty)
        {
            stylesheet.path = stylesheet.baseline = stylesheet.draft = "";
            stylesheet.conflict = false;
        }
        if (uxml && string.IsNullOrEmpty(stylesheet.path) && asset != null)
        {
            var tree = ((VisualTreeAsset)asset).CloneTree();
            foreach (var element in Walk(tree))
                for (int i = 0; i < element.styleSheets.count; i++)
                {
                    var sheet = element.styleSheets[i];
                    string sheetPath = AssetDatabase.GetAssetPath(sheet);
                    if (sheetPath.StartsWith("Assets/") && sheetPath.EndsWith(".uss"))
                    { stylesheet.path = sheetPath; stylesheet.baseline = stylesheet.draft = File.ReadAllText(sheetPath); goto Found; }
                }
        }
        Found:
        SchedulePreview();
    }

    private void SchedulePreview()
    {
        previewPending = true;
        previewDue = EditorApplication.timeSinceStartup + .6;
    }

    private string Scratch(Document doc, ref string path)
    {
        if (string.IsNullOrEmpty(doc.path)) return "";
        if (!File.Exists(doc.path)) throw new IOException("源文件已删除：" + doc.path);
        if (!doc.Dirty) return doc.path;
        if (string.IsNullOrEmpty(path))
            path = Path.GetDirectoryName(doc.path).Replace('\\', '/') + "/UIWorkbenchPreview_" + Guid.NewGuid().ToString("N") + Path.GetExtension(doc.path);
        File.WriteAllText(path, doc.draft);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        return path;
    }

    private void RebuildPreview()
    {
        if (preview == null) return;
        try
        {
            string uxml = Scratch(layout, ref scratchLayout);
            string uss = Scratch(stylesheet, ref scratchStyle);
            preview.Clear(); hierarchy.Clear(); selectedElement = null;
            lastPickPosition = new Vector2(float.NaN, float.NaN);
            preview.style.width = previewWidth; preview.style.height = previewHeight;
            // The preview host survives rebuilds; explicitly remove previous styles.
            preview.styleSheets.Clear();
            if (!string.IsNullOrEmpty(uxml))
            {
                var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxml);
                if (tree == null) throw new IOException("UXML 无法导入，请检查 Console。");
                tree.CloneTree(preview);
            }
            else
            {
                preview.Add(new Label("USS 可单独编辑。选择一个 UXML 来预览它的效果。"));
                preview.Add(new Button { text = "示例 Button" });
                var sample = new VisualElement { name = "sample" };
                sample.AddToClassList("sample");
                preview.Add(sample);
            }
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(uss);
            var original = AssetDatabase.LoadAssetAtPath<StyleSheet>(stylesheet.path);
            bool replaced = false;
            foreach (var element in Walk(preview))
            {
                if (sheet != null && original != null && element.styleSheets.Contains(original))
                {
                    var sheets = new List<StyleSheet>();
                    for (int i = 0; i < element.styleSheets.count; i++) sheets.Add(element.styleSheets[i] == original ? sheet : element.styleSheets[i]);
                    element.styleSheets.Clear();
                    foreach (var s in sheets) element.styleSheets.Add(s);
                    replaced = true;
                }
                string target = ElementSelector(element);
                if (target == "") continue;
                var item = element;
                string selected = target;
                if (selected == selector && (selectedElement == null || ElementPath(element) == selectedElementPath)) selectedElement = element;
                hierarchy.Add(new Button(() => SelectElement(item, selected)) { text = target + "  (" + element.GetType().Name + ")  [" + ElementPath(element) + "]" });
            }
            if (sheet != null && !replaced) preview.styleSheets.Add(sheet);
            FitPreview();
            preview.schedule.Execute(UpdateOutline);
            Repaint();
        }
        catch (Exception e) { message = "预览失败：" + e.Message; Repaint(); }
    }

    private static IEnumerable<VisualElement> Walk(VisualElement root)
    {
        yield return root;
        foreach (var child in root.Children())
            foreach (var node in Walk(child)) yield return node;
    }

    public static string UpdateOverride(string source, string target, string key, string val, bool remove)
    {
        if (!Regex.IsMatch(target, @"^[#.][A-Za-z_][A-Za-z0-9_-]*$"))
            throw new ArgumentException("可视化调整请选择简单的 #name 或 .class；复杂选择器请使用 USS 源码。");
        if (!Regex.IsMatch(key, @"^-?[a-z][a-z0-9-]*$") || val.IndexOfAny(new[] { '{', '}', ';', '\r', '\n' }) >= 0)
            throw new ArgumentException("属性或值格式无效。");
        string begin = "/* UIWorkbench: " + target + " */";
        string end = "/* /UIWorkbench: " + target + " */";
        int start = source.IndexOf(begin, StringComparison.Ordinal);
        int finish = start < 0 ? -1 : source.IndexOf(end, start, StringComparison.Ordinal);
        string block = start >= 0 && finish >= 0 ? source.Substring(start, finish + end.Length - start) : "";
        var entries = new Dictionary<string, string>();
        foreach (Match match in Regex.Matches(block, @"([\w-]+)\s*:\s*([^;{}]+);"))
            entries[match.Groups[1].Value] = match.Groups[2].Value.Trim();
        if (remove) entries.Remove(key); else entries[key] = val.Trim();
        string replacement = "";
        if (entries.Count > 0)
        {
            replacement = begin + "\n" + target + " {\n";
            foreach (var entry in entries) replacement += "    " + entry.Key + ": " + entry.Value + ";\n";
            replacement += "}\n" + end;
        }
        if (block != "") return source.Substring(0, start) + replacement + source.Substring(finish + end.Length);
        return replacement == "" ? source : source + "\n\n" + replacement + "\n";
    }

    private bool SaveDocuments()
    {
        try
        {
            layout.CheckDisk(); stylesheet.CheckDisk();
            if ((layout.Dirty && !layout.CanSave) || (stylesheet.Dirty && !stylesheet.CanSave))
                throw new IOException("源文件已变化，保存被阻止。先导出草稿或重新加载。");
            string a = layout.Save(), b = stylesheet.Save();
            hasUnsavedChanges = layout.Dirty || stylesheet.Dirty;
            message = string.IsNullOrEmpty(a + b) ? "没有需要保存的修改。" : "已保存。原文件备份位于 Library/UIWorkbench/Backups。";
            SchedulePreview();
            return true;
        }
        catch (Exception e) { message = e.Message; Repaint(); return false; }
    }

    public override void SaveChanges()
    {
        if (!SaveDocuments()) throw new IOException(message);
        base.SaveChanges();
    }

    public override void DiscardChanges()
    {
        layout.draft = layout.baseline;
        stylesheet.draft = stylesheet.baseline;
        base.DiscardChanges();
    }

    private void ReloadFromDisk()
    {
        if ((layout.Dirty || stylesheet.Dirty) && !EditorUtility.DisplayDialog("重新加载", "将放弃当前草稿，读取最新源文件。", "重新加载", "取消")) return;
        try
        {
            foreach (var doc in new[] { layout, stylesheet })
            {
                if (string.IsNullOrEmpty(doc.path)) continue;
                if (!File.Exists(doc.path)) { doc.conflict = true; message = "文件已删除：" + doc.path; continue; }
                doc.baseline = doc.draft = File.ReadAllText(doc.path); doc.conflict = false;
            }
            hasUnsavedChanges = layout.Dirty || stylesheet.Dirty;
            SchedulePreview();
        }
        catch (Exception e) { message = e.Message; }
    }

    private void ExportDraft()
    {
        var doc = editLayout ? layout : stylesheet;
        if (string.IsNullOrEmpty(doc.path)) return;
        string path = EditorUtility.SaveFilePanel("导出草稿", "", Path.GetFileNameWithoutExtension(doc.path) + "_draft", Path.GetExtension(doc.path).TrimStart('.'));
        if (string.IsNullOrEmpty(path)) return;
        if ((!string.IsNullOrEmpty(layout.path) && Path.GetFullPath(path).Equals(Path.GetFullPath(layout.path), StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrEmpty(stylesheet.path) && Path.GetFullPath(path).Equals(Path.GetFullPath(stylesheet.path), StringComparison.OrdinalIgnoreCase)))
        { message = "导出请选择新文件；源文件只能通过保存按钮写入。"; return; }
        File.WriteAllText(path, doc.draft);
        message = "草稿已导出：" + path;
    }

    private void CleanupScratch()
    {
        foreach (string path in new[] { scratchLayout, scratchStyle })
            if (!string.IsNullOrEmpty(path)) AssetDatabase.DeleteAsset(path);
        scratchLayout = scratchStyle = "";
    }

    private void OnLostFocus() { EndCanvasDrag(); }

    private void OnDisable()
    {
        EndCanvasDrag();
        EditorApplication.update -= Tick;
        Undo.undoRedoPerformed -= SchedulePreview;
        CleanupScratch();
    }
}

[InitializeOnLoad]
internal static class HideUnusedJobsMenu
{
    static HideUnusedJobsMenu()
    {
        EditorApplication.delayCall += Hide;
    }
    private static void Hide()
    {
        // Burst remains installed because URP and 2D packages depend on it.
        var menu = typeof(EditorApplication).Assembly.GetType("UnityEditor.Menu");
        var remove = menu == null ? null : menu.GetMethod("RemoveMenuItem",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        if (remove != null) remove.Invoke(null, new object[] { "Jobs" });
    }
}
