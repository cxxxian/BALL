using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>One continuous Electro-style ribbon; shares the charge Shader and HDR profile.</summary>
public class TeslaArcFX : MonoBehaviour
{
    public static TeslaArcFX Instance { get; private set; }
    private const int PoolSize = 12;
    private const float ArcDuration = .18f;
    private static Material _sharedLineMaterial;
    private readonly Queue<ArcInstance> _pool = new Queue<ArcInstance>();
    private Camera _camera;
    private sealed class ArcInstance
    {
        public GameObject Root;
        public LineRenderer Body;
        public readonly MaterialPropertyBlock Properties = new MaterialPropertyBlock();
    }
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _camera = Camera.main;
        for (int i=0;i<PoolSize;i++) _pool.Enqueue(CreateArcInstance());
    }
    public void SpawnArc(Vector2 from, Vector2 to, int jitterSeed)
    {
        if (_pool.Count == 0) return;
        var arc = _pool.Dequeue();
        arc.Root.SetActive(true);
        if (_camera == null) _camera = Camera.main;
        float perPixel = _camera != null && _camera.orthographic
            ? 2f*_camera.orthographicSize/Mathf.Max(1,Screen.height) : .025f;
        arc.Body.startWidth = arc.Body.endWidth = perPixel*63f;
        arc.Body.SetPosition(0,from); arc.Body.SetPosition(1,to);
        arc.Properties.SetFloat("_NoiseSeed",(jitterSeed & 1023)*.037f);
        arc.Properties.SetFloat("_Wobble",.6f);
        arc.Body.SetPropertyBlock(arc.Properties);
        StartCoroutine(FlashAndReturn(arc));
    }
    private IEnumerator FlashAndReturn(ArcInstance arc)
    {
        float elapsed=0;
        while (elapsed < ArcDuration)
        {
            float life=1f-elapsed/ArcDuration;
            var color = new Color(1,1,1,life*life);
            arc.Body.startColor=arc.Body.endColor=color;
            elapsed+=Time.deltaTime;
            yield return null;
        }
        arc.Root.SetActive(false); _pool.Enqueue(arc);
    }
    private ArcInstance CreateArcInstance()
    {
        var root=new GameObject("TeslaArc"); root.transform.SetParent(transform,false);
        var line=root.AddComponent<LineRenderer>();
        line.useWorldSpace=true; line.textureMode=LineTextureMode.Stretch;
        line.alignment=LineAlignment.View; line.positionCount=2;
        line.sortingOrder=22; line.shadowCastingMode=ShadowCastingMode.Off;
        line.receiveShadows=false; line.allowOcclusionWhenDynamic=false;
        if (_sharedLineMaterial == null)
        {
            var shader=Shader.Find("Custom/ChargeArcUnlit");
            if (shader == null) shader=Shader.Find("Sprites/Default");
            _sharedLineMaterial=new Material(shader) { name="ElectricArc_Shared" };
        }
        line.sharedMaterial=_sharedLineMaterial;
        line.startColor=line.endColor=Color.white;
        root.SetActive(false);
        return new ArcInstance { Root=root, Body=line };
    }
    public static void EnsureInstance()
    {
        if (Instance == null) new GameObject("TeslaArcFX").AddComponent<TeslaArcFX>();
    }
}
