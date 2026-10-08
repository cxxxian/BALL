using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class TAShowcaseController : MonoBehaviour
{
    public Camera presentationCamera;
    public Canvas presentationCanvas;
    public CanvasGroup interfaceGroup;
    public TAShowcaseTarget hero;
    public TAShowcaseTarget[] specimens;
    public Material neonMaterial;
    public Volume presentationVolume;
    public Text titleText, detailText, stateText, scaleText;
    public Button[] exhibitButtons, levelButtons;
    public Button cycleButton, freezeButton, triggerButton, bloomButton, slowButton, cleanButton;
    public Slider scaleSlider;
    public ImpactFX impact;
    public TeslaArcFX tesla;

    public int Exhibit { get; private set; }
    public int Level { get; private set; } = 3;
    public bool Cycling { get; private set; } = true;
    private float _cycleTime, _effectTime;
    public TAShowcaseRain rainStage;
    public TAShowcaseExtras extraStage;
    private bool _slow, _clean, _bloomEnabled = true;
    private float _previousTimeScale = 1f;
    private float _flashLeft;
    private int _screenWidth, _screenHeight;
    private bool _previousRunInBackground;
    private VolumeProfile _profile;
    private Bloom _bloom;
    private MaterialPropertyBlock _block;
    private static readonly Color Accent = new Color(0.25f, 0.88f, 1f);
    private static readonly string[] Titles = { "程序化电荷 / ELECTRIC", "玩法霜痕 / FROST MARKS", "代码雨与流体 / MATRIX FLOW", "运行时特效 / IMPACT", "HDR 材质 / NEON", "墙体传播脉冲 / WALL PULSE", "反应式能量盾 / REACTIVE SHIELD" };
    private static readonly string[] Details = {
        "短电弧 → 分支 → 内部放电\n\n共享材质 · 动态 ribbon 网格\n紫白亮芯 · 连续噪声辉光\n层数控制密度与路径\n\nTA-10  程序化电弧",
        "薄冰 → 冰壳 → 厚霜冰晶\n\n粗糙法线 · 原贴图折射\n厚度透射 · 乳白散射\n共享材质 + MPB 状态驱动\n\nTA-09  战斗冰霜材质",
        "移动鼠标，搅动代码雨\n\n字符图集 · 随机列流\n速度平流 · 压力投影\n涡量约束 · 位移输运\n\nC 自动轨迹 / R 清空\nTA-06 / TA-07",
        "命中 / 解体 / 冲击环 / 电弧\n\n复用 ImpactFX 与 TeslaArcFX\n像素粒子 + 几何扩散波\n按空格或按钮重复播放\n\nTA-04  Runtime VFX",
        "保留金属，点亮霓虹\n\nSpriteNeonHDR 亮色遮罩\nMPB 驱动受击闪光\nBloom 开关对比\n\nTA-01 / TA-02  HDR 管线",
        "点击墙框，双向传播\n\n撞击点投影至弧长\nUV2 连续距离坐标\nMPB 多脉冲槽位\n\nTA-08  台面传播脉冲",
        "点击护盾，观察波纹\n\n测地球面 · 六边形胞元\nFresnel · 几何凹陷\n表面传播 · 溶解揭露\n\nF 揭露 / R 恢复护盾"
    };

    private void Start()
    {
        _block = new MaterialPropertyBlock();
        _previousRunInBackground = Application.runInBackground;
        Application.runInBackground = true;
        _previousTimeScale = Time.timeScale;
        _profile = presentationVolume.profile; // Private runtime copy of the authored profile.
        _profile.TryGet(out _bloom);
        for (int i = 0; i < exhibitButtons.Length; i++) { int index = i; exhibitButtons[i].onClick.AddListener(() => SelectExhibit(index)); }
        for (int i = 0; i < levelButtons.Length; i++) { int index = i; levelButtons[i].onClick.AddListener(() => SetLevel(index)); }
        cycleButton.onClick.AddListener(ToggleCycle);
        freezeButton.onClick.AddListener(Freeze);
        triggerButton.onClick.AddListener(Trigger);
        presentationCamera.backgroundColor = Color.black;
        rainStage.Initialize(presentationCamera);
        bloomButton.onClick.AddListener(ToggleBloom);
        slowButton.onClick.AddListener(ToggleSlow);
        cleanButton.onClick.AddListener(ToggleClean);
        scaleSlider.onValueChanged.AddListener(SetScale);
        SelectExhibit(0);
        SetScale(scaleSlider.value);
        exhibitButtons[0].Select();
    }

    private void Update()
    {
        FitViewport();
        if (Input.GetKeyDown(KeyCode.H)) ToggleClean();
        if (Input.GetKeyDown(KeyCode.Space)) Trigger();
        if (Input.GetKeyDown(KeyCode.C)) ToggleCycle();
        if (Input.GetKeyDown(KeyCode.F)) Freeze();
        if (Input.GetKeyDown(KeyCode.R)) { if(Exhibit == 2) { rainStage.ResetField(); UpdateState(); } else if(Exhibit == 6) extraStage.ResetStudy(); }
        if (Input.GetKeyDown(KeyCode.P)) ToggleBloom();
        if (Input.GetKeyDown(KeyCode.S)) ToggleSlow();
        for (int i = 0; i < exhibitButtons.Length; i++) if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))) SelectExhibit(i);
        if (Cycling && Exhibit != 2)
        {
            _cycleTime += Time.deltaTime;
            if (_cycleTime >= 2f) { _cycleTime = 0f; ApplyLevel((Level + 1) % 4); }
        }
        if (Exhibit >= 3 && Cycling)
        {
            _effectTime += Time.deltaTime;
            if (_effectTime >= 1.7f) { _effectTime = 0; Trigger(); }
        }
        if (Exhibit == 4)
        {
            _flashLeft = Mathf.Max(0f, _flashLeft - Time.deltaTime);
            var sr = hero.MainSR;
            sr.GetPropertyBlock(_block);
            _block.SetFloat("_HitFlash", _flashLeft / 0.32f);
            sr.SetPropertyBlock(_block);
        }
    }

    public void SelectExhibit(int exhibit)
    {
        Exhibit = Mathf.Clamp(exhibit, 0, exhibitButtons.Length - 1);
        titleText.text = Titles[Exhibit]; detailText.text = Details[Exhibit];
        _effectTime = 0f;
        Configure(hero, Level);
        for (int i = 0; i < specimens.Length; i++) Configure(specimens[i], i);
        for (int i = 0; i < exhibitButtons.Length; i++) Paint(exhibitButtons[i], i == Exhibit);
        bool environment = Exhibit == 2 || Exhibit >= 5;
        rainStage.gameObject.SetActive(Exhibit == 2);
        if (Exhibit == 2) rainStage.Select(true, Level);
        extraStage.gameObject.SetActive(Exhibit >= 5);
        if (Exhibit >= 5) extraStage.Select(Exhibit == 6);
        hero.gameObject.SetActive(!environment);
        foreach (var specimen in specimens) specimen.gameObject.SetActive(!environment);
        scaleSlider.gameObject.SetActive(!environment); scaleText.gameObject.SetActive(!environment);
        foreach (var text in presentationCanvas.GetComponentsInChildren<Text>(true))
            if (text.name == "Closeup caption" || text.name == "Comparison caption" || text.name.StartsWith("Specimen label")) text.gameObject.SetActive(!environment);
        freezeButton.gameObject.SetActive(Exhibit == 1 || Exhibit == 2 || Exhibit == 6);
        Label(freezeButton, Exhibit == 2 ? "清空重置 [R]" : Exhibit == 6 ? "溶解揭露 [F]" : "短冻 [F]");
        Label(triggerButton, Exhibit == 2 ? "自动轨迹 [SPACE]" : Exhibit >= 5 ? "触发反馈 [SPACE]" : "播放 / 消耗 [SPACE]");
        foreach(var button in levelButtons)button.gameObject.SetActive(Exhibit < 5);
        foreach(var button in levelButtons)PositionControls(button,environment);
        PositionControls(cycleButton,environment);PositionControls(freezeButton,environment);PositionControls(triggerButton,environment);
        UpdateState();
    }

    private void Configure(TAShowcaseTarget target, int level)
    {
        var frost = target.GetComponent<EnemyFrostState>();
        var electric = target.GetComponent<EnemyElectricState>();
        frost.DevFillMarks(Exhibit == 1 ? level : 0);
        electric.DevFillCharge(Exhibit == 0 ? level : 0);
        var visual = target.GetComponent<EnemyBuildStackVisual>();
        visual.enabled = Exhibit != 4;
        target.MainSR.sharedMaterial = Exhibit == 4 ? neonMaterial : EnemyBuildStackVisual.SharedMaterial;
        // Retain the production texture binding across material switches.
        target.MainSR.GetPropertyBlock(_block);
        _block.SetTexture("_MainTex", target.MainSR.sprite.texture);
        _block.SetVector("_MainTex_ST", new Vector4(1f, 1f, 0f, 0f));
        _block.SetColor("_Color", Color.white);
        _block.SetFloat("_HitFlash", 0f);
        target.MainSR.SetPropertyBlock(_block);
        if (Exhibit != 4) visual.ForceRefresh();
    }

    public void SetLevel(int level) { Cycling = false; ApplyLevel(level); }
    private void ApplyLevel(int level)
    {
        Level = Mathf.Clamp(level, 0, 3);
        Configure(hero, Level);
        if (Exhibit == 2) rainStage.SetLevel(Level);
        UpdateState();
    }
    private void UpdateState()
    {
        stateText.text = Exhibit == 2 ? "移动鼠标搅动字雨  ·  C 自动轨迹  ·  R 清空" : Exhibit >= 5 ? "点击效果区域或 SPACE 触发  ·  C 自动播放" : "STATE  " + Level + " / 3     ·     " + (Cycling ? "自动循环" : "手动观察");
        for (int i = 0; i < levelButtons.Length; i++) Paint(levelButtons[i], i == Level);
        Label(cycleButton, Exhibit == 2 ? "自动轨迹 " + (rainStage.DemoPlaying ? "ON" : "OFF") + " [C]" : Cycling ? "循环 ON  [C]" : "循环 OFF  [C]");
    }
    public void ToggleCycle() { if(Exhibit == 2) rainStage.ToggleDemo(); else Cycling = !Cycling; _cycleTime = 0; UpdateState(); }
    public void SetScale(float multiplier)
    {
        float width = hero.MainSR.sprite.bounds.size.x;
        hero.transform.localScale = Vector3.one * (0.9f * multiplier / Mathf.Max(width, 0.01f));
        scaleText.text = "特写倍率   ×" + multiplier.ToString("0.0");
    }
    public void Freeze()
    {
        if(Exhibit == 2) { rainStage.ResetField(); UpdateState(); return; }
        if(Exhibit == 6) { extraStage.Reveal(); return; }
        if (Exhibit != 1) SelectExhibit(1);
        hero.GetComponent<EnemyFrostState>().ApplyShortFreeze(2f);
    }
    public void Trigger()
    {
        Vector2 pos = hero.transform.position;
        if (Exhibit == 2) { ToggleCycle(); return; }
        if (Exhibit >= 5) { extraStage.Trigger(); return; }
        if (Exhibit <= 1)
        {
            Configure(hero, 0);
            Level = 0; Cycling = false; UpdateState();
            if (Exhibit == 0) tesla.SpawnArc(pos, pos + new Vector2(2.8f, 1f), Random.Range(1, 9999));
            impact.SpawnHit(pos, Accent, 1.6f);
        }
        else if (Exhibit == 3)
        {
            impact.SpawnHit(pos, Accent, 1.8f);
            impact.SpawnBossDissolve(pos + Vector2.down * 0.6f, Accent, 1f);
            impact.SpawnCorePulseWave(pos, 2.8f, Accent, 0.85f);
            tesla.SpawnArc(pos - Vector2.right * 2.5f, pos + Vector2.right * 2.5f, Random.Range(1, 9999));
        }
        else
        {
            _flashLeft = 0.32f;
            hero.MainSR.GetPropertyBlock(_block); _block.SetFloat("_HitFlash", 1f); hero.MainSR.SetPropertyBlock(_block);
            impact.SpawnHit(pos, Accent, 1f);
        }
    }
    public void ToggleBloom() { _bloomEnabled = !_bloomEnabled; if (_bloom != null) _bloom.active = _bloomEnabled; Label(bloomButton, "Bloom " + (_bloomEnabled ? "ON" : "OFF") + " [P]"); }
    public void ToggleSlow() { _slow = !_slow; Time.timeScale = _slow ? 0.25f : 1f; Label(slowButton, _slow ? "速度 0.25× [S]" : "速度 1× [S]"); }
    public void ToggleClean() { _clean = !_clean; interfaceGroup.alpha = _clean ? 0f : 1f; interfaceGroup.interactable = interfaceGroup.blocksRaycasts = !_clean; }
    private void FitViewport()
    {
        if (_screenWidth == Screen.width && _screenHeight == Screen.height) return;
        _screenWidth = Screen.width; _screenHeight = Screen.height;
        float aspect = Screen.width / (float)Mathf.Max(1, Screen.height);
        const float target = 16f / 9f;
        if (aspect < target) { float height = aspect / target; presentationCamera.rect = new Rect(0f, (1f-height)*0.5f, 1f, height); }
        else { float width = target / aspect; presentationCamera.rect = new Rect((1f-width)*0.5f, 0f, width, 1f); }
    }
    private static void Label(Button button, string value) => button.GetComponentInChildren<Text>().text = value;
    private static void PositionControls(Button button,bool belowStage)
    {
        var rect=button.GetComponent<RectTransform>();
        rect.anchorMin=new Vector2(rect.anchorMin.x,belowStage ? .155f : .318f);
        rect.anchorMax=new Vector2(rect.anchorMax.x,belowStage ? .197f : .36f);
    }
    private static void Paint(Button button, bool selected) => button.image.color = selected ? new Color(0.1f, 0.29f, 0.36f) : new Color(0.075f, 0.1f, 0.135f);
    private void OnDestroy()
    {
        Time.timeScale = _previousTimeScale;
        Application.runInBackground = _previousRunInBackground;
        if (_profile != null) Destroy(_profile);
    }
}


