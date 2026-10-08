using System.Collections.Generic;
using UnityEngine;

/// <summary>Connected beveled white voxels grow from the rails and remain before material conversion.</summary>
public sealed class SlotAssemblyPixelCloud
{
    private struct Seed
    {
        public Transform part;
        public Vector3 localTarget, size;
        public float start, duration;
        public Color color;
    }
    private readonly List<Seed> seeds=new List<Seed>();
    private readonly Transform rig;
    private readonly ParticleSystem system;
    private readonly float height;
    private ParticleSystem.Particle[] particles;

    public SlotAssemblyPixelCloud(Transform rig,Material material,Mesh cube,float height)
    {
        this.rig=rig;this.height=height;
        var go=new GameObject("Assembly / pixel flux"){hideFlags=HideFlags.HideAndDontSave,layer=5};
        go.transform.SetParent(rig,false);
        system=go.AddComponent<ParticleSystem>();
        var main=system.main;main.playOnAwake=false;main.loop=false;
        main.simulationSpace=ParticleSystemSimulationSpace.Local;main.maxParticles=6000;
        main.startSpeed=0;main.startLifetime=1000;main.scalingMode=ParticleSystemScalingMode.Hierarchy;
        var emission=system.emission;emission.enabled=false;
        var shape=system.shape;shape.enabled=false;
        system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var renderer=system.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=cube;renderer.sharedMaterial=material;
        renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>{ParticleSystemVertexStream.Position,ParticleSystemVertexStream.Normal,ParticleSystemVertexStream.Color});
        renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows=false;
    }

    public void AddSurface(Transform part,Rect region,Texture2D texture,Vector3 pivot,float width,float start,bool curved)
    {
        const int columns=48,rows=86;
        int left=Mathf.FloorToInt(region.xMin*columns),right=Mathf.CeilToInt(region.xMax*columns);
        int top=Mathf.FloorToInt(region.yMin*rows),bottom=Mathf.CeilToInt(region.yMax*rows);
        for(int y=top;y<bottom;y++) for(int x=left;x<right;x++)
        {
            float x0=Mathf.Max(x/(float)columns,region.xMin),x1=Mathf.Min((x+1f)/columns,region.xMax);
            float y0=Mathf.Max(y/(float)rows,region.yMin),y1=Mathf.Min((y+1f)/rows,region.yMax);
            float nx=(x0+x1)*.5f,ny=(y0+y1)*.5f;
            var sample=texture.isReadable?texture.GetPixelBilinear(nx,1-ny):Color.cyan;
            if(sample.a<.05f) continue;
            float dx=Mathf.Min(nx-region.xMin,region.xMax-nx)*width;
            float dy=Mathf.Min(ny-region.yMin,region.yMax-ny)*height;
            float radius=Mathf.Min(region.width*width,region.height*height)*.5f;
            float distance=Mathf.Clamp01(Mathf.Min(dx,dy)/radius);
            float relativeX=(nx-region.xMin)/region.width;
            float z=curved?-Mathf.Sin(relativeX*Mathf.PI)*height*.045f:0;
            var target=new Vector3((nx-.5f)*width,(.5f-ny)*height,z)-pivot;
            seeds.Add(new Seed
            {
                part=part,localTarget=target+Vector3.back*height*.007f,
                start=.52f+distance*.24f,
                duration=.018f,
                size=new Vector3((x1-x0)*width,(y1-y0)*height,height*.015f),
                color=Color.white
            });
        }
        particles=new ParticleSystem.Particle[seeds.Count];
    }

    public void Update(float time)
    {
        if(particles==null) return;
        for(int i=0;i<seeds.Count;i++)
        {
            Seed seed=seeds[i];
            float travel=Mathf.Clamp01((time-seed.start)/seed.duration);
            float t=1-Mathf.Pow(1-travel,3);
            Vector3 end=rig.InverseTransformPoint(seed.part.TransformPoint(seed.localTarget));
            Vector3 position=end+Vector3.forward*height*.025f*(1-t);
            float fade=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((time-seed.start-.14f)/.08f));
            float alpha=time<seed.start?0:fade;
            Color color=seed.color;color.a=alpha;
            particles[i]=new ParticleSystem.Particle
            {
                position=position,startSize3D=new Vector3(seed.size.x,seed.size.y,seed.size.z*Mathf.Lerp(.2f,1,t)),startColor=color,
                rotation3D=Vector3.zero,
                startLifetime=1000,remainingLifetime=1000,randomSeed=(uint)(i+1)
            };
        }
        system.SetParticles(particles,particles.Length);
    }
}
