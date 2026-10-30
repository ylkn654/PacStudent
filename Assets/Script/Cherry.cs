using UnityEngine;

// bonus cherry: moves in a straight line from start to end, then destroyed
public class Cherry : MonoBehaviour
{
    [Tooltip("Fail-safe: the cherry is always removed after this many seconds")]
    public float maxLifetime = 30f;

    Vector3 p1, p2;
    private float x, d;
    float timer;

    // set start, end and speed
    public void Launch(Vector3 start, Vector3 end, float speed)
    {
        p1 = start;
        p2 = end;
        if (speed > 0f) d = Vector3.Distance(start, end) / speed;
        else d = 0f;
        x = 0;
        transform.position = start;
    }

    // move the cherry, destroy on timeout or when it reaches the end
    void Update()
    {
        timer = timer + Time.deltaTime;
        if (d <= 0f)
        {
            Destroy(gameObject);
            return;
        }
        if (timer > Mathf.Max(maxLifetime, d * 2f))
        {
            Destroy(gameObject);
            return;
        }

        x += Time.deltaTime / d;
        transform.position = Vector3.Lerp(p1, p2, x);
        if (x >= 1f)
            Destroy(gameObject);
    }

    // eaten
    public void Eaten()
    {
        if (GetComponent<Collider2D>() != null) GetComponent<Collider2D>().enabled = false;
        Destroy(gameObject);
    }
}
