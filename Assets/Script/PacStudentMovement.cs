using UnityEngine;

// pac walks around a few fixed points (demo)
public class PacStudentMovement : MonoBehaviour
{
    // waypoints
    private Vector3[] wp = new Vector3[]
    {
        new Vector3(-0.5f, 6.5f, -1.5f), new Vector3(-5.5f, 6.5f, -1.5f),
        new Vector3(-5.5f, 10.5f, -1.5f),
        new Vector3(-0.5f, 10.5f, -1.5f)
    };

    public float moveSpeed = 3f;

    Animator anim;
    int idx = 0;
    float prog = 0f;
    Vector3 a, b;

    void Start()
    {
        anim = GetComponent<Animator>();
        if (wp == null || wp.Length < 2)
        {
            Debug.LogWarning("[PacStudentMovement] Needs at least two waypoints.", this);
            enabled = false;
            return;
        }
        transform.position = wp[0];
        idx = 1;
        Next();
    }

    // move to the current target, go to the next one when reached
    void Update()
    {
        if (wp == null || wp.Length < 2) return;
        float dist = Vector3.Distance(a, b);
        if (dist <= Mathf.Epsilon) prog = 1f;
        else prog += (Mathf.Max(moveSpeed, 0.01f) / dist) * Time.deltaTime;
        transform.position = Vector3.Lerp(a, b, prog);
        if (prog >= 1f)
        {
            transform.position = b;
            idx = (idx + 1) % wp.Length;
            Next();
        }
    }

    // set the next target and update the anim direction
    void Next()
    {
        a = transform.position;
        b = wp[idx];
        prog = 0f;
        if (anim == null) return;
        Vector3 dd = (b - a).normalized;
        // 0 up 1 right 2 down 3 left
        if (Mathf.Abs(dd.x) > Mathf.Abs(dd.y)) anim.SetInteger("Direction", dd.x > 0 ? 1 : 3);
        else anim.SetInteger("Direction", dd.y > 0 ? 0 : 2);
    }
}
