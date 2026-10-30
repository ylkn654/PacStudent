using UnityEngine;

// camera fit: zoom to the screen ratio so the whole map is visible
[RequireComponent(typeof(Camera))]
public class CameraFit : MonoBehaviour
{
    [Tooltip("Extra world units above and below the level")]
    public float verticalMargin = 0.5f;
    [Tooltip("World units kept free on each side of the level for the HUD")]
    public float sideSpace = 6.5f;

    private Camera c;
    float a_old;

    private void Awake() { c = GetComponent<Camera>(); }

    // only recalc when the aspect changes
    void LateUpdate()
    {
        if (Mathf.Approximately(c.aspect, a_old)) return;
        a_old = c.aspect;
        Fit();
    }

    public void Fit()
    {
        LevelGenerator lg = LevelGenerator.Instance;
        if (lg == null) lg = FindAnyObjectByType<LevelGenerator>();
        if (lg == null) { return; }
        if (c == null) c = GetComponent<Camera>();
        if (c == null) return;
        if (c.aspect <= 0f) return; // TODO??

        // work out by height and by width, use the bigger one
        var v = lg.MapSize;
        float tmp1 = v.y * 0.5f + verticalMargin, tmp2 = (v.x * 0.5f + sideSpace) / c.aspect;
        c.orthographicSize = tmp1 > tmp2 ? tmp1 : tmp2;
        transform.position = new Vector3(lg.MapCenter.x, lg.MapCenter.y, transform.position.z);
    }
}
