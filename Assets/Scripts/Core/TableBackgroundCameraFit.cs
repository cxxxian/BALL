using UnityEngine;

/// <summary>
/// Keeps the integrated portrait table art fully inside an orthographic camera
/// across Android aspect ratios. This only frames the board; it does not move
/// or resize gameplay objects or colliders.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public sealed class TableBackgroundCameraFit : MonoBehaviour
{
    [SerializeField] private Vector2 boardWorldSize = new Vector2(10.83f, 19.25f);
    [SerializeField, Min(0f)] private float padding = 0.15f;

    private Camera _camera;
    private float _lastAspect = -1f;

    private void Awake()
    {
        CacheCamera();
        ApplyFit();
    }

    private void OnEnable()
    {
        CacheCamera();
        ApplyFit();
    }

    private void OnValidate()
    {
        CacheCamera();
        ApplyFit();
    }

    private void LateUpdate()
    {
        if (_camera == null)
            CacheCamera();

        if (_camera != null && !Mathf.Approximately(_camera.aspect, _lastAspect))
            ApplyFit();
    }

    private void CacheCamera()
    {
        if (_camera == null)
            _camera = GetComponent<Camera>();
    }

    private void ApplyFit()
    {
        if (_camera == null || !_camera.orthographic || boardWorldSize.x <= 0f || boardWorldSize.y <= 0f)
            return;

        float aspect = Mathf.Max(0.01f, _camera.aspect);
        float halfHeight = boardWorldSize.y * 0.5f + padding;
        float halfWidthAsHeight = (boardWorldSize.x * 0.5f + padding) / aspect;
        _camera.orthographicSize = Mathf.Max(halfHeight, halfWidthAsHeight);
        _lastAspect = aspect;
    }
}
