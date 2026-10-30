using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// pac controller: reads keyboard, moves cell by cell, handles walls, tunnels, death and collisions
public class PacStudentController : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Cells per second")]
    public float speed = 5f;

    [Header("Animation (one animator: Up / Down / Left / Right / Dead)")]
    public Animator animator;

    [Header("Audio")]
    [Tooltip("Looping source used for the two movement sounds")]
    public AudioSource moveSource;
    [Tooltip("Moving into a cell with a pellet")]
    public AudioClip moveEatingClip;
    [Tooltip("Moving into an empty cell")]
    public AudioClip moveEmptyClip;
    public AudioSource sfxSource;
    public AudioClip wallBumpClip;
    public AudioClip deathClip;

    [Header("Particles")]
    public ParticleSystem dustParticles;
    public ParticleSystem wallHitParticles;
    public ParticleSystem deathParticles;

    [Tooltip("How long the death animation / sound plays before respawning")]
    public float deathDuration = 2f;

    // input: lastInput = last key pressed, currentInput = dir currently moving
    GridDir lastInput = GridDir.None, currentInput = GridDir.None;

    // movement state
    bool moving;
    Vector3 from, to;
    float k, dur;
    GridDir md = GridDir.None;

    Vector2Int c;
    Vector2Int c0;
    bool bumped;
    bool ready;

    public bool ControlEnabled { get; set; }
    public bool IsDead { get; private set; }
    public Vector2Int Cell => c;
    public GridDir FacingDir { get; private set; } = GridDir.Right;

    private LevelGenerator lv;
    LevelGenerator L
    {
        get
        {
            if (lv == null)
            {
                lv = LevelGenerator.Instance;
                if (lv == null) lv = FindAnyObjectByType<LevelGenerator>();
            }
            return lv;
        }
    }

    void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator == null) Debug.LogWarning("[PacStudent] No Animator found: movement works but will not animate.", this);
    }

    void Start()
    {
        if (L == null)
        {
            Debug.LogError("[PacStudent] No LevelGenerator in the scene. PacStudent is disabled.", this);
            enabled = false;
            return;
        }

        c0 = L.WorldToCell(transform.position);
        if (!L.IsWalkable(c0)) Debug.LogWarning($"[PacStudent] Start cell {c0} is not walkable. Check PacStudent's position in the scene.", this);
        ready = true;
        ResetToStart();
    }

    // back to start, facing right, waiting for input
    public void ResetToStart()
    {
        StopAllCoroutines();
        IsDead = false;
        c = c0;
        if (L != null) transform.position = L.CellToWorld(c);
        moving = false;
        lastInput = GridDir.None;
        currentInput = GridDir.None;
        md = GridDir.None;
        bumped = false;

        if (animator != null) animator.SetBool("Dead", false);
        FacingDir = GridDir.Right;
        if (animator != null) animator.SetInteger("Dir", (int)GridDir.Right);
        Fx(false);
    }

    void Update()
    {
        if (IsDead || !ready || L == null) return;

        // read WASD
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.wKey.wasPressedThisFrame) lastInput = GridDir.Up;
            else if (kb.aKey.wasPressedThisFrame) lastInput = GridDir.Left;
            else if (kb.sKey.wasPressedThisFrame) lastInput = GridDir.Down;
            else if (kb.dKey.wasPressedThisFrame) lastInput = GridDir.Right;
        }

        if (!ControlEnabled)
        {
            if (!moving) Fx(false);
            return;
        }

        // if moving keep moving to the next cell
        if (moving)
        {
            k +=dur > 0f ? Time.deltaTime / dur : 1f;
            transform.position = Vector3.Lerp(from, to, Mathf.Clamp01(k));
            if (k < 1f) return;
            transform.position = to;
            moving = false;
        }

        // reached a cell: try lastInput first, then currentInput
        if (lastInput != GridDir.None && Step(lastInput))
        {
            currentInput = lastInput;
            return;
        }
        if (currentInput != GridDir.None && Step(currentInput)) return;

        // cant move either way: hit wall, play particles and sound
        if (md != GridDir.None && !bumped)
        {
            bumped = true;
            GridDir dd = currentInput != GridDir.None ? currentInput : md;
            if (wallHitParticles != null)
            {
                wallHitParticles.transform.position = transform.position + dd.ToWorld() * 0.5f;
                wallHitParticles.Play();
            }
            Snd(wallBumpClip);
        }
        md = GridDir.None;
        Fx(false);
    }

    // try to step one cell in this dir, false if its a wall
    private bool Step(GridDir d)
    {
        if (d == GridDir.None) return false;
        Vector2Int n = c + d.ToCellOffset();

        // tunnel: step off the edge and come out the other side (left/right, or top/bottom on rotated maps)
        if (!L.InBounds(n) && L.IsTunnelEnd(c))
        {
            var w = new Vector2Int((n.x % L.Cols + L.Cols) % L.Cols, (n.y % L.Rows + L.Rows) % L.Rows);
            if (!L.IsWalkable(w)) return false;
            c = w;
            transform.position = L.CellToWorld(c);
            n = c + d.ToCellOffset();
        }

        if (L.IsWalkable(n) == false) return false;

        from = transform.position;
        to = L.CellToWorld(n);
        k = 0f;
        dur = 1f / Mathf.Max(speed, 0.01f);
        c = n;
        moving = true;
        md = d;
        bumped = false;

        FacingDir = d;
        if (animator != null) animator.SetInteger("Dir", (int)d);

        // pellet in the next cell -> eating sound
        bool hasP = false;
        foreach (var hit in Physics2D.OverlapPointAll(to))
            if (hit != null && hit.GetComponent<Pellet>() != null)
            {
                hasP = true;
                break;
            }
        Fx(true, hasP);
        return true;
    }

    // walk anim, dust particles and footstep sound
    void Fx(bool on, bool eat = false)
    {
        if (animator != null) animator.speed = (on || IsDead) ? 1f : 0f;

        if (dustParticles != null)
        {
            var em = dustParticles.emission;
            em.enabled = on;
            if (on && !dustParticles.isPlaying) dustParticles.Play();
        }

        if (moveSource == null) return;
        if (!on)
        {
            if (moveSource.isPlaying) moveSource.Stop();
            return;
        }

        AudioClip a = moveEmptyClip;
        if (eat && moveEatingClip != null) a = moveEatingClip;
        if (a == null) a = moveEatingClip;
        if (a == null) return;

        if (moveSource.clip != a)
        {
            moveSource.clip = a;
            moveSource.Play();
        }
        else if (!moveSource.isPlaying) moveSource.Play();
    }

    void Snd(AudioClip a)
    {
        if (a == null) return;
        AudioSource s = sfxSource != null ? sfxSource : moveSource;
        if (s != null) s.PlayOneShot(a);
    }

    // stop on game over
    public void Freeze()
    {
        ControlEnabled = false;
        moving = false;
        Fx(false);
    }

    // die: anim, particles and sound, then callback
    public void Die(System.Action onFinished)
    {
        if (!IsDead)
        {
            IsDead = true;
            ControlEnabled = false;
            moving = false;
            Fx(false);
            if (animator != null)
            {
                animator.speed = 1f;
                animator.SetBool("Dead", true);
            }
            if (deathParticles != null)
            {
                deathParticles.transform.position = transform.position;
                deathParticles.Play();
            }
            Snd(deathClip);
        }
        StartCoroutine(W(onFinished));
    }

    IEnumerator W(System.Action cb)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, deathDuration));
        if (cb != null) cb();
    }

    // hit pellet / cherry / ghost
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null) return;
        if (GameManager.Instance == null || IsDead || !GameManager.Instance.IsPlaying) return;

        if (other.GetComponent<Pellet>() != null)
        {
            GameManager.Instance.EatPellet(other.GetComponent<Pellet>());
            return;
        }
        if (other.GetComponent<Cherry>() != null)
        {
            GameManager.Instance.EatCherry(other.GetComponent<Cherry>());
            return;
        }
        var g = other.GetComponentInParent<GhostController>();
        if (g != null) GameManager.Instance.OnGhostTouched(g);
    }
}
