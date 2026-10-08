using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Isolated visual demo. Never reads or writes gameplay, chips or BuffManager.</summary>
[ExecuteAlways, RequireComponent(typeof(UIDocument))]
public sealed class SlotVisualLabController : MonoBehaviour
{
    public Texture2D backplate;
    public Texture2D[] glyphs;
    public Texture2D edgeShade;
    public Texture2D selectionGlow;
    public Font displayFont;
    private VisualElement root, stage;
    private readonly Image[,] slots = new Image[3, 7];
    private readonly VisualElement[] windows = new VisualElement[3];
    private readonly VisualElement[] glow = new VisualElement[3];
    private readonly float[] offsets = new float[3];
    private readonly int[] results = { 0, 6, 2 };
    private Label status;
    private Button roll;
    private bool spinning, special, overlays = true;
    private float started;
    private int sequence;
    private const float Pitch = 108f;
    private static readonly string[] Names = { "SPEED", "SHIELD", "HEAL", "FROST", "MULTI", "UTILITY", "SPECIAL", "OVERLOAD" };

    private void OnEnable()
    {
        var doc = GetComponent<UIDocument>();
        if (doc.rootVisualElement != null) doc.rootVisualElement.schedule.Execute(Rebuild);
    }

    private void OnDisable()
    {
        if (root != null) root.UnregisterCallback<GeometryChangedEvent>(Fit);
        spinning = false;
    }

    public void Rebuild()
    {
        if (root != null) root.UnregisterCallback<GeometryChangedEvent>(Fit);
        root = GetComponent<UIDocument>().rootVisualElement;
        stage = root.Q<VisualElement>("stage");
        if (stage == null || backplate == null || glyphs == null || glyphs.Length < 8) return;
        spinning = false;
        stage.Clear();
        if (displayFont != null) stage.style.unityFont = displayFont;
        AddImage(stage, "Terminal / environment plate", backplate, 0, 0, 1536, 1024);
        Text("P R O T O C O L   A R R A Y", 530, 109, 476, 38, 27);
        status = Text("R O L L   F O R   E N H A N C E M E N T", 518, 150, 500, 27, 14);
        Text("SYSTEM\nONLINE", 327, 122, 120, 46, 13);
        Text("ARENA\nPROTOCOL\nTERMINAL", 1095, 111, 118, 63, 12);
        Text("INSERT\nENERGY", 239, 517, 100, 43, 13);
        Text("03", 235, 561, 104, 54, 34);
        Text("POSSIBLE\nPROTOCOLS", 1174, 250, 144, 47, 12);
        for (int i = 0; i < 7; i++)
        {
            AddImage(stage, "Legend " + Names[i], glyphs[i], 1193, 300 + i * 46, 36, 36);
            Text(Names[i], 1237, 302 + i * 46, 100, 34, 12);
        }
        for (int r = 0; r < 3; r++)
        {
            float x = 480 + r * 228;
            windows[r] = Box(stage, "Reel " + (r + 1) + " / clipped stream", x, 244, 152, 386);
            windows[r].AddToClassList("reel-window");
            for (int j = 0; j < 7; j++) slots[r, j] = AddImage(windows[r], "Protocol cell " + j, glyphs[(j + r * 2) % 8], 0, 0, 152, 152);
            AddImage(windows[r], "Glass edge shading", edgeShade, 0, 0, 152, 386);
            glow[r] = AddImage(windows[r], "Selection glow", selectionGlow, 0, 139, 152, 108);
            Box(windows[r], "Upper lock line", 0, 139, 152, 1).style.backgroundColor = new Color(.56f, .94f, 1f, .65f);
            Box(windows[r], "Lower lock line", 0, 247, 152, 1).style.backgroundColor = new Color(.56f, .94f, 1f, .65f);
            offsets[r] = 0;
            RenderReel(r);
        }
        Text("COST", 370, 746, 83, 40, 15);
        Text("◉  1", 458, 746, 72, 40, 20).style.color = new Color(1f, .63f, .25f);
        Text("CURRENT", 1000, 746, 105, 40, 14);
        Text("◉  12", 1107, 746, 88, 40, 20).style.color = new Color(1f, .63f, .25f);
        roll = Button("R O L L", 597, 734, 343, 66, () => PreviewRoll(false));
        Button("NORMAL", 480, 919, 128, 38, () => PreviewRoll(false), true);
        Button("SPECIAL", 628, 919, 128, 38, () => PreviewRoll(true), true);
        Button("GLOW / GLASS", 776, 919, 166, 38, ToggleOverlays, true);
        Button("RESET", 962, 919, 95, 38, ResetPreview, true);
        Text("VISUAL LAB  /  PREVIEW ONLY  /  NO BUFFS OR CURRENCY APPLIED", 397, 970, 742, 22, 11);
        root.RegisterCallback<GeometryChangedEvent>(Fit);
        FitStage();
    }

    private void Fit(GeometryChangedEvent e) => FitStage();
    private void FitStage()
    {
        if (stage == null || root == null) return;
        float w = root.resolvedStyle.width, h = root.resolvedStyle.height;
        if (float.IsNaN(w) || float.IsNaN(h) || w <= 0 || h <= 0) return;
        float s = Mathf.Min(w / 1536f, h / 1024f);
        stage.transform.scale = new Vector3(s, s, 1);
        stage.style.left = (w - 1536 * s) * .5f;
        stage.style.top = (h - 1024 * s) * .5f;
    }

    public void PreviewRoll(bool specialResult)
    {
        if (!Application.isPlaying || stage == null || spinning) return;
        special = specialResult;
        sequence++;
        for (int r = 0; r < 3; r++) results[r] = special ? 7 : (sequence * 3 + r * 2) % 7;
        started = Time.unscaledTime;
        spinning = true;
        roll.SetEnabled(false);
        status.text = "A C T I V A T I N G   /   C O M P I L I N G";
    }

    private void Update()
    {
        if (!Application.isPlaying || stage == null || !spinning) return;
        float elapsed = Time.unscaledTime - started;
        for (int r = 0; r < 3; r++)
        {
            float duration = 1.9f + r * .32f;
            float t = Mathf.Clamp01(elapsed / duration);
            float progress = t < .15f ? .16f * Mathf.Pow(t / .15f, 2) : .16f + .84f * (1 - Mathf.Pow(1 - (t - .15f) / .85f, 3));
            offsets[r] = (12 + r * 2) * Pitch * (1 - progress);
            RenderReel(r);
            glow[r].style.opacity = overlays ? (t >= 1 ? .85f + .15f * Mathf.Sin(elapsed * 12) : .45f) : 0;
        }
        if (elapsed >= 2.54f)
        {
            spinning = false;
            roll.SetEnabled(true);
            status.text = special ? "S P E C I A L   P R O T O C O L   L O C K E D" : "P R O T O C O L S   L O C K E D   /   P R E V I E W";
            status.style.color = special ? new Color(1f, .65f, .3f) : new Color(.74f, .98f, 1f);
        }
    }

    private void RenderReel(int r)
    {
        int cycle = Mathf.FloorToInt(offsets[r] / Pitch);
        float fraction = offsets[r] % Pitch;
        for (int j = 0; j < 7; j++)
        {
            float d = (j - 3) * Pitch + fraction;
            int id = ((results[r] + j - 3 - cycle) % 8 + 8) % 8;
            var cell = slots[r, j];
            cell.image = glyphs[id];
            cell.style.top = 193 + d - 76;
            float distance = Mathf.Clamp01(Mathf.Abs(d) / 220);
            cell.transform.scale = new Vector3(1 - distance * .12f, 1 - distance * .55f, 1);
            cell.style.opacity = Mathf.Lerp(1, .08f, distance);
        }
    }

    private void ToggleOverlays()
    {
        overlays = !overlays;
        foreach (var window in windows)
        {
            window.Q("Glass edge shading").style.display = overlays ? DisplayStyle.Flex : DisplayStyle.None;
            window.Q("Selection glow").style.display = overlays ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    public void ResetPreview()
    {
        spinning = false;
        results[0] = 0; results[1] = 6; results[2] = 2;
        for (int r = 0; r < 3; r++) { offsets[r] = 0; RenderReel(r); }
        roll.SetEnabled(true);
        status.text = "R O L L   F O R   E N H A N C E M E N T";
        status.style.color = new Color(.74f, .98f, 1);
    }

    private static VisualElement Box(VisualElement parent, string name, float x, float y, float w, float h)
    {
        var e = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
        Place(e, x, y, w, h); parent.Add(e); return e;
    }
    private static Image AddImage(VisualElement parent, string name, Texture2D texture, float x, float y, float w, float h)
    {
        var e = new Image { name = name, image = texture, scaleMode = ScaleMode.StretchToFill, pickingMode = PickingMode.Ignore };
        Place(e, x, y, w, h); parent.Add(e); return e;
    }
    private Label Text(string text, float x, float y, float w, float h, int size)
    {
        var e = new Label(text) { pickingMode = PickingMode.Ignore };
        Place(e, x, y, w, h); e.style.fontSize = size; stage.Add(e); return e;
    }
    private Button Button(string text, float x, float y, float w, float h, Action action, bool test = false)
    {
        var e = new Button(action) { text = text };
        e.AddToClassList("live-button"); if (test) e.AddToClassList("test-button");
        Place(e, x, y, w, h); stage.Add(e); return e;
    }
    private static void Place(VisualElement e, float x, float y, float w, float h)
    {
        e.style.position = Position.Absolute; e.style.left = x; e.style.top = y; e.style.width = w; e.style.height = h;
    }
}
