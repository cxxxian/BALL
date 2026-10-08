using UnityEngine;

public sealed class TAShowcaseExtras : MonoBehaviour
{
    public Material wallMaterial;
    public PlayfieldTopArc Wall { get; private set; }
    public Scene2WeaknessReveal Shield { get; private set; }
    private GameObject _wallRoot, _shieldRoot;
    private bool _shieldStudy, _revealing;
    private float _progress;
    public void Select(bool shield)
    {
        if(Wall == null)
        {
            _wallRoot=new GameObject("Production Wall Frame");_wallRoot.SetActive(false);_wallRoot.transform.SetParent(transform,false);
            _wallRoot.transform.position=new Vector3(2.5f,0,0);
            Wall=_wallRoot.AddComponent<PlayfieldTopArc>();
            Wall.wallCenterX=3.2f;Wall.wallBottomY=-3.3f;Wall.shoulderY=.5f;Wall.apexY=2f;
            Wall.wallThickness=.32f;Wall.hideSideWallSprites=false;Wall.wallMaterial=wallMaterial;
            Wall.Rebuild();Wall.GetComponent<EdgeCollider2D>().enabled=false;
            var pulse=Wall.GetComponent<PlayfieldWallPulse>();pulse.frame=Wall;pulse.frameRenderer=Wall.FrameRenderer;
            pulse.spreadSpeed=7f;pulse.bandWidth=.7f;pulse.duration=1.4f;
        }
        if(Shield == null)
        {
            _shieldRoot=new GameObject("Production Reactive Shield");_shieldRoot.SetActive(false);_shieldRoot.transform.SetParent(transform,false);
            _shieldRoot.transform.position=new Vector3(2.5f,-.35f,0);
            Shield=_shieldRoot.AddComponent<Scene2WeaknessReveal>();Shield.ConfigurePresentationRadius(2.25f);
        }
        _shieldStudy=shield;_revealing=false;
        _wallRoot.SetActive(!shield);_shieldRoot.SetActive(shield);
        if(shield)Shield.SetExposed(false);
    }
    public void Trigger()
    {
        if(_shieldStudy)
        {
            Shield.SetExposed(false);
            float angle=Random.Range(0,Mathf.PI*2);
            Shield.PulseAt((Vector2)_shieldRoot.transform.position+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*1.7f);
        }
        else Wall.GetComponent<PlayfieldWallPulse>().TriggerAtWorld(new Vector2(Random.value<.5f?-3.2f:3.2f,Random.Range(-2.8f,.5f)));
    }
    public void Reveal()
    {
        if(!_shieldStudy)return;
        _revealing=true;_progress=0;Shield.Begin();
    }
    public void ResetStudy()
    {
        _revealing=false;if(Shield != null) { Shield.End();Shield.SetExposed(false); }
    }
    private void Update()
    {
        if(_revealing)
        {
            _progress+=Time.deltaTime/2.5f;Shield.Show(_progress);
            if(_progress>=1f) { _revealing=false;Shield.End(); }
        }
        if(Input.GetMouseButtonDown(0))
        {
            var camera=Camera.main;if(camera == null)return;
            Vector2 p=camera.ScreenToWorldPoint(Input.mousePosition);
            if(p.x < -3.7f || p.x > 8.7f || p.y < -3.4f || p.y > 2.2f)return;
            if(_shieldStudy)Shield.PulseAt(p);
            else Wall.GetComponent<PlayfieldWallPulse>().TriggerAtWorld(p-(Vector2)_wallRoot.transform.position);
        }
    }
}
