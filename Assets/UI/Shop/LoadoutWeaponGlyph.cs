using UnityEngine;

/// <summary>Scalable TRON style weapon schematic, independent of the card frame.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public class LoadoutWeaponGlyph : UnityEngine.UI.MaskableGraphic
{
    [SerializeField] private FlipperWeaponType weaponType;
    public void SetWeapon(FlipperWeaponType type) { weaponType = type; SetVerticesDirty(); }
    private Vector2 P(float x, float y)
    {
        var r = rectTransform.rect;
        return r.center + new Vector2(x * r.width, y * r.height);
    }
    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
    {
        vh.Clear();
        Vector2[] outline = { P(-0.44f,-0.2f), P(-0.44f,0.14f), P(-0.32f,0.32f), P(0.40f,0.32f), P(0.45f,0.17f), P(0.27f,-0.12f), P(-0.29f,-0.32f) };
        var dark = new Color(0.035f, 0.11f, 0.15f, 1);
        for (int i = 0; i < outline.Length; i++)
        {
            int n = vh.currentVertCount;
            vh.AddVert(P(0,0), dark, Vector2.zero); vh.AddVert(outline[i], dark, Vector2.zero); vh.AddVert(outline[(i+1)%outline.Length], dark, Vector2.zero); vh.AddTriangle(n,n+1,n+2);
            Line(vh, outline[i], outline[(i+1)%outline.Length], 2, color);
        }
        var bright = color; bright.a = 0.85f;
        Line(vh,P(-0.2f,0.19f),P(0.32f,0.19f),5,bright);
        Line(vh,P(-0.14f,-0.02f),P(0.27f,0.1f),3,bright);
        for(int i=0;i<12;i++)
        {
            float a=i*Mathf.PI/6, b=(i+1)*Mathf.PI/6;
            Line(vh,P(-0.3f+Mathf.Cos(a)*0.095f,Mathf.Sin(a)*0.18f),P(-0.3f+Mathf.Cos(b)*0.095f,Mathf.Sin(b)*0.18f),3,bright);
        }
        if (weaponType == FlipperWeaponType.Cannon)
            for (int i=0;i<3;i++) Line(vh,P(0.1f,0.04f+i*0.055f),P(0.4f,0.04f+i*0.055f),3,bright);
        else if(weaponType == FlipperWeaponType.Bomb)
            for(int i=0;i<6;i++) { float a=i*Mathf.PI/3; Line(vh,P(0.03f+Mathf.Cos(a)*0.06f,0.06f+Mathf.Sin(a)*0.1f),P(0.03f+Mathf.Cos(a)*0.12f,0.06f+Mathf.Sin(a)*0.2f),3,bright); }
        else
        {
            Line(vh,P(0.12f,0.12f),P(0.49f,0.12f),6,bright);
            Line(vh,P(0.12f,0.05f),P(0.49f,0.05f),2,bright);
        }
    }
    private static void Line(UnityEngine.UI.VertexHelper vh,Vector2 a,Vector2 b,float width,Color c)
    {
        Vector2 d=b-a, n=new Vector2(-d.y,d.x).normalized*width*0.5f;
        int start=vh.currentVertCount;
        vh.AddVert(a-n,c,Vector2.zero); vh.AddVert(a+n,c,Vector2.zero); vh.AddVert(b+n,c,Vector2.zero); vh.AddVert(b-n,c,Vector2.zero);
        vh.AddTriangle(start,start+1,start+2); vh.AddTriangle(start,start+2,start+3);
    }
}
