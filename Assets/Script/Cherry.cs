using UnityEngine;

// 奖励樱桃：从起点直线移动到终点，到达后销毁
public class Cherry : MonoBehaviour
{
    [Tooltip("Fail-safe: the cherry is always removed after this many seconds")]
    public float maxLifetime = 30f;

    Vector3 p1, p2;
    private float x, d;
    float timer;

    // 设置起点、终点和速度
    public void Launch(Vector3 start, Vector3 end, float speed)
    {
        p1 = start; p2 = end;
        if (speed > 0f) d = Vector3.Distance(start, end) / speed;
        else d = 0f;
        x = 0;
        transform.position = start;
    }

    // 移动樱桃，超时或到达终点就销毁
    void Update()
    {
        timer = timer + Time.deltaTime;
        if (d <= 0f) { Destroy(gameObject); return; }
        if (timer > Mathf.Max(maxLifetime, d * 2f)) { Destroy(gameObject); return; }

        x += Time.deltaTime / d;
        transform.position = Vector3.Lerp(p1, p2, x);
        if (x >= 1f)
            Destroy(gameObject);
    }

    // 被吃掉
    public void Eaten()
    {
        if (GetComponent<Collider2D>() != null) GetComponent<Collider2D>().enabled = false;
        Destroy(gameObject);
    }
}
