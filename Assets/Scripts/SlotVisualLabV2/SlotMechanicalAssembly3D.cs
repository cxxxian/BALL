using System.Collections;
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
    private Texture faceTexture;
    private Coroutine retirement;
    private readonly List<Material> coreMaterials=new List<Material>();
    private LineRenderer coreLink;
    private readonly List<Material> coreEmitters=new List<Material>();
    private readonly List<LineRenderer> growthHeads=new List<LineRenderer>();
    private readonly List<Material> railMaterials=new List<Material>();
    private readonly List<Mesh> railMeshes=new List<Mesh>();
    private readonly List<Vector3[]> railPaths=new List<Vector3[]>();

    public bool Prepare(VisualElement stage,Texture2D texture,Texture appearance = null)
    {
        Clear();
        var bloom=GetComponent<SlotVisualLabV2Bloom>();
        if(bloom==null || bloom.outputCamera==null || texture==null) return false;
        faceTexture=appearance != null ? appearance : texture;
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
        leftGrip=Grip("Seed / left original spine",-1);rightGrip=Grip("Seed / right original spine",1);
        coreLink=NewObject("Core / ignition link",rig).AddComponent<LineRenderer>();
        coreLink.useWorldSpace=false;coreLink.sharedMaterial=wire;
        coreLink.widthMultiplier=worldHeight*.0008f;coreLink.positionCount=2;
        coreLink.numCapVertices=3;
        coreLink.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        SetProgress(0);
        return true;
    }

    private GameObject NewObject(string objectName,Transform parent)
    {
        var go=new GameObject(objectName){hideFlags=HideFlags.HideAndDontSave,layer=5};
        go.transform.SetParent(parent,false);return go;
    }

    private Transform Grip(string objectName,int side)
    {
        var root=NewObject(side<0?"Baton / left solid grip":"Baton / right solid grip",rig).transform;
        float w=worldWidth*.085f,h=worldHeight*.021f,d=worldHeight*.022f;
        BatonBox(root,"Graphite grip",new Vector3(w,h,d),Vector3.zero,false);
        BatonBox(root,"Inset cyan groove",new Vector3(w*.80f,h*.065f,d*.035f),
            new Vector3(0,0,-d*.51f),true);
        for(int end=-1;end<=1;end+=2)
        {
            var points=end>0
                ?new[]{Point(side<0?.09f:.91f,.415f),Point(side<0?.09f:.91f,.23f),
                    Point(side<0?.15f:.85f,.185f),Point(side<0?.16f:.84f,.065f),Point(side<0?.20f:.80f,.03f)}
                :new[]{Point(side<0?.09f:.91f,.415f),Point(side<0?.09f:.91f,.68f),
                    Point(side<0?.055f:.945f,.735f),Point(side<0?.10f:.90f,.895f),Point(side<0?.16f:.84f,.915f)};
            railPaths.Add(points);
            var rail=NewObject("Baton / extruded rail",rig);
            var mesh=new Mesh{name="Continuous solid guide"};owned.Add(mesh);railMeshes.Add(mesh);
            rail.AddComponent<MeshFilter>().sharedMesh=mesh;
            var mat=Surface(null,false);mat.SetFloat("_Reveal",1);mat.SetColor("_BodyColor",new Color(.4f,.5f,.65f));
            mat.SetFloat("_GuideRetire",1);railMaterials.Add(mat);rail.AddComponent<MeshRenderer>().sharedMaterial=mat;
            var head=NewObject("Baton / advancing light",rig).AddComponent<LineRenderer>();
            head.useWorldSpace=false;head.sharedMaterial=wire;head.widthMultiplier=worldWidth*.003f;
            head.numCapVertices=3;head.positionCount=2;
            head.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            growthHeads.Add(head);
        }
        return root;
    }

    private void BatonBox(Transform parent,string name,Vector3 size,Vector3 pos,bool emissive)
    {
        var go=NewObject(name,parent);go.transform.localPosition=pos;
        float x=size.x*.5f,y=size.y*.5f,z=size.z*.5f;
        float cut=Mathf.Min(x,y)*.24f;
        var outline=new[]{new Vector2(-x+cut,-y),new Vector2(x-cut,-y),new Vector2(x,-y+cut),
            new Vector2(x,y-cut),new Vector2(x-cut,y),new Vector2(-x+cut,y),
            new Vector2(-x,y-cut),new Vector2(-x,-y+cut)};
        var rings=new Vector3[4,8];
        for(int r=0;r<4;r++)for(int i=0;i<8;i++)
        {
            float k=r==0||r==3?.90f:1;
            rings[r,i]=new Vector3(outline[i].x*k,outline[i].y*k,r==0?-z:r==1?-z*.75f:r==2?z*.75f:z);
        }
        var vertices=new List<Vector3>();var triangles=new List<int>();
        for(int r=0;r<3;r++)for(int i=0;i<8;i++)
        {
            int j=(i+1)%8,n=vertices.Count;
            vertices.Add(rings[r,i]);vertices.Add(rings[r,j]);vertices.Add(rings[r+1,j]);vertices.Add(rings[r+1,i]);
            triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
        }
        for(int i=0;i<8;i++)
        {
            int j=(i+1)%8,n=vertices.Count;
            vertices.Add(new Vector3(0,0,-z));vertices.Add(rings[0,j]);vertices.Add(rings[0,i]);
            triangles.AddRange(new[]{n,n+1,n+2});
            n=vertices.Count;vertices.Add(new Vector3(0,0,z));vertices.Add(rings[3,i]);vertices.Add(rings[3,j]);
            triangles.AddRange(new[]{n,n+1,n+2});
        }
        var mesh=new Mesh{name=name};owned.Add(mesh);mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);
        mesh.uv=new Vector2[vertices.Count];mesh.RecalculateNormals();mesh.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh=mesh;
        var mat=Surface(null,false);mat.SetFloat("_Reveal",1);mat.SetColor("_BodyColor",new Color(.35f,.45f,.55f));
        if(emissive){mat.SetFloat("_GlowStrength",.7f);coreEmitters.Add(mat);}
        coreMaterials.Add(mat);go.AddComponent<MeshRenderer>().sharedMaterial=mat;
    }

    private void GrowRail(int index,float progress)
    {
        var source=railPaths[index];
        float total=0;
        for(int i=1;i<source.Length;i++)total+=Vector3.Distance(source[i-1],source[i]);
        float remaining=total*progress;
        var path=new List<Vector3>{source[0]};
        for(int i=1;i<source.Length;i++)
        {
            float length=Vector3.Distance(source[i-1],source[i]);
            if(remaining<length){path.Add(Vector3.Lerp(source[i-1],source[i],remaining/length));break;}
            path.Add(source[i]);remaining-=length;
        }
        var vertices=new List<Vector3>();var triangles=new List<int>();
        float width=worldWidth*.020f,depth=worldHeight*.012f;
        for(int i=0;i<path.Count;i++)
        {
            Vector3 tangent=(path[Mathf.Min(i+1,path.Count-1)]-path[Mathf.Max(0,i-1)]).normalized;
            Vector3 across=new Vector3(-tangent.y,tangent.x,0)*width*.5f;
            vertices.Add(path[i]-across+Vector3.back*depth);vertices.Add(path[i]+across+Vector3.back*depth);
            vertices.Add(path[i]+across);vertices.Add(path[i]-across);
            if(i>0)for(int side=0;side<4;side++)
            {
                int n=(i-1)*4+side,next=(side+1)%4;
                triangles.AddRange(new[]{n,(i-1)*4+next,i*4+next,n,i*4+next,i*4+side});
            }
        }
        var mesh=railMeshes[index];mesh.Clear();mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);
        var uv=new Vector2[vertices.Count];for(int i=0;i<vertices.Count;i++)uv[i]=new Vector2(vertices[i].x/worldWidth+.5f,vertices[i].y/worldHeight+.5f);
        mesh.uv=uv;mesh.RecalculateNormals();mesh.RecalculateBounds();
        var head=growthHeads[index];
        head.enabled=progress>0 && progress<1;
        var tip=path[path.Count-1]+Vector3.back*(depth+worldHeight*.001f);
        var direction=(path[path.Count-1]-path[path.Count-2]).normalized;
        head.SetPosition(0,tip-direction*worldHeight*.018f);head.SetPosition(1,tip);
        head.startColor=new Color(.02f,.3f,.5f,.2f);head.endColor=new Color(.15f,1,1,1);
    }

    private Material Surface(Texture texture,bool textured)
    {
        var material=new Material(Shader.Find("ReboundProtocol/SlotVisualLabV2/AssemblySurface"));
        if(texture!=null) material.SetTexture("_BaseMap",texture);
        material.SetFloat("_UseTexture",textured?1:0);
        var bloom=GetComponent<SlotVisualLabV2Bloom>();
        material.SetFloat("_EmissionStrength",bloom != null ? bloom.emissionStrength : 2.4f);owned.Add(material);return material;
    }

    private Vector3 Point(float x,float y,float z=0) => new Vector3((x-.5f)*worldWidth,(.5f-y)*worldHeight,z);

    private void AddPart(string partName,Rect region,float start,Vector3 fold,Texture2D texture,bool curved)
    {
        var go=NewObject(partName,rig);
        Vector3 pivot=Point(region.center.x,region.center.y);
        float depth=worldHeight*(curved?.075f:.048f);
        var part=new Part{transform=go.transform,destination=pivot,foldedRotation=fold,start=start,depth=depth};
        part.face=Surface(faceTexture,true);part.side=Surface(null,false);
        var uvBounds=new Vector4(region.xMin,1-region.yMax,region.xMax,1-region.yMin);
        part.face.SetVector("_Region",uvBounds);part.side.SetVector("_Region",uvBounds);
        part.face.SetFloat("_GrowthMode",0);part.side.SetFloat("_GrowthMode",0);
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
            var first=contour[i]+pivot;var second=contour[next]+pivot;
            var firstUV=new Vector2(first.x/worldWidth+.5f,first.y/worldHeight+.5f);
            var secondUV=new Vector2(second.x/worldWidth+.5f,second.y/worldHeight+.5f);
            uv.Add(firstUV);uv.Add(secondUV);uv.Add(secondUV);uv.Add(firstUV);
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
        float travel=Mathf.SmoothStep(0,1,Phase(time,.07f,.24f));
        float spread=Mathf.Lerp(worldWidth*.043f,worldWidth*.395f,travel);
        float dockY=.085f*worldHeight;
        float retract=Mathf.SmoothStep(0,1,Phase(time,.31f,.49f));
        leftGrip.localPosition=new Vector3(-spread,dockY,-worldHeight*.012f);
        rightGrip.localPosition=new Vector3(spread,dockY,-worldHeight*.012f);
        leftGrip.localRotation=rightGrip.localRotation=Quaternion.identity;
        leftGrip.localScale=rightGrip.localScale=new Vector3(Mathf.Lerp(1,.12f,retract),1,1);
        float retirement=1-Mathf.SmoothStep(0,1,Phase(time,.43f,.57f));
        foreach(var mat in coreMaterials)mat.SetFloat("_Opacity",retirement);
        foreach(var mat in railMaterials){mat.SetFloat("_Opacity",1);mat.SetFloat("_ShellProgress",Phase(time,.50f,.94f));}
        float ignition=Mathf.SmoothStep(0,1,Phase(time,0,.07f));
        foreach(var mat in coreEmitters)mat.SetFloat("_GlowStrength",ignition*.65f);
        float growth=Mathf.SmoothStep(0,1,Phase(time,.22f,.46f));
        for(int i=0;i<railMeshes.Count;i++)GrowRail(i,growth);
        coreLink.enabled=time>.07f && time<.31f;
        coreLink.SetPosition(0,new Vector3(-spread,dockY,-worldHeight*.025f));
        coreLink.SetPosition(1,new Vector3(spread,dockY,-worldHeight*.025f));
        float linkAlpha=travel*(1-Mathf.SmoothStep(0,1,Phase(time,.24f,.31f)))*.18f;
        coreLink.startColor=coreLink.endColor=new Color(.15f,.7f,1,linkAlpha);
        foreach(var part in parts)
        {
            part.transform.localPosition=part.destination;
            part.transform.localRotation=Quaternion.identity;
            part.transform.localScale=Vector3.one;
            float solid=Phase(time,.50f,.94f);
            part.face.SetFloat("_Reveal",solid);part.side.SetFloat("_Reveal",solid);
            part.face.SetFloat("_AmberVisibility",Mathf.SmoothStep(0,1,Phase(time,.55f,.68f)));
        }
        circuit?.Update(time);
    }

    public void SetPresentationOpacity(float opacity)
    {
        foreach(var mat in coreMaterials)mat.SetFloat("_Opacity",Mathf.Clamp01(opacity));
        foreach(var mat in railMaterials)mat.SetFloat("_Opacity",Mathf.Clamp01(opacity));
        foreach(var part in parts)
        {
            part.face.SetFloat("_Opacity",Mathf.Clamp01(opacity));
            part.side.SetFloat("_Opacity",Mathf.Clamp01(opacity));
        }
    }

    public void RetireAfterUIFrames()
    {
        if(retirement!=null) StopCoroutine(retirement);
        SetPresentationOpacity(0);
        retirement=StartCoroutine(Retire());
    }

    private IEnumerator Retire()
    {
        // Let UI Toolkit publish its fully visible background before releasing the old drawables.
        yield return null;
        yield return null;
        retirement=null;
        Clear();
    }

    public void Clear()
    {
        if(retirement!=null) {StopCoroutine(retirement);retirement=null;}
        if(camera!=null){camera.orthographic=savedOrtho;camera.fieldOfView=savedFov;camera=null;}
        if(rig!=null) Destroy(rig.gameObject);
        rig=null;parts.Clear();circuit=null;faceTexture=null;
        coreMaterials.Clear();coreEmitters.Clear();railMaterials.Clear();railMeshes.Clear();railPaths.Clear();growthHeads.Clear();coreLink=null;
        foreach(var asset in owned) if(asset!=null) Destroy(asset);
        owned.Clear();
    }
    private void OnDisable()=>Clear();
}
