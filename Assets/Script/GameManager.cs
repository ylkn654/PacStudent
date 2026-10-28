using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// 游戏总控：倒计时、分数、生命、计时、鬼的状态、音乐和游戏结束
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Level")]
    public int levelNumber = 1;
    public string levelName = "Level 1";
    public string startSceneName = "StartScene";

    [Header("References")]
    public HUD hud;
    public PacStudentController pac;
    public GhostController[] ghosts;

    [Header("Rules")]
    public int startLives = 3;
    public int pelletScore = 10;
    public int powerPelletScore = 50;
    public int cherryScore = 100;
    public int ghostScore = 300;
    public float scaredDuration = 10f;
    public float recoveringAt = 3f;

    [Header("Music")]
    public AudioSource musicSource;
    public AudioClip introMusic;
    public AudioClip normalMusic;
    public AudioClip scaredMusic;
    public AudioClip deadGhostMusic;

    [Header("Sound effects")]
    public AudioSource sfxSource;
    public AudioClip cherryClip;
    public AudioClip ghostEatClip;
    public AudioClip powerPelletClip;

    [Header("Fail-safes")]
    [Tooltip("If PacStudent's death callback never arrives, the round continues after this many seconds")]
    public float deathTimeout = 6f;

    // 0 = countdown 1 = playing 2 = dying 3 = over
    int st = 0;
    int sc;     // 分数
    int l;      // 生命
    float t;    // 游戏时间
    float t2;   // 鬼害怕剩余时间
    GhostState gs = GhostState.Normal;

    public bool IsPlaying => st == 1;
    public bool GhostsCanMove { get { return st == 1; } }
    public Vector3 PacPosition => pac != null ? pac.transform.position : transform.position;

    void Awake()
    {
        if (Instance != null && Instance != this)
            Debug.LogWarning($"[GameManager] More than one GameManager in the scene ({Instance.name}, {name}). Using the newest one.", this);
        Instance = this;

        // Inspector 没拖的引用自动找
        if (hud == null) hud = FindAnyObjectByType<HUD>();
        if (pac == null) pac = FindAnyObjectByType<PacStudentController>();
        if (ghosts == null || ghosts.Length == 0) ghosts = FindObjectsByType<GhostController>();
        if (musicSource == null) musicSource = GetComponent<AudioSource>();
        if (musicSource == null) musicSource = gameObject.AddComponent<AudioSource>();
        if (sfxSource == null) sfxSource = musicSource;
        if (hud == null) Debug.LogError("[GameManager] No HUD found: score, lives and timers will not be shown.", this);
        if (pac == null) Debug.LogError("[GameManager] No PacStudentController found.", this);
        if (ghosts.Length == 0) Debug.LogWarning("[GameManager] No ghosts found.", this);
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        if (startLives < 1) { Debug.LogWarning("[GameManager] startLives must be at least 1. Using 1.", this); startLives = 1; }
        if (recoveringAt > scaredDuration) recoveringAt = scaredDuration;

        l = startLives;
        if (hud != null) { hud.SetLives(l); hud.SetScore(0); hud.SetTime(0f); hud.SetGhostTimer(0); hud.SetLevelName(levelName); }
        StartCoroutine(Begin());
    }

    // 开局倒计时 3 2 1 GO
    IEnumerator Begin()
    {
        st = 0;
        if (pac != null) pac.ControlEnabled = false;
        Music(introMusic, false);

        string[] arr = { "3", "2", "1", "GO!" };
        for (int i = 0; i < arr.Length; i++)
        {
            if (hud != null) hud.SetBigText(arr[i]);
            yield return new WaitForSeconds(1f);
        }

        if (hud != null) hud.SetBigText(null);
        if (musicSource != null) musicSource.Stop();
        st = 1;
        if (pac != null) pac.ControlEnabled = true;
        RefreshMusic();
    }

    // 更新游戏计时和鬼害怕倒计时
    void Update()
    {
        if (st == 1 || st == 2)
        {
            t += Time.deltaTime;
            if (hud != null) hud.SetTime(t);
        }

        if (st == 1)
        {
            if (t2 > 0f)
            {
                t2 -= Time.deltaTime;
                if (t2 <= 0f)
                {
                    t2 = 0f;
                    SetAll(GhostState.Normal);
                }
                else if (t2 <= recoveringAt && gs == GhostState.Scared) SetAll(GhostState.Recovering);
                if (hud != null) hud.SetGhostTimer(Mathf.CeilToInt(t2));
            }
        }
    }

    // ---- 计分 ----
    void Add(int n)
    {
        if (n <= 0) return;
        long tmp = (long)sc + n;
        if (tmp > int.MaxValue) sc = int.MaxValue; else sc = (int)tmp;
        if (hud != null) hud.SetScore(sc);
    }

    // 吃豆子，能量豆会让鬼进入害怕状态
    public void EatPellet(Pellet pellet)
    {
        if (pellet == null || st == 3) return;
        if (!pellet.gameObject.activeSelf) return;
        bool p = pellet.isPowerPellet;
        pellet.gameObject.SetActive(false);
        Destroy(pellet.gameObject);

        if (p == true)
        {
            Add(powerPelletScore);
            Sfx(powerPelletClip);
            t2 = scaredDuration;
            if (hud != null) hud.SetGhostTimer(Mathf.CeilToInt(t2));
            SetAll(GhostState.Scared);
        }
        else Add(pelletScore);

        if (Pellet.Remaining <= 0) StartCoroutine(End());
    }

    public void EatCherry(Cherry cherry)
    {
        if (cherry == null || st == 3) return;
        Add(cherryScore); Sfx(cherryClip);
        cherry.Eaten();
    }

    // ---- 鬼 ----
    // 改变所有没死的鬼的状态
    void SetAll(GhostState s)
    {
        gs = s;
        if (ghosts != null)
            for (int i = 0; i < ghosts.Length; i++)
                if (ghosts[i] != null && ghosts[i].State != GhostState.Dead) ghosts[i].SetState(s);
        RefreshMusic();
    }

    // 吃豆人碰到鬼：普通鬼 = 吃豆人死，害怕的鬼 = 鬼被吃
    public void OnGhostTouched(GhostController ghost)
    {
        if (ghost == null || st != 1) return;
        if (ghost.State == GhostState.Normal) { StartCoroutine(Dead()); return; }
        if (ghost.State == GhostState.Scared || ghost.State == GhostState.Recovering)
        {
            ghost.Die();
            Add(ghostScore);
            Sfx(ghostEatClip);
            RefreshMusic();
        }
    }

    // 死掉的鬼回到家后复活
    public void OnGhostRespawned(GhostController ghost)
    {
        if (ghost == null) return;
        ghost.SetState(gs);
        RefreshMusic();
    }

    // ---- 吃豆人死亡 / 游戏结束 ----
    // 扣命，等死亡动画播完，然后重生或结束游戏
    IEnumerator Dead()
    {
        st = 2;
        l = Mathf.Max(0, l - 1);
        if (hud != null) hud.SetLives(l);

        bool flag = pac == null;
        if (pac != null) pac.Die(() => flag = true);

        float w = 0f;
        while (!flag && w < deathTimeout) { w += Time.deltaTime; yield return null; }
        if (!flag) Debug.LogWarning("[GameManager] PacStudent death did not finish in time. Continuing anyway.", this);

        if (l <= 0) { StartCoroutine(End()); yield break; }

        // 重生：所有角色回到起点
        t2 = 0f;
        if (hud != null) hud.SetGhostTimer(0);
        gs = GhostState.Normal;
        if (ghosts != null) foreach (var g in ghosts) if (g != null) g.ResetToStart();
        if (pac != null) { pac.ResetToStart(); pac.ControlEnabled = true; }
        st = 1;
        RefreshMusic();
    }

    // 游戏结束：保存记录，3 秒后回开始界面
    IEnumerator End()
    {
        if (st == 3) yield break;
        st = 3;

        if (pac != null) pac.Freeze();
        if (hud != null) { hud.SetButtonsInteractable(false); hud.SetBigText("Game Over"); }

        try { SaveData.TrySave(levelNumber, sc, t); }
        catch (System.Exception e) { Debug.LogError("[GameManager] Could not save the record: " + e.Message, this); }

        yield return new WaitForSeconds(3f);

        if (!string.IsNullOrEmpty(startSceneName) && Application.CanStreamedLevelBeLoaded(startSceneName))
        {
            SceneManager.LoadScene(startSceneName);
        }
        else
        {
            Debug.LogWarning($"[GameManager] Scene '{startSceneName}' is not in Build Settings. Loading scene 0 instead.", this);
            if (SceneManager.sceneCountInBuildSettings > 0) SceneManager.LoadScene(0);
        }
    }

    // ---- 声音 ----
    void Sfx(AudioClip c)
    {
        if (c == null) return;
        if (sfxSource != null) sfxSource.PlayOneShot(c);
        else if (musicSource != null) musicSource.PlayOneShot(c);
    }

    // 根据鬼的状态切换背景音乐
    void RefreshMusic()
    {
        if (st == 0 || st == 3) return;

        bool anyDead = false;
        if (ghosts != null) foreach (var g in ghosts) { if (g != null && g.State == GhostState.Dead) { anyDead = true; break; } }

        AudioClip c = normalMusic;
        if (anyDead && deadGhostMusic != null) c = deadGhostMusic;
        else if (gs != GhostState.Normal && scaredMusic != null) c = scaredMusic;
        Music(c, true);
    }

    void Music(AudioClip c, bool loop)
    {
        if (c == null || musicSource == null) return;
        if (musicSource.clip == c && musicSource.isPlaying) return;
        musicSource.clip = c; musicSource.loop = loop;
        musicSource.Play();
    }
}
