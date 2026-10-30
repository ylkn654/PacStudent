using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// game manager: countdown, score, lives, timer, ghost states, music and game over
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

    // round phase
    const int COUNTDOWN = 0, PLAYING = 1, DYING = 2, OVER = 3;
    int st = COUNTDOWN;
    int sc;     // score
    int l;      // lives
    float t;    // game time
    float t2;   // scared time left
    GhostState gs = GhostState.Normal;

    public bool IsPlaying => st == PLAYING;
    public bool GhostsCanMove { get { return st == PLAYING; } }
    public Vector3 PacPosition => pac != null ? pac.transform.position : transform.position;

    void Awake()
    {
        if (Instance != null && Instance != this)
            Debug.LogWarning($"[GameManager] More than one GameManager in the scene ({Instance.name}, {name}). Using the newest one.", this);
        Instance = this;

        // find anything not dragged in the Inspector
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
        if (startLives < 1)
        {
            Debug.LogWarning("[GameManager] startLives must be at least 1. Using 1.", this);
            startLives = 1;
        }
        if (recoveringAt > scaredDuration) recoveringAt = scaredDuration;

        l = startLives;
        if (hud != null)
        {
            hud.SetLives(l);
            hud.SetScore(0);
            hud.SetTime(0f);
            hud.SetGhostTimer(0);
            hud.SetLevelName(levelName);
        }
        StartCoroutine(Begin());
    }

    // round start countdown 3 2 1 GO
    IEnumerator Begin()
    {
        st = COUNTDOWN;
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
        st = PLAYING;
        if (pac != null) pac.ControlEnabled = true;
        RefreshMusic();
    }

    // update game timer and scared countdown
    void Update()
    {
        if (st == PLAYING || st == DYING)
        {
            t += Time.deltaTime;
            if (hud != null) hud.SetTime(t);
        }

        if (st == PLAYING)
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

    // ---- score ----
    void Add(int n)
    {
        if (n <= 0) return;
        long tmp = (long)sc + n;
        if (tmp > int.MaxValue) sc = int.MaxValue;
        else sc = (int)tmp;
        if (hud != null) hud.SetScore(sc);
    }

    // eat a pellet, power pellets make the ghosts scared
    public void EatPellet(Pellet pellet)
    {
        if (pellet == null || st == OVER) return;
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
        if (cherry == null || st == OVER) return;
        Add(cherryScore);
        Sfx(cherryClip);
        cherry.Eaten();
    }

    // ---- ghosts ----
    // change the state of every ghost that isnt dead
    void SetAll(GhostState s)
    {
        gs = s;
        if (ghosts != null)
            for (int i = 0; i < ghosts.Length; i++)
                if (ghosts[i] != null && ghosts[i].State != GhostState.Dead) ghosts[i].SetState(s);
        RefreshMusic();
    }

    // pac touched a ghost: normal ghost = pac dies, scared ghost = ghost gets eaten
    public void OnGhostTouched(GhostController ghost)
    {
        if (ghost == null || st != PLAYING) return;
        if (ghost.State == GhostState.Normal)
        {
            StartCoroutine(Dead());
            return;
        }
        if (ghost.State == GhostState.Scared || ghost.State == GhostState.Recovering)
        {
            ghost.Die();
            Add(ghostScore);
            Sfx(ghostEatClip);
            RefreshMusic();
        }
    }

    // dead ghost got home, respawn
    public void OnGhostRespawned(GhostController ghost)
    {
        if (ghost == null) return;
        ghost.SetState(gs);
        RefreshMusic();
    }

    // ---- pac death / game over ----
    // lose a life, wait for the death anim, then respawn or game over
    IEnumerator Dead()
    {
        st = DYING;
        l = Mathf.Max(0, l - 1);
        if (hud != null) hud.SetLives(l);

        bool flag = pac == null;
        if (pac != null) pac.Die(() => flag = true);

        float w = 0f;
        while (!flag && w < deathTimeout)
        {
            w += Time.deltaTime;
            yield return null;
        }
        if (!flag) Debug.LogWarning("[GameManager] PacStudent death did not finish in time. Continuing anyway.", this);

        if (l <= 0)
        {
            StartCoroutine(End());
            yield break;
        }

        // respawn: everyone back to the start
        t2 = 0f;
        if (hud != null) hud.SetGhostTimer(0);
        gs = GhostState.Normal;
        if (ghosts != null) foreach (var g in ghosts) if (g != null) g.ResetToStart();
        if (pac != null)
        {
            pac.ResetToStart();
            pac.ControlEnabled = true;
        }
        st = PLAYING;
        RefreshMusic();
    }

    // game over: save the record, back to the start screen after 3s
    IEnumerator End()
    {
        if (st == OVER) yield break;
        st = OVER;

        if (pac != null) pac.Freeze();
        if (hud != null)
        {
            hud.SetButtonsInteractable(false);
            hud.SetBigText("Game Over");
        }

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

    // ---- audio ----
    void Sfx(AudioClip c)
    {
        if (c == null) return;
        if (sfxSource != null) sfxSource.PlayOneShot(c);
        else if (musicSource != null) musicSource.PlayOneShot(c);
    }

    // switch background music depending on ghost state
    void RefreshMusic()
    {
        if (st == COUNTDOWN || st == OVER) return;

        bool anyDead = false;
        if (ghosts != null) foreach (var g in ghosts)
        {
            if (g != null && g.State == GhostState.Dead)
            {
                anyDead = true;
                break;
            }
        }

        AudioClip c = normalMusic;
        if (anyDead && deadGhostMusic != null) c = deadGhostMusic;
        else if (gs != GhostState.Normal && scaredMusic != null) c = scaredMusic;
        Music(c, true);
    }

    void Music(AudioClip c, bool loop)
    {
        if (c == null || musicSource == null) return;
        if (musicSource.clip == c && musicSource.isPlaying) return;
        musicSource.clip = c;
        musicSource.loop = loop;
        musicSource.Play();
    }
}
