using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// 吃豆人控制：读键盘输入，按格子移动，处理撞墙、隧道、死亡和碰撞
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

    // 输入：inA = 最后按的方向，inB = 当前在走的方向
    GridDir inA = GridDir.None, inB = GridDir.None;

    // 移动状态
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
        if (L == null) { Debug.LogError("[PacStudent] No LevelGenerator in the scene. PacStudent is disabled.", this); enabled = false; return; }

        c0 = L.WorldToCell(transform.position);
        if (!L.IsWalkable(c0)) Debug.LogWarning($"[PacStudent] Start cell {c0} is not walkable. Check PacStudent's position in the scene.", this);
        ready = true;
        ResetToStart();
    }

    // 回到起点，朝右，等待输入
    public void ResetToStart()
    {
        StopAllCoroutines();
        IsDead = false;
        c = c0;
        if (L != null) transform.position = L.CellToWorld(c);
        moving = false;
        inA = GridDir.None; inB = GridDir.None; md = GridDir.None;
        bumped = false;

        if (animator != null) animator.SetBool("Dead", false);
        FacingDir = GridDir.Right;
        if (animator != null) animator.SetInteger("Dir", 3);
        Fx(false);
    }

    void Update()
    {
        if (IsDead || !ready || L == null) return;

        // 读取 WASD
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.wKey.wasPressedThisFrame) inA = GridDir.Up;
            else if (kb.aKey.wasPressedThisFrame) inA = GridDir.Left;
            else if (kb.sKey.wasPressedThisFrame) inA = GridDir.Down;
            else if (kb.dKey.wasPressedThisFrame) inA = GridDir.Right;
        }

        if (!ControlEnabled)
        {
            if (!moving) Fx(false);
            return;
        }

        // 正在移动就继续移动到下一格
        if (moving)
        {
            k +=dur > 0f ? Time.deltaTime / dur : 1f;
            transform.position = Vector3.Lerp(from, to, Mathf.Clamp01(k));
            if (k < 1f) return;
            transform.position = to;
            moving = false;
        }

        // 到格子了：先试最后按的方向，不行再试当前方向
        if (inA != GridDir.None && Step(inA)) { inB = inA; return; }
        if (inB != GridDir.None && Step(inB)) return;

        // 都走不了：撞墙，播粒子和音效
        if (md != GridDir.None && !bumped)
        {
            bumped = true;
            GridDir dd = inB != GridDir.None ? inB : md;
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

    // 尝试往某个方向走一格，是墙就返回 false
    private bool Step(GridDir d)
    {
        if (d == GridDir.None) return false;
        Vector2Int n = c + d.ToCellOffset();

        // 左右隧道：走出边界就传送到另一边
        if (!L.InBounds(n) && L.IsTunnelEnd(c))
        {
            var w = new Vector2Int(n.x < 0 ? L.Cols - 1 : 0, c.y);
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

        // 下一格有豆子就播吃豆音效
        bool hasP = false;
        foreach (var hit in Physics2D.OverlapPointAll(to))
            if (hit != null && hit.GetComponent<Pellet>() != null) { hasP = true; break; }
        Fx(true, hasP);
        return true;
    }

    // 移动时的动画、灰尘粒子和脚步音效
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
        if (!on) { if (moveSource.isPlaying) moveSource.Stop(); return; }

        AudioClip a = moveEmptyClip;
        if (eat && moveEatingClip != null) a = moveEatingClip;
        if (a == null) a = moveEatingClip;
        if (a == null) return;

        if (moveSource.clip != a) { moveSource.clip = a; moveSource.Play(); }
        else if (!moveSource.isPlaying) moveSource.Play();
    }

    void Snd(AudioClip a)
    {
        if (a == null) return;
        AudioSource s = sfxSource != null ? sfxSource : moveSource;
        if (s != null) s.PlayOneShot(a);
    }

    // 游戏结束时停住
    public void Freeze()
    {
        ControlEnabled = false;
        moving = false;
        Fx(false);
    }

    // 死亡：播动画、粒子和音效，结束后回调
    public void Die(System.Action onFinished)
    {
        if (!IsDead)
        {
            IsDead = true;
            ControlEnabled = false;
            moving = false;
            Fx(false);
            if (animator != null) { animator.speed = 1f; animator.SetBool("Dead", true); }
            if (deathParticles != null) { deathParticles.transform.position = transform.position; deathParticles.Play(); }
            Snd(deathClip);
        }
        StartCoroutine(W(onFinished));
    }

    IEnumerator W(System.Action cb)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, deathDuration));
        if (cb != null) cb();
    }

    // 碰到豆子 / 樱桃 / 鬼
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null) return;
        if (GameManager.Instance == null || IsDead || !GameManager.Instance.IsPlaying) return;

        if (other.GetComponent<Pellet>() != null) { GameManager.Instance.EatPellet(other.GetComponent<Pellet>()); return; }
        if (other.GetComponent<Cherry>() != null) { GameManager.Instance.EatCherry(other.GetComponent<Cherry>()); return; }
        var g = other.GetComponentInParent<GhostController>();
        if (g != null) GameManager.Instance.OnGhostTouched(g);
    }
}
