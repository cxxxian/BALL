#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Opt-in verification, attached only for an Editor test run.
public sealed class TAShowcaseSmokeChecks : MonoBehaviour
{
    private readonly List<string> _results = new List<string>();
    private IEnumerator Start()
    {
        yield return null;
        var c = FindObjectOfType<TAShowcaseController>();
        for (int exhibit = 0; exhibit < 7; exhibit++)
        {
            c.exhibitButtons[exhibit].onClick.Invoke();
            for (int level = 0; level < 4; level++)
            {
                c.levelButtons[level].onClick.Invoke();
                yield return null;
                var electric = c.hero.GetComponent<EnemyElectricState>();
                var frost = c.hero.GetComponent<EnemyFrostState>();
                Check(c.Exhibit == exhibit && c.Level == level, "tab and level buttons " + exhibit + "/" + level);
                Check(electric.ChargeStacks == (exhibit == 0 ? level : 0), "electric state " + exhibit + "/" + level);
                Check(frost.MarkStacks == (exhibit == 1 ? level : 0), "frost state " + exhibit + "/" + level);
                Check(c.hero.MainSR.sharedMaterial.shader.name == (exhibit == 4 ? "Custom/SpriteNeonHDR" : "Custom/EnemyBuildStack"), "production shader " + exhibit + "/" + level);
            }
            c.triggerButton.onClick.Invoke();
            yield return new WaitForSecondsRealtime(.25f);
        }
        c.SelectExhibit(1); c.SetLevel(3); c.freezeButton.onClick.Invoke();
        Check(c.hero.GetComponent<EnemyFrostState>().IsFrozen, "freeze button uses production short-freeze state");
        c.scaleSlider.value = 3f; Check(Mathf.Abs(c.hero.MainSR.bounds.size.x - 2.7f) < .01f, "3x closeup");
        c.scaleSlider.value = 6f; Check(Mathf.Abs(c.hero.MainSR.bounds.size.x - 5.4f) < .01f, "6x closeup");
        c.scaleSlider.value = 5f;
        Check(c.presentationCamera.backgroundColor == Color.black, "fixed black background");
        c.bloomButton.onClick.Invoke(); c.bloomButton.onClick.Invoke();
        c.slowButton.onClick.Invoke(); Check(Mathf.Abs(Time.timeScale - .25f) < .01f, "quarter-speed playback");
        c.slowButton.onClick.Invoke(); Check(Mathf.Abs(Time.timeScale - 1f) < .01f, "normal-speed playback");
        c.cleanButton.onClick.Invoke(); Check(c.interfaceGroup.alpha == 0f && !c.interfaceGroup.blocksRaycasts, "clean capture hides interface");
        c.cleanButton.onClick.Invoke(); Check(c.interfaceGroup.alpha == 1f && c.interfaceGroup.interactable, "interface restores");
        c.SelectExhibit(2); c.SetLevel(3); yield return null;
        Check(c.rainStage.background.ShowcaseReady && c.rainStage.Interactive,"production rain and fluid ready");
        Check(!c.hero.gameObject.activeSelf && !c.specimens[0].gameObject.activeSelf,"environment hides enemy specimens");
        Check(c.rainStage.background.ShowcaseMaterial.GetColor("_BgColor") == Color.black,"rain base is black");
        c.cycleButton.onClick.Invoke();
        Check(c.rainStage.DemoPlaying,"fluid auto trajectory starts");
        yield return new WaitForSecondsRealtime(.7f);
        Check(MeasureWake(c.rainStage.background.ShowcaseWake) > .00001f,"auto trajectory changes GPU displacement field");
        c.cycleButton.onClick.Invoke(); c.freezeButton.onClick.Invoke();
        Check(!c.rainStage.DemoPlaying && MeasureWake(c.rainStage.background.ShowcaseWake) < .00001f,"reset clears GPU field");
        c.SelectExhibit(5);c.Trigger();
        var block = new MaterialPropertyBlock();c.extraStage.Wall.FrameRenderer.GetPropertyBlock(block);
        Check(block.GetVector("_WallPulse0").y >= 0,"production wall pulse reaches renderer");
        c.SelectExhibit(6); c.Trigger();
        Check(c.extraStage.Shield.ShieldVisible && c.extraStage.Shield.ActiveRipples > 0,"production shield responds to hit");
        c.Freeze();Check(c.extraStage.Shield.IsPlaying,"shield reveal starts");
        c.extraStage.ResetStudy();Check(!c.extraStage.Shield.IsPlaying && c.extraStage.Shield.ShieldVisible,"shield restores after reveal reset");
        c.SelectExhibit(0); c.SetLevel(3);
        Check(c.hero.gameObject.activeSelf && c.specimens[0].gameObject.activeSelf, "returning restores gameplay specimens");
        System.IO.File.WriteAllLines("Temp/TAPortfolioSmokeResult.txt", _results);
        Debug.Log("[TAPortfolioSmoke] PASSED " + _results.Count + " checks");
        Destroy(this);
    }
    private static float MeasureWake(RenderTexture target)
    {
        var previous = RenderTexture.active;
        var texture = new Texture2D(target.width,target.height,TextureFormat.RGBAFloat,false);
        try {
            RenderTexture.active = target;texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();
            float sum = 0;foreach(var pixel in texture.GetPixels())sum += Mathf.Abs(pixel.r)+Mathf.Abs(pixel.g);
            return sum;
        } finally { RenderTexture.active = previous;Destroy(texture); }
    }
    private void Check(bool passed, string label)
    {
        _results.Add((passed ? "PASS " : "FAIL ") + label);
        if (!passed)
        {
            System.IO.File.WriteAllLines("Temp/TAPortfolioSmokeResult.txt", _results);
            throw new InvalidOperationException(label);
        }
    }
}
#endif

