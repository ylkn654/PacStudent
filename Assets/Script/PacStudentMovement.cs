using UnityEngine;

public class PacStudentMovement : MonoBehaviour
{
    private Vector3[] waypoints = new Vector3[]
    {
        new Vector3(-0.5f, 6.5f, -1.5f),
        new Vector3(-5.5f, 6.5f, -1.5f),
        new Vector3(-5.5f, 10.5f, -1.5f),
        new Vector3(-0.5f, 10.5f, -1.5f)
    };

    public float moveSpeed = 3f;

    [Header("Audio")]
    [Tooltip("Plays the movement sound on loop while PacStudent is moving")]
    public AudioSource moveAudio;
    public AudioClip moveClip;

    private Animator animator;
    private int currentTargetIndex = 0;
    private float moveProgress = 0f;
    
    private Vector3 startPos;
    private Vector3 targetPos;

    void Start()
    {
        animator = GetComponent<Animator>();

        transform.position = waypoints[0];
        currentTargetIndex = 1;
        SetNewTarget();

        // PacStudent never stops in this scene, so the movement sound loops the whole time
        if (moveAudio == null) moveAudio = GetComponent<AudioSource>();
        if (moveAudio != null)
        {
            if (moveClip != null) moveAudio.clip = moveClip;
            moveAudio.loop = true;
            if (moveAudio.clip != null) moveAudio.Play();
        }
    }

    void Update()
    {
        float distance = Vector3.Distance(startPos, targetPos);


        moveProgress += (moveSpeed / distance) * Time.deltaTime;
        
        transform.position = Vector3.Lerp(startPos, targetPos, moveProgress);
        
        if (moveProgress >= 1f)
        {
            transform.position = targetPos;
            
            currentTargetIndex = (currentTargetIndex + 1) % waypoints.Length;
            SetNewTarget();
        }
    }

    private void SetNewTarget()
    {
        startPos = transform.position;
        targetPos = waypoints[currentTargetIndex];
        moveProgress = 0f;
        
        UpdateAnimationDirection();
    }

    private void UpdateAnimationDirection()
    {
        if (animator == null) return;
        Vector3 direction = (targetPos - startPos).normalized;
        
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
            if (direction.x > 0) animator.SetInteger("Direction", 1);
            else animator.SetInteger("Direction", 3);
        }
        else
        {
            if (direction.y > 0) animator.SetInteger("Direction", 0);
            else animator.SetInteger("Direction", 2);
        }
    }
}