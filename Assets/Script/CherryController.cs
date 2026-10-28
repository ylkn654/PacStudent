using System.Collections;
using UnityEngine;

// 樱桃生成器：每隔几秒在屏幕外生成一个樱桃，让它穿过地图中心
public class CherryController : MonoBehaviour
{
    public Sprite cherrySprite;
    public float spawnDelay = 5f;
    [Tooltip("World units per second")]
    public float speed = 3.5f;
    public float scale = 1f;
    public int sortingOrder = 50;
    public Camera cam;

    private Cherry cur;

    void Start()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) cam = FindAnyObjectByType<Camera>();
        if (cherrySprite == null) Debug.LogWarning("[CherryController] No cherry sprite assigned: the cherry will be invisible.", this);
        StartCoroutine(Loop());
    }

    // 循环：等待 -> 生成 -> 等上一个消失
    IEnumerator Loop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Mathf.Max(0.1f, spawnDelay));
            bool ok = DoIt();
            if (ok == false) continue;
            while (cur != null) yield return null;
        }
    }

    // 生成一个樱桃，失败返回 false
    private bool DoIt()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null || LevelGenerator.Instance == null) return false;
        Vector3 c = LevelGenerator.Instance.MapCenter;

        // 计算屏幕外的随机起点，终点在对面
        float hh = 10f;
        if (cam.orthographic) hh = cam.orthographicSize;
        float hw = hh * cam.aspect;
        float r = Mathf.Sqrt(hw * hw + hh * hh) + 1.5f;
        float ang = Random.Range(0f, Mathf.PI * 2f);
        Vector3 s = new Vector3(cam.transform.position.x, cam.transform.position.y, c.z) + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * r;
        Vector3 e = c + (c - s);

        // 创建樱桃物体（图片、碰撞体、刚体）
        GameObject obj = new GameObject("BonusCherry");
        obj.transform.position = s;
        obj.transform.localScale = Vector3.one * (scale > 0f ? scale : 1f);

        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = cherrySprite; sr.sortingOrder = sortingOrder;
        sr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

        var cc = obj.AddComponent<CircleCollider2D>();
        cc.isTrigger = true; cc.radius = 0.35f;
        obj.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;

        cur = obj.AddComponent<Cherry>();
        cur.Launch(s, e, speed > 0f ? speed : 3.5f);
        return true;
    }
}
