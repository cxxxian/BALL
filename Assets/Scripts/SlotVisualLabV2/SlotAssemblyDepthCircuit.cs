using System.Collections.Generic;
using UnityEngine;

/// <summary>Continuous front rails and recessed return rails, completed before surface synthesis.</summary>
public sealed class SlotAssemblyDepthCircuit
{
    private sealed class Route
    {
        public LineRenderer line;
        public Vector3[] points;
        public float length,start,end,brightness;
    }
    private readonly List<Route> routes=new List<Route>();
    private readonly Transform rig;
    private readonly Material material;
    private readonly float width,height;
    public SlotAssemblyDepthCircuit(Transform rig,Material material,float width,float height)
    {this.rig=rig;this.material=material;this.width=width;this.height=height;}
    private Vector3 P(float x,float y,float z)=>new Vector3((x-.5f)*width,(.5f-y)*height,z);
    private void RouteLine(Vector3[] points,float start,float end,float brightness,float thickness)
    {
        var go=new GameObject("Circuit / continuous depth rail"){layer=5,hideFlags=HideFlags.HideAndDontSave};
        go.transform.SetParent(rig,false);
        var line=go.AddComponent<LineRenderer>();
        line.useWorldSpace=false;line.sharedMaterial=material;line.widthMultiplier=height*thickness;
        line.numCornerVertices=3;line.numCapVertices=3;
        line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows=false;
        var route=new Route{line=line,points=points,start=start,end=end,brightness=brightness};
        for(int i=1;i<points.Length;i++) route.length+=Vector3.Distance(points[i-1],points[i]);
        routes.Add(route);
    }
    public void Add(Rect r,float order,float depth,bool curved,Vector3[] silhouette)
    {
        float start=.23f+(order-.24f)*.22f,end=start+.17f;
        float bevel=Mathf.Min(r.width,r.height)*.12f;
        // Chamfered closed rails share the same coordinates as the solid panels.
        var front=new[]{P(r.xMin+bevel,r.yMax,-.002f*height),P(r.xMin,r.yMax-bevel,-.002f*height),
            P(r.xMin,r.yMin+bevel,-.002f*height),P(r.xMin+bevel,r.yMin,-.002f*height),
            P(r.xMax-bevel,r.yMin,-.002f*height),P(r.xMax,r.yMin+bevel,-.002f*height),
            P(r.xMax,r.yMax-bevel,-.002f*height),P(r.xMax-bevel,r.yMax,-.002f*height),P(r.xMin+bevel,r.yMax,-.002f*height)};
        if(!curved)
        {
            front=new Vector3[silhouette.Length+1];
            for(int i=0;i<silhouette.Length;i++) front[i]=silhouette[i]+Vector3.back*height*.002f;
            front[front.Length-1]=front[0];
        }
        RouteLine(front,start,end,1,.0016f);
        // Recessed rails are inset: visible parallax comes from the depth connections, not rotation.
        var rear=new Vector3[front.Length];
        Vector3 middle=P(r.center.x,r.center.y,0);
        for(int i=0;i<front.Length;i++) rear[i]=middle+(front[i]-middle)*.88f+Vector3.forward*depth;
        RouteLine(rear,start+.025f,end+.03f,.30f,.0011f);
        foreach(int index in new[]{0,(front.Length-1)/4,(front.Length-1)/2,(front.Length-1)*3/4})
            RouteLine(new[]{rear[index],front[index]},end-.05f,end+.025f,.65f,.0012f);
        float branchX=r.center.x<.5f?.105f:.895f;
        float sourceY=.415f;
        float socketX=branchX<r.center.x?r.xMin:r.xMax;
        RouteLine(new[]{P(branchX,sourceY,depth),P(branchX,r.center.y,depth),P(socketX,r.center.y,depth+depth)},
            .19f,start+.045f,.42f,.0012f);
        if(curved)
        {
            foreach(float y in new[]{r.yMin,r.yMax})
            {
                var arc=new Vector3[17];
                for(int i=0;i<arc.Length;i++) {float t=i/16f;arc[i]=P(Mathf.Lerp(r.xMin,r.xMax,t),y,-Mathf.Sin(t*Mathf.PI)*height*.045f-height*.002f);}
                RouteLine(arc,start+.035f,end+.02f,.85f,.0015f);
            }
        }
    }
    public void Update(float time)
    {
        float fade=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.63f,.86f,time));
        foreach(var route in routes)
        {
            float phase=Mathf.InverseLerp(route.start,route.end,time);
            float drawn=route.length*(1-Mathf.Pow(1-phase,2));
            route.line.enabled=phase>0 && fade>0;
            var positions=new List<Vector3>{route.points[0]};
            for(int i=1;i<route.points.Length;i++)
            {
                float segment=Vector3.Distance(route.points[i-1],route.points[i]);
                if(drawn<segment){positions.Add(Vector3.Lerp(route.points[i-1],route.points[i],drawn/segment));break;}
                positions.Add(route.points[i]);drawn-=segment;
            }
            route.line.positionCount=positions.Count;route.line.SetPositions(positions.ToArray());
            Color color=new Color(.025f*route.brightness,.52f*route.brightness,.72f*route.brightness,fade);
            route.line.startColor=route.line.endColor=color;
        }
    }
}
