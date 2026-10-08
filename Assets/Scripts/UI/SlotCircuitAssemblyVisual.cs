using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Energy paths grow from one core, then synthesize the opaque device surface in place.</summary>
public sealed class SlotCircuitAssemblyVisual : VisualElement
{
    private sealed class Route
    {
        public Vector2[] points;
        public float start, duration;
    }
    private readonly List<Route> routes = new List<Route>();
    private readonly Texture plate;
    private float progress;
    public float Progress { get => progress; set { progress = value; MarkDirtyRepaint(); } }

    public SlotCircuitAssemblyVisual(Texture plate)
    {
        this.plate = plate;
        pickingMode = PickingMode.Ignore;
        name = "circuit-synthesis";
        generateVisualContent += Draw;
        RouteAt(.15f, .23f, new Vector2(.5f,.5f), new Vector2(.5f,.08f));
        RouteAt(.15f, .23f, new Vector2(.5f,.5f), new Vector2(.5f,.91f));
        for (int i = 0; i < 10; i++)
        {
            float y = .12f + i * .081f;
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side < 0 ? .1f : .9f;
                float start = .23f + Mathf.Abs(y - .5f) * .29f;
                RouteAt(start, .19f, new Vector2(.5f,y), new Vector2(.5f+side*.065f,y),
                    new Vector2(.5f+side*.09f,y-.018f), new Vector2(x,y-.018f));
                RouteAt(start+.12f, .16f, new Vector2(x,y-.018f), new Vector2(x,y+.045f),
                    new Vector2(x-side*.025f,y+.068f));
            }
        }
        for (int side = -1; side <= 1; side += 2)
        {
            float x = side < 0 ? .08f : .92f;
            RouteAt(.34f,.28f, new Vector2(x,.5f),new Vector2(x,.22f),
                new Vector2(x+side*.015f,.10f), new Vector2(.5f+side*.31f,.032f),new Vector2(.5f,.032f));
            RouteAt(.34f,.28f, new Vector2(x,.5f),new Vector2(x,.72f),
                new Vector2(.5f+side*.455f,.87f),new Vector2(.5f+side*.47f,.92f),new Vector2(.5f,.92f));
        }
        // Reel ribs are traced as curved light rails; they never move as image fragments.
        for (int reel = 0; reel < 3; reel++)
        {
            float center = .252f + reel * .235f;
            var outline = new List<Vector2>();
            for (int i=0;i<=18;i++)
            {
                float t=i/18f;
                outline.Add(new Vector2(center-.073f-.013f*Mathf.Sin(t*Mathf.PI),.24f+t*.345f));
            }
            for (int i=18;i>=0;i--)
            {
                float t=i/18f;
                outline.Add(new Vector2(center+.073f+.013f*Mathf.Sin(t*Mathf.PI),.24f+t*.345f));
            }
            outline.Add(outline[0]);
            RouteAt(.40f+reel*.035f,.25f,outline.ToArray());
            for (int row=0;row<6;row++)
            {
                float y=.275f+row*.05f;
                RouteAt(.48f+row*.015f+reel*.025f,.14f,new Vector2(center-.075f,y),new Vector2(center+.075f,y));
            }
        }
        RouteAt(.44f,.24f,new Vector2(.5f,.20f),new Vector2(.24f,.20f),new Vector2(.15f,.16f),
            new Vector2(.17f,.075f),new Vector2(.83f,.075f),new Vector2(.85f,.16f),new Vector2(.76f,.20f),new Vector2(.5f,.20f));
        RouteAt(.45f,.25f,new Vector2(.5f,.61f),new Vector2(.14f,.61f),new Vector2(.14f,.70f),
            new Vector2(.19f,.735f),new Vector2(.81f,.735f),new Vector2(.86f,.70f),new Vector2(.86f,.61f),new Vector2(.5f,.61f));
        RouteAt(.50f,.22f,new Vector2(.5f,.79f),new Vector2(.24f,.79f),new Vector2(.16f,.83f),
            new Vector2(.21f,.86f),new Vector2(.58f,.86f),new Vector2(.63f,.83f),new Vector2(.58f,.79f),new Vector2(.5f,.79f));
    }

    private void RouteAt(float start, float duration, params Vector2[] points) =>
        routes.Add(new Route { start=start, duration=duration, points=points });

    private void Draw(MeshGenerationContext context)
    {
        float w=contentRect.width, h=contentRect.height;
        if (w<=0 || h<=0) return;
        DrawSurface(context,w,h);
        var painter=context.painter2D;
        float circuitAlpha=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.76f,1,progress));
        // Two physical grip halves separate, exposing the energy lattice between them.
        float open=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.08f,.25f,progress));
        float gripAlpha=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.27f,.48f,progress));
        float offset=.012f+open*.18f;
        DrawGrip(painter,new Vector2((.5f-offset)*w,.5f*h),w*.016f,h*.125f,gripAlpha);
        DrawGrip(painter,new Vector2((.5f+offset)*w,.5f*h),w*.016f,h*.125f,gripAlpha);
        Trace(painter,new[] {new Vector2(.5f-offset,.5f),new Vector2(.5f+offset,.5f)},1,w,h,gripAlpha);
        foreach (var route in routes)
        {
            float t=Mathf.Clamp01((progress-route.start)/route.duration);
            if (t>0 && circuitAlpha>0) Trace(painter,route.points,t,w,h,circuitAlpha);
        }
    }

    private static void DrawGrip(Painter2D p, Vector2 center, float halfWidth, float halfHeight, float alpha)
    {
        if(alpha<=0) return;
        p.BeginPath();
        p.MoveTo(center+new Vector2(-halfWidth,-halfHeight+6));
        p.LineTo(center+new Vector2(0,-halfHeight));
        p.LineTo(center+new Vector2(halfWidth,-halfHeight+6));
        p.LineTo(center+new Vector2(halfWidth,halfHeight-6));
        p.LineTo(center+new Vector2(0,halfHeight));
        p.LineTo(center+new Vector2(-halfWidth,halfHeight-6));
        p.ClosePath();
        p.fillColor=new Color(.025f,.06f,.085f,alpha); p.Fill();
        p.lineWidth=2; p.strokeColor=new Color(.08f,.9f,1,alpha); p.Stroke();
        p.BeginPath(); p.MoveTo(center+new Vector2(0,-halfHeight*.75f));p.LineTo(center+new Vector2(0,halfHeight*.75f));
        p.lineWidth=3;p.strokeColor=new Color(.7f,1,1,alpha);p.Stroke();
    }

    private static void Trace(Painter2D p, Vector2[] points, float t, float w, float h, float alpha)
    {
        float length=0;
        for(int i=1;i<points.Length;i++) length+=Vector2.Distance(points[i-1],points[i]);
        float remaining=length*t;
        Vector2 tip=Vector2.Scale(points[0],new Vector2(w,h));
        for(int pass=0;pass<2;pass++)
        {
            float budget=remaining;
            p.BeginPath();p.MoveTo(Vector2.Scale(points[0],new Vector2(w,h)));
            for(int i=1;i<points.Length && budget>0;i++)
            {
                float distance=Vector2.Distance(points[i-1],points[i]);
                Vector2 next=Vector2.Lerp(points[i-1],points[i],Mathf.Min(1,budget/Mathf.Max(.0001f,distance)));
                tip=Vector2.Scale(next,new Vector2(w,h));p.LineTo(tip);budget-=distance;
            }
            p.lineWidth=pass==0?6:1.5f;
            p.strokeColor=pass==0?new Color(0,.65f,1,alpha*.13f):new Color(.1f,.85f,1,alpha*.8f);
            p.Stroke();
        }
        if(t<1)
        {
            p.BeginPath();p.Arc(tip,2.5f,Angle.Degrees(0),Angle.Degrees(360));
            p.fillColor=new Color(.8f,1,1,alpha);p.Fill();
        }
    }

    private void DrawSurface(MeshGenerationContext context,float w,float h)
    {
        if(plate==null || progress<.57f) return;
        const int columns=24,rows=40;
        var mesh=context.Allocate(columns*rows*4,columns*rows*6,plate);
        Rect uv=mesh.uvRegion;
        for(int y=0;y<rows;y++) for(int x=0;x<columns;x++)
        {
            float nx=(x+.5f)/columns,ny=(y+.5f)/rows;
            // The solidification front follows the trunk and lateral buses out from the core.
            float distance=Mathf.Abs(ny-.5f)*.60f+Mathf.Abs(nx-.5f)*.40f;
            float variation=((x*17+y*31)%13)/13f*.035f;
            float arrival=.57f+distance*.48f+variation;
            float amount=Mathf.SmoothStep(0,1,Mathf.Clamp01((progress-arrival)/.12f));
            float hot=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((progress-arrival-.055f)/.11f));
            Color32 color=Color.Lerp(Color.white,new Color(.16f,.9f,1),hot*.7f);
            color.a=(byte)(amount*255);
            float left=(float)x/columns,right=(float)(x+1)/columns,top=(float)y/rows,bottom=(float)(y+1)/rows;
            mesh.SetNextVertex(V(left*w,top*h,left,1-top,uv,color));
            mesh.SetNextVertex(V(right*w,top*h,right,1-top,uv,color));
            mesh.SetNextVertex(V(right*w,bottom*h,right,1-bottom,uv,color));
            mesh.SetNextVertex(V(left*w,bottom*h,left,1-bottom,uv,color));
            ushort index=(ushort)((y*columns+x)*4);
            mesh.SetNextIndex(index);mesh.SetNextIndex((ushort)(index+1));mesh.SetNextIndex((ushort)(index+2));
            mesh.SetNextIndex((ushort)(index+2));mesh.SetNextIndex((ushort)(index+3));mesh.SetNextIndex(index);
        }
    }

    private static Vertex V(float x,float y,float u,float v,Rect uv,Color32 color) => new Vertex
    {position=new Vector3(x,y,Vertex.nearZ),uv=new Vector2(uv.x+u*uv.width,uv.y+v*uv.height),tint=color};
}
