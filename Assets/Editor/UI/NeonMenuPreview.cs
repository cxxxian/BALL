using UnityEngine;

/// <summary>Renders a temporary copy. Does not save or alter the scene's layout.</summary>
public static class NeonMenuPreview
{
    public static string Capture(bool loadout)
    {
        MenuCanvasPanel source = loadout ? (MenuCanvasPanel)Object.FindObjectOfType<LoadoutCanvasView>(true) : Object.FindObjectOfType<MainMenuCanvasView>(true);
        if(source == null) throw new System.InvalidOperationException("Create the menu prefabs first.");
        var clone = Object.Instantiate(source.gameObject);
        var go = new GameObject("MenuPreviewCamera");
        var rt = new RenderTexture(1080,1920,24);
        Texture2D texture = null;
        var previous = RenderTexture.active;
        try
        {
            clone.GetComponent<Canvas>().enabled=true;
            clone.GetComponent<MenuCanvasPanel>().RefreshPresentation();
            var group=clone.GetComponent<CanvasGroup>(); group.alpha=1;
            var camera=go.AddComponent<Camera>(); camera.orthographic=true; camera.orthographicSize=5; camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.black; camera.transform.position=new Vector3(0,0,-10);
            rt.Create(); camera.targetTexture=rt;
            var canvas=clone.GetComponent<Canvas>(); canvas.enabled=true; canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=1;
            foreach(var scroll in clone.GetComponentsInChildren<UnityEngine.UI.ScrollRect>())
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active=rt;
            texture=new Texture2D(1080,1920,TextureFormat.RGBA32,false); texture.ReadPixels(new Rect(0,0,1080,1920),0,0); texture.Apply();
            System.IO.Directory.CreateDirectory("Assets/Screenshots");
            string path="Assets/Screenshots/"+(loadout?"loadout_canvas_final.png":"main_menu_canvas_final.png");
            System.IO.File.WriteAllBytes(path,texture.EncodeToPNG()); return path;
        }
        finally
        {
            RenderTexture.active=previous; rt.Release();
            if(texture!=null) Object.DestroyImmediate(texture);
            Object.DestroyImmediate(go); Object.DestroyImmediate(clone); Object.DestroyImmediate(rt);
        }
    }
}
