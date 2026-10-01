using System;
using UnityEngine;

public enum LoadoutReturnTarget { MainMenu }

/// <summary>Loadout commands and navigation; presentation is an authored uGUI prefab.</summary>
public class LoadoutPanelController : MonoBehaviour
{
    public static LoadoutPanelController Instance { get; private set; }
    public event Action LoadoutChanged;
    [SerializeField] private LoadoutCanvasView canvasView;
    private LoadoutReturnTarget returnTarget;
    private void Awake() => Instance = this;
    private void OnDestroy() { if (Instance == this) Instance = null; }
    public void Show(LoadoutReturnTarget target)
    {
        returnTarget = target;
        PlayerProfile.Load(); RunLoadout.Load();
        var catalog = RunCatalog.Load();
        if (catalog != null) RunLoadout.EnsureDefaults(catalog);
        canvasView?.Show();
    }
    public void Hide()
    {
        HideForNavigation();
        MainMenuController.Instance?.OnLoadoutClosed(returnTarget);
    }
    public void HideForNavigation() => canvasView?.Hide();
    public bool EquipBall(string id)
    {
        if (!RunLoadout.TrySelectBall(id, RunCatalog.Load())) return false;
        canvasView?.RefreshPresentation(); LoadoutChanged?.Invoke(); return true;
    }
    public bool EquipWeapon(string id)
    {
        if (!RunLoadout.TrySelectFlipperWeapon(id)) return false;
        canvasView?.RefreshPresentation(); LoadoutChanged?.Invoke(); return true;
    }
}
