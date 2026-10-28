using UnityEngine;
using UnityEngine.UI;

// UI 图片帧动画（开始界面的角色），可以上下浮动
[RequireComponent(typeof(Image))]
public class UISpriteAnimator : MonoBehaviour
{
    public Sprite[] frames;
    public float framesPerSecond = 10f;
    [Tooltip("Gentle up/down bob in pixels (0 = off)")]
    public float bobHeight = 0f;
    public float bobSpeed = 3f;

    Image img; RectTransform rt; Vector2 p0;

    void Awake()
    {
        img = GetComponent<Image>();
        rt = (RectTransform)transform;
        p0 = rt.anchoredPosition;
    }

    // 切换帧 + 浮动
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
