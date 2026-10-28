using System.Collections.Generic;
using UnityEngine;

public enum GhostState { Normal = 0, Scared = 1, Recovering = 2, Dead = 3 }

// 鬼的移动和 AI：每走完一格按编号（1-4）选下一个方向
public class GhostController : MonoBehaviour
{
    [Tooltip("1 = runs away, 2 = chases, 3 = random, 4 = circles the outer wall clockwise")]
    [Range(1, 4)] public int ghostNumber = 1;
    public Animator animator;

    [Header("Speed (cells per second)")]
    [Tooltip("Normal speed. Default = 90% of PacStudent's 5 cells/s")]
    public float normalSpeed = 4.5f;
    [Tooltip("Scared, Recovering and Dead speed, as a fraction of the normal speed")]
    public float slowFactor = 0.5f;

    Vector2Int sc; Vector3 sp;
    Vector2Int c, pc;
    GridDir md = GridDir.None;

    bool lerp;
    Vector3 f, to;
    float t, dist;

    int ct = -1;
    int cs;
    bool init;

    public GhostState State { get; private set; } = GhostState.Normal;

    LevelGenerator _lv;
    LevelGenerator lvl
    {
        get
        {
            if (_lv == null) _lv = LevelGenerator.Instance != null ? LevelGenerator.Instance : FindAnyObjectByType<LevelGenerator>();
            return _lv;
        }
    }

    void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator == null) Debug.LogWarning($"[Ghost {ghostNumber}] No Animator found: it will move but not animate.", this);
    }

    void Start()
    {
        if (lvl == null)
        {
            Debug.LogError($"[Ghost {ghostNumber}] No LevelGenerator in the scene. Ghost is disabled.", this);
            enabled = false;
            return;
        }
        sp = transform.position;
        sc = lvl.WorldToCell(sp);
        init = true;
        ResetToStart();
    }

    // 回到鬼屋起点，普通状态
    public void ResetToStart()
    {
        if (!init) return;
        transform.position = sp;
        c = sc; pc = sc;
        lerp = false;
        md = GridDir.Down;
        ct = -1; cs = 0;
        SetState(GhostState.Normal);
        if (animator != null) animator.SetInteger("Dir", 1);
    }

    public void SetState(GhostState state)
    {
        State = state;
        if (animator != null) animator.SetInteger("State", (int)state);
    }

    // 被吃掉：直线飞回鬼屋
    public void Die()
    {
        if (State == GhostState.Dead) return;
        SetState(GhostState.Dead);
        f = transform.position; to = sp; t = 0f;
        dist = Vector3.Distance(f, to);
        lerp = true;
    }

    void Update()
    {
        if (GameManager.Instance == null || !GameManager.Instance.GhostsCanMove || !init || lvl == null)
        {
            if (animator != null) animator.speed = 0f;
            return;
        }
        if (animator != null) animator.speed = 1f;

        // 移动中（害怕/死亡时速度变慢）
        if (lerp)
        {
            float spd = State == GhostState.Normal ? normalSpeed : normalSpeed * slowFactor;
            if (spd < 0.01f) spd = 0.01f;
            t += dist > 0f ? spd * Time.deltaTime / dist : 1f;
            transform.position = Vector3.Lerp(f, to, Mathf.Clamp01(t));
            if (t < 1f) return;

            transform.position = to;
            lerp = false;

            // 死亡的鬼到家了：复活
            if (State == GhostState.Dead)
            {
                c = sc; pc = sc;
                ct = -1; cs = 0;
                if (GameManager.Instance != null) GameManager.Instance.OnGhostRespawned(this);
                else SetState(GhostState.Normal);
                return;
            }
        }

        if (State == GhostState.Dead)
        {
            // shouldnt happen
            f = transform.position; to = sp; t = 0f;
            dist = Vector3.Distance(f, to);
            lerp = true;
            return;
        }

        // 选下一个方向，开始走下一格
        GridDir n = Pick();
        if (n != GridDir.None)
        {
            pc = c;
            c += n.ToCellOffset();
            md = n;
            f = transform.position;
            to = lvl.CellToWorld(c);
            t = 0f;
            dist = Vector3.Distance(f, to);
            lerp = true;
            if (animator != null) animator.SetInteger("Dir", (int)n);
        }
    }

    // 能不能走进这个格子（鬼屋里的鬼可以穿过鬼屋墙出去）
    bool Ok(Vector2Int n)
    {
        if (!lvl.InBounds(n)) return false;
        if (lvl.IsWalkable(n)) return true;
        return lvl.IsGhostHouse(c) && lvl.IsGhostHouse(n);
    }

    // 选方向：先找能走的方向（尽量不回头），再按鬼的类型决定
    GridDir Pick()
    {
        var list = new List<GridDir>();
        GridDir back = GridDir.None;
        foreach (GridDir d in GridDirExt.All)
        {
            Vector2Int nn = c + d.ToCellOffset();
            if (!Ok(nn)) continue;
            if (nn == pc) back = d; else list.Add(d);
        }
        if (list.Count == 0 && back != GridDir.None) list.Add(back);

        if (list.Count == 0)
        {
            c = lvl.WorldToCell(transform.position);
            pc = c;
            return GridDir.None;
        }

        // 在鬼屋里：先出去（1、3 号往上，2、4 号往下）
        if (lvl.IsGhostHouse(c))
            return Near(list, lvl.GetHouseExit(ghostNumber == 1 || ghostNumber == 3, c));

        if (State == GhostState.Scared || State == GhostState.Recovering) return Dist(list, true);

        // 1 号躲开，2 号追击，3 号随机
        if (ghostNumber == 1) return Dist(list, true);
        else if (ghostNumber == 2) return Dist(list, false);
        else if (ghostNumber == 3) return list[Random.Range(0, list.Count)];

        // 4 号：顺时针绕着外墙的四个角走
        if (ct < 0 || ct > 3)
        {
            ct = 0;
            float b = float.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                float dd = (lvl.GetCornerCell(i) - c).sqrMagnitude;
                if (dd < b) { b = dd; ct = i; }
            }
        }
        cs++;
        if (c == lvl.GetCornerCell(ct) || cs > 150) { ct = (ct + 1) % 4; cs = 0; }
        return Near(list, lvl.GetCornerCell(ct));
    }

    // away = true 选离吃豆人不变近的方向，false 选不变远的方向
    GridDir Dist(List<GridDir> o, bool away)
    {
        if (GameManager.Instance == null) return o[Random.Range(0, o.Count)];

        Vector3 p = GameManager.Instance.PacPosition;
        float now = Vector3.Distance(lvl.CellToWorld(c), p);

        List<GridDir> tmp = new List<GridDir>();
        for (int i = 0; i < o.Count; i++)
        {
            float a = Vector3.Distance(lvl.CellToWorld(c + o[i].ToCellOffset()), p);
            if (away) { if (a >= now) tmp.Add(o[i]); }
            else if (a <= now) tmp.Add(o[i]);
        }
        if (tmp.Count == 0) tmp = o;
        return tmp[Random.Range(0, tmp.Count)];
    }

    // 选离目标格子最近的方向
    GridDir Near(List<GridDir> o, Vector2Int target)
    {
        if (o == null || o.Count == 0) return GridDir.None;
        GridDir r = o[0];
        float best = float.MaxValue;
        foreach (GridDir d in o)
        {
            float x = (c + d.ToCellOffset() - target).sqrMagnitude;
            if (x < best) { best = x; r = d; }
        }
        return r;
    }
}
