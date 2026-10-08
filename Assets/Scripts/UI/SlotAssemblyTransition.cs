using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>One core unfolds into circuitry, a wire chassis, then an opaque device.</summary>
public sealed class SlotAssemblyTransition : IDisposable
{
    private sealed class Part
    {
        public VisualElement element;
        public StyleFloat opacity;
        public float targetOpacity;
    }
    private readonly VisualElement host;
    private readonly List<Part> content = new List<Part>();
    private VisualElement assembly;
    private Sequence sequence;
    private int generation;
    private float activeDuration=Duration;
    private SlotMechanicalAssembly3D mechanical;
    public bool IsPlaying { get; private set; }
    public const float Duration = 3.2f;

    public SlotAssemblyTransition(VisualElement host) { this.host = host; }

    public void Play(Texture2D plate = null, VisualElement backplate = null, Action onComplete = null,
        SlotMechanicalAssembly3D mechanical = null, Texture appearance = null)
    {
        Dispose();
        this.mechanical=mechanical;
        if (host == null) return;
        foreach (var child in host.Children())
            content.Add(new Part { element=child, opacity=child.style.opacity,
                targetOpacity=child.style.opacity.keyword==StyleKeyword.Undefined ? child.style.opacity.value : 1 });
        IsPlaying = true;
        SetInputBlocked(true);
        foreach (var part in content) part.element.style.opacity=0;
        int request=++generation;
        host.schedule.Execute(() =>
        {
            if(request!=generation || !IsPlaying) return;
            Build(plate,backplate,onComplete,appearance);
        });
    }

    private void Build(Texture2D plate, VisualElement backplate, Action onComplete, Texture appearance)
    {
        float width=host.resolvedStyle.width,height=host.resolvedStyle.height;
        if(float.IsNaN(width) || float.IsNaN(height) || width<=0 || height<=0)
        { Dispose();onComplete?.Invoke();return; }
        SlotCircuitAssemblyVisual circuit=null;
        bool solid3D=mechanical!=null && mechanical.Prepare(host,plate,appearance);
        activeDuration=solid3D?4.4f:Duration;
        if(!solid3D)
        {
            circuit=new SlotCircuitAssemblyVisual(appearance != null ? appearance : plate);
            assembly=circuit;
            circuit.name="slot-assembly";
            circuit.style.position=Position.Absolute;
            circuit.style.left=0;circuit.style.top=0;
            circuit.style.width=width;circuit.style.height=height;
            host.Add(circuit);
        }
        float progress=0;
        sequence=DOTween.Sequence().SetUpdate(true).SetAutoKill(false);
        sequence.Append(DOTween.To(() => progress,value =>
        {
            progress=value;
            if(solid3D) mechanical.SetProgress(value);
            else circuit.Progress=1-Mathf.Pow(1-value,1.22f);
            // Make the UI background opaque before retiring the temporary assembly geometry.
            // The two rendering paths need a brief overlap rather than a same-frame hard cut.
            float retirement=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.94f,1,value));
            if(solid3D) mechanical.SetPresentationOpacity(retirement);
            else circuit.style.opacity=retirement;
            foreach(var part in content)
            {
                bool isBackplate=part.element==backplate;
                float start=isBackplate ? .86f : plate==null ? .78f : .90f;
                float end=isBackplate ? .96f : .99f;
                float alpha=Mathf.SmoothStep(0,1,Mathf.InverseLerp(start,end,value));
                part.element.style.opacity=alpha*part.targetOpacity;
            }
        },1,activeDuration).SetEase(Ease.Linear));
        sequence.OnComplete(() => { Restore(true);IsPlaying=false;onComplete?.Invoke(); });
        sequence.Play();
    }

    public void Preview(float progress)
    {
        if(sequence==null) return;
        if(progress>=1) sequence.Complete(true);
        else sequence.Goto(Mathf.Clamp01(progress)*activeDuration,false);
    }

    private void Restore(bool deferMechanicalCleanup=false)
    {
        foreach(var part in content) part.element.style.opacity=part.opacity;
        if(deferMechanicalCleanup) mechanical?.RetireAfterUIFrames();
        else mechanical?.Clear();
        assembly?.RemoveFromHierarchy();assembly=null;
        SetInputBlocked(false);
    }

    // Disabling the host also applies Unity's disabled opacity to the whole cabinet.
    // Block interaction without changing the appearance during the reveal.
    private void BlockInput(EventBase evt)
    {
        evt.StopImmediatePropagation();
        evt.PreventDefault();
    }

    private void SetInputBlocked(bool blocked)
    {
        if(host==null) return;
        if(blocked)
        {
            host.RegisterCallback<PointerDownEvent>(BlockInput,TrickleDown.TrickleDown);
            host.RegisterCallback<PointerUpEvent>(BlockInput,TrickleDown.TrickleDown);
            host.RegisterCallback<ClickEvent>(BlockInput,TrickleDown.TrickleDown);
            host.RegisterCallback<KeyDownEvent>(BlockInput,TrickleDown.TrickleDown);
            host.RegisterCallback<NavigationSubmitEvent>(BlockInput,TrickleDown.TrickleDown);
            host.RegisterCallback<NavigationMoveEvent>(BlockInput,TrickleDown.TrickleDown);
        }
        else
        {
            host.UnregisterCallback<PointerDownEvent>(BlockInput,TrickleDown.TrickleDown);
            host.UnregisterCallback<PointerUpEvent>(BlockInput,TrickleDown.TrickleDown);
            host.UnregisterCallback<ClickEvent>(BlockInput,TrickleDown.TrickleDown);
            host.UnregisterCallback<KeyDownEvent>(BlockInput,TrickleDown.TrickleDown);
            host.UnregisterCallback<NavigationSubmitEvent>(BlockInput,TrickleDown.TrickleDown);
            host.UnregisterCallback<NavigationMoveEvent>(BlockInput,TrickleDown.TrickleDown);
        }
    }

    public void Dispose()
    {
        generation++;
        sequence?.Kill();sequence=null;
        if(content.Count>0) Restore();
        content.Clear();IsPlaying=false;
        mechanical=null;
    }
}
