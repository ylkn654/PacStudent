using UnityEngine;
using UnityEngine.UI;

// UI sprite frame anim (start screen characters), can bob up and down
[RequireComponent(typeof(Image))]
public class UISpriteAnimator : MonoBehaviour
{
    public Sprite[] frames;
    public float framesPerSecond = 10f;
    [Tooltip("Gentle up/down bob in pixels (0 = off)")]
    public float bobHeight = 0f;
    public float bobSpeed = 3f;

    Image img;
    RectTransform rt;
    Vector2 p0;

    void Awake()
    {
        img = GetComponent<Image>();
        rt = (RectTransform)transform;
        p0 = rt.anchoredPosition;
    }

    // switch frame + bob
    void Update()
    {
        if (img != null && frames != null && frames.Length > 0 && framesPerSecond > 0f)
        {
            int idx = Mathf.FloorToInt(Time.unscaledTime * framesPerSecond) % frames.Length;
            if (frames[idx] != null) img.sprite = frames[idx];
        }
        if (bobHeight > 0f) rt.anchoredPosition = p0 + Vector2.up * Mathf.Sin(Time.unscaledTime * bobSpeed + p0.x * 0.01f) * bobHeight;
    }
}
