using UnityEngine;

/// <summary>
/// Zooms the orthographic camera so the whole level fits on screen in any aspect ratio
/// (16:9, 4:3, Free Aspect), and centres the camera on it.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraFit : MonoBehaviour
{
    [Tooltip("The level to fit: the LevelManager (generated map). Falls back to the hand-made map if empty")]
    public Transform level;
    [Tooltip("Extra world units kept around the level")]
    public float margin = 0.5f;

    private Camera cam;
    private float lastAspect;
    private Bounds lastBounds;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;
    }

    private void LateUpdate()
    {
        // The generated map appears after Start, and the window can be resized, so keep checking
        if (!TryGetLevelBounds(out Bounds b)) return;
        if (Mathf.Approximately(cam.aspect, lastAspect) && b == lastBounds) return;
        lastAspect = cam.aspect;
        lastBounds = b;
        Fit(b);
    }

    private void Fit(Bounds b)
    {
        if (cam.aspect <= 0f) return;   // window minimised

        // Big enough for the level height and for the level width at this aspect ratio
        float byHeight = b.extents.y + margin;
        float byWidth = (b.extents.x + margin) / cam.aspect;
        cam.orthographicSize = Mathf.Max(byHeight, byWidth);
        transform.position = new Vector3(b.center.x, b.center.y, transform.position.z);
    }

    private bool TryGetLevelBounds(out Bounds bounds)
    {
        bounds = default;
        Transform root = level;
        if (root == null || root.GetComponentInChildren<SpriteRenderer>() == null)
        {
            GameObject manual = GameObject.Find("ManualMap");
            if (manual != null) root = manual.transform;
        }
        if (root == null) return false;

        bool found = false;
        foreach (var r in root.GetComponentsInChildren<Renderer>())
        {
            // Map tiles only: sprites and tilemaps (the hand-made map uses both)
            if (!(r is SpriteRenderer) && !(r is UnityEngine.Tilemaps.TilemapRenderer)) continue;
            if (!found) { bounds = r.bounds; found = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return found;
    }
}
