using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Procedural solid chassis, curved drums and depth rails driven by one assembly timeline.</summary>
public sealed class SlotMechanicalAssembly3D : MonoBehaviour
{
    private sealed class Part
    {
        public Transform transform;
        public Vector3 destination, foldedRotation;
        public Material face, side;
        public Vector3[] edge;
        public float start;
        public float depth;
    }
    private readonly List<Part> parts=new List<Part>();
    private readonly List<Object> owned=new List<Object>();
    private Transform rig,leftGrip,rightGrip;
    private Camera camera;
    private bool savedOrtho;
    private float savedFov,worldWidth,worldHeight;
    private Vector3 center;
    private Material wire;
    private SlotAssemblyDepthCircuit circuit;

    public bool Prepare(VisualElement stage,Texture2D texture)
    {
        Clear();
        var bloom=GetComponent<SlotVisualLabV2Bloom>();
        if(bloom==null || bloom.outputCamera==null || texture==null) return false;
        camera=bloom.outputCamera;savedOrtho=camera.orthographic;savedFov=camera.fieldOfView;
        float rootW=GetComponent<UIDocument>().rootVisualElement.resolvedStyle.width;
        float rootH=GetComponent<UIDocument>().rootVisualElement.resolvedStyle.height;
        if(rootW<=0 || rootH<=0) {camera=null;return false;}
        Rect bounds=stage.worldBound;
        float viewHeight=camera.orthographicSize*2;
        worldWidth=viewHeight*camera.aspect*bounds.width/rootW;
        worldHeight=viewHeight*bounds.height/rootH;
        center=new Vector3((bounds.center.x/rootW-.5f)*viewHeight*camera.aspect,
            (.5f-bounds.center.y/rootH)*viewHeight,10);
        camera.fieldOfView=2*Mathf.Atan(viewHeight/20)*Mathf.Rad2Deg;
        camera.orthographic=savedOrtho;
        rig=NewObject("Slot / mechanical synthesis",null).transform;
        wire=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        wire.SetColor("_BaseColor",new Color(.025f,1.5f,2.2f,1));
        wire.SetFloat("_Surface",1);wire.SetFloat("_SrcBlend",5);wire.SetFloat("_DstBlend",10);
        wire.SetFloat("_ZWrite",0);wire.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");wire.renderQueue=2995;
        owned.Add(wire);
        var pixelMaterial=new Material(Shader.Find("ReboundProtocol/SlotVisualLabV2/AssemblyPixel"));
        pixelMaterial.SetFloat("_Gain",3);owned.Add(pixelMaterial);
        circuit=new SlotAssemblyDepthCircuit(rig,pixelMaterial,worldWidth,worldHeight);
        AddPart("Header",new Rect(.07f,.03f,.86f,.19f),.38f,new Vector3(-105,0,-8),texture,false);
        AddPart("Base and status console",new Rect(.015f,.61f,.97f,.31f),.43f,new Vector3(100,0,5),texture,false);
        AddPart("Left spine",new Rect(.045f,.22f,.12f,.39f),.24f,new Vector3(0,105,-8),texture,false);
        AddPart("Right spine",new Rect(.835f,.22f,.12f,.39f),.24f,new Vector3(0,-105,8),texture,false);
        for(int i=0;i<3;i++)
            AddPart("Curved reel "+i,new Rect(.169f+i*.246f,.242f,.169f,.340f),
                .40f+i*.045f,new Vector3(100-i*12,(i-1)*28,0),texture,true);
        AddPart("Left reel bridge",new Rect(.338f,.22f,.077f,.39f),.44f,new Vector3(30,65,0),texture,false);
        AddPart("Right reel bridge",new Rect(.584f,.22f,.077f,.39f),.49f,new Vector3(30,-65,0),texture,false);
        leftGrip=Grip("Left core grip");rightGrip=Grip("Right core grip");
        SetProgress(0);
        return true;
    }

    private GameObject NewObject(string objectName,Transform parent)
    {
        var go=new GameObject(objectName){hideFlags=HideFlags.HideAndDontSave,layer=5};
        go.transform.SetParent(parent,false);return go;
    }

    private Transform Grip(string objectName)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name=objectName;go.hideFlags=HideFlags.HideAndDontSave;go.layer=5;
        go.transform.SetParent(rig,false);
        Destroy(go.GetComponent<Collider>());
        go.transform.localScale=new Vector3(worldWidth*.035f,worldHeight*.17f,worldHeight*.035f);
        var material=Surface(null,false);material.SetFloat("_Reveal",1);
        go.GetComponent<MeshRenderer>().sharedMaterial=material;
        var stripe=GameObject.CreatePrimitive(PrimitiveType.Cube);
        stripe.hideFlags=HideFlags.HideAndDontSave;stripe.layer=5;
        stripe.transform.SetParent(go.transform,false);
        stripe.transform.localScale=new Vector3(.12f,.84f,.08f);
        stripe.transform.localPosition=new Vector3(0,0,-.51f);
        stripe.GetComponent<MeshRenderer>().sharedMaterial=wire;
        Destroy(stripe.GetComponent<Collider>());
        return go.transform;
    }

    private Material Surface(Texture2D texture,bool textured)
    {
        var material=new Material(Shader.Find("ReboundProtocol/SlotVisualLabV2/AssemblySurface"));
        if(texture!=null) material.SetTexture("_BaseMap",texture);
        material.SetFloat("_UseTexture",textured?1:0);
        material.SetFloat("_EmissionStrength",2.4f);owned.Add(material);return material;
    }

    private Vector3 Point(float x,float y,float z=0) => new Vector3((x-.5f)*worldWidth,(.5f-y)*worldHeight,z);

    private void AddPart(string partName,Rect region,float start,Vector3 fold,Texture2D texture,bool curved)
    {
        var go=NewObject(partName,rig);
        Vector3 pivot=Point(region.center.x,region.center.y);
        float depth=worldHeight*(curved?.075f:.048f);
        var part=new Part{transform=go.transform,destination=pivot,foldedRotation=fold,start=start,depth=depth};
        part.face=Surface(texture,true);part.side=Surface(null,false);
        var uvBounds=new Vector4(region.xMin,1-region.yMax,region.xMax,1-region.yMin);
        part.face.SetVector("_Region",uvBounds);part.side.SetVector("_Region",uvBounds);
        var mesh=new Mesh{name=partName+" solid mesh"};owned.Add(mesh);
        var vertices=new List<Vector3>();var uv=new List<Vector2>();
        var front=new List<int>();var sides=new List<int>();
        const int columns=18,rows=16;
        for(int y=0;y<=rows;y++) for(int x=0;x<=columns;x++)
        {
            float nx=x/(float)columns,ny=y/(float)rows;
            float px=Mathf.Lerp(region.xMin,region.xMax,nx),py=Mathf.Lerp(region.yMin,region.yMax,ny);
            float z=curved?-Mathf.Sin(nx*Mathf.PI)*worldHeight*.045f:0;
            vertices.Add(Point(px,py,z)-pivot);uv.Add(new Vector2(px,1-py));
        }
        for(int y=0;y<rows;y++) for(int x=0;x<columns;x++)
        {
            int a=y*(columns+1)+x;
            front.Add(a);front.Add(a+1);front.Add(a+columns+2);
            front.Add(a+columns+2);front.Add(a+columns+1);front.Add(a);
        }
        // Read the opaque silhouette at regular rows; the side walls follow the actual artwork.
        var contour=new List<Vector3>();
        const int edgeRows=28;
        for(int pass=0;pass<2;pass++) for(int row=0;row<=edgeRows;row++)
        {
            float py=Mathf.Lerp(region.yMin,region.yMax,(pass==0?row:edgeRows-row)/(float)edgeRows);
            float px=pass==0?region.xMin:region.xMax;
            if(!curved && texture.isReadable)
            {
                int ty=Mathf.Clamp(Mathf.RoundToInt((1-py)*(texture.height-1)),0,texture.height-1);
                int from=Mathf.Clamp(Mathf.RoundToInt(region.xMin*texture.width),0,texture.width-1);
                int to=Mathf.Clamp(Mathf.RoundToInt(region.xMax*texture.width),0,texture.width-1);
                for(int col=pass==0?from:to;col>=from && col<=to;col+=pass==0?2:-2)
                    if(texture.GetPixel(col,ty).a>.7f){px=col/(float)texture.width;break;}
            }
            contour.Add(Point(px,py)-pivot);
        }
        part.edge=contour.ToArray();
        var circuitContour=new Vector3[contour.Count];
        for(int i=0;i<contour.Count;i++) circuitContour[i]=contour[i]+pivot;
        circuit.Add(region,start,depth,curved,circuitContour);
        for(int i=0;i<contour.Count;i++)
        {
            int next=(i+1)%contour.Count,a=vertices.Count;
            vertices.Add(contour[i]);vertices.Add(contour[next]);
            vertices.Add(contour[next]+Vector3.forward*depth);vertices.Add(contour[i]+Vector3.forward*depth);
            uv.Add(new Vector2(region.center.x,1-region.center.y));uv.Add(uv[uv.Count-1]);uv.Add(uv[uv.Count-1]);uv.Add(uv[uv.Count-1]);
            sides.Add(a);sides.Add(a+1);sides.Add(a+2);sides.Add(a+2);sides.Add(a+3);sides.Add(a);
        }
        mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.subMeshCount=2;
        mesh.SetTriangles(front,0);mesh.SetTriangles(sides,1);mesh.RecalculateNormals();mesh.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh=mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials=new[]{part.face,part.side};
        parts.Add(part);
    }

    private static float Cubic(float value) {value=Mathf.Clamp01(value);return 1-Mathf.Pow(1-value,3);}
    private static float Phase(float time,float start,float end) => Mathf.Clamp01((time-start)/(end-start));

    public void SetProgress(float time)
    {
        if(rig==null) return;
        rig.position=camera.transform.TransformPoint(center);
        rig.localScale=Vector3.one;
        rig.rotation=camera.transform.rotation;
        float pull=1-Mathf.Pow(1-Phase(time,.12f,.24f),5);
        // Each baton docks on the middle of an outer spine, so its final position has a visible circuit node.
        float spread=Mathf.Lerp(.019f,.395f,pull)*worldWidth;
        float dockY=.085f*worldHeight;
        leftGrip.localPosition=new Vector3(-spread,dockY,-.15f);rightGrip.localPosition=new Vector3(spread,dockY,-.15f);
        leftGrip.localRotation=rightGrip.localRotation=Quaternion.identity;
        bool coreOn=time<.49f;leftGrip.gameObject.SetActive(coreOn);rightGrip.gameObject.SetActive(coreOn);
        foreach(var part in parts)
        {
            part.transform.localPosition=part.destination;
            part.transform.localRotation=Quaternion.identity;
            part.transform.localScale=Vector3.one;
            float solid=Phase(time,.50f,.94f);
            part.face.SetFloat("_Reveal",solid);part.side.SetFloat("_Reveal",solid);
        }
        circuit?.Update(time);
    }

    public void Clear()
    {
        if(camera!=null){camera.orthographic=savedOrtho;camera.fieldOfView=savedFov;camera=null;}
        if(rig!=null) Destroy(rig.gameObject);
        rig=null;parts.Clear();circuit=null;
        foreach(var asset in owned) if(asset!=null) Destroy(asset);
        owned.Clear();
    }
    private void OnDisable()=>Clear();
}
