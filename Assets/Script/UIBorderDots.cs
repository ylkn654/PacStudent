using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 开始界面的边框动画：一圈沿边缘移动的小点
[RequireComponent(typeof(RectTransform))]
public class UIBorderDots : MonoBehaviour
{
    public int dotCount = 72;
    public float dotSize = 18f;
    [Tooltip("Laps around the border per second")]
    public float speed = 0.04f;
    public Color colorA = new Color(1f, 0.85f, 0.2f);
    public Color colorB = new Color(0.45f, 0.95f, 0.45f);
    [Tooltip("Every Nth dot is a big 'power' dot that pulses")]
    public int bigEvery = 9;
    public Sprite dotSprite;

    private List<RectTransform> list = new List<RectTransform>();
    RectTransform r;
    float o;

    void Start()
    {
        r = (RectTransform)transform;
        // 创建所有小点
        for (int i = 0; i < dotCount; i++)
        {
            var g = new GameObject("Dot" + i, typeof(RectTransform), typeof(Image));
            g.transform.SetParent(transform, false);
            g.GetComponent<Image>().sprite = dotSprite;
            g.GetComponent<Image>().raycastTarget = false;
            if (i % 2 == 0) g.GetComponent<Image>().color = colorA; else g.GetComponent<Image>().color = colorB;
            RectTransform t = (RectTransform)g.transform;
            t.anchorMin = new Vector2(0.5f, 0.5f); t.anchorMax = new Vector2(0.5f, 0.5f);
            t.sizeDelta = Vector2.one * dotSize;
            list.Add(t);
        }
    }

    // 每帧移动小点，大点会一闪一闪
    void Update()
    {
        o = (o + speed * Time.unscaledDeltaTime) % 1f;
        float w = r.rect.width;
        float h = r.rect.height;
        float p = 1f + 0.35f * Mathf.Sin(Time.unscaledTime * 6f);

        for (int i = 0; i < list.Count; i++)
        {
            list[i].anchoredPosition = Calc((i / (float)list.Count + o) % 1f, w, h);
            if (bigEvery > 0 && i % bigEvery == 0) list[i].sizeDelta = Vector2.one * (dotSize * 1.8f * p);
            else list[i].sizeDelta = Vector2.one * dotSize;
        }
    }

    // 根据进度 t 算出边框上的位置（从左上角顺时针）
    static Vector2 Calc(float t, float w, float h)
    {
        float d = t * (2f * (w + h));
        if (d < w) return new Vector2(-w * 0.5f + d, h * 0.5f);
        d = d - w;
        if (d < h) return new Vector2(w * 0.5f, h * 0.5f - d);
        d -= h;
        if (d < w)
            return new Vector2(w * 0.5f - d, -h * 0.5f);
        d -= w;
        return new Vector2(-w * 0.5f, -h * 0.5f + d);
    }
}
