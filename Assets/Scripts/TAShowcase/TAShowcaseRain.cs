using UnityEngine;

// Drives the existing menu solver; no duplicate fluid implementation.
public sealed class TAShowcaseRain : MonoBehaviour
{
    public MainMenuMatrixBackground background;
    public bool Interactive { get; private set; }
    public bool DemoPlaying { get; private set; }
    private Vector2 _previous;
    private float _phase;
    public void Initialize(Camera camera)
    {
        background.ConfigureShowcase(camera,new Vector2(2.5f,-.55f),new Vector2(12.4f,5.6f));
    }
    public void Select(bool interactive,int level)
    {
        Interactive=interactive;DemoPlaying=false;
        background.SetShowcaseInteraction(interactive);
        background.SetShowcasePreset(level);
    }
    public void SetLevel(int level) => background.SetShowcasePreset(level);
    public void ToggleDemo()
    {
        DemoPlaying=!DemoPlaying; _phase=0;_previous=new Vector2(.5f,.5f);
    }
    public void ResetField() { DemoPlaying=false; background.ResetShowcase(); }
    private void Update()
    {
        if(!Interactive || !DemoPlaying)return;
        _phase+=Time.unscaledDeltaTime;
        Vector2 next=new Vector2(.5f+Mathf.Sin(_phase*1.7f)*.27f,.5f+Mathf.Sin(_phase*3.4f)*.30f);
        background.QueueShowcaseStroke(_previous,next);_previous=next;
    }
}
