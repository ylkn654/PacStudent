using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// start screen: level buttons, shows best score and time for each level
public class StartSceneManager : MonoBehaviour
{
    public Button level1Button;
    public Button level2Button;
    public string level1Scene = "PacStudent";
    [Tooltip("Leave empty until the Level 2 (design iteration) scene exists")]
    public string level2Scene = "";

    public Text level1ScoreText;
    public Text level1TimeText;
    public Text level2ScoreText;
    public Text level2TimeText;

    [Tooltip("Small delay so the click sound can be heard before the scene changes")]
    public float loadDelay = 0.25f;

    bool busy = false;

    void Start()
    {
        // show records
        if (level1ScoreText != null) level1ScoreText.text = "High Score  " + SaveData.GetHighScore(1).ToString("000000");
        if (level1TimeText != null) level1TimeText.text = "Best Time  " + SaveData.FormatTime(SaveData.GetBestTime(1));
        if (level2ScoreText != null) level2ScoreText.text = "High Score  " + SaveData.GetHighScore(2).ToString("000000");
        if (level2TimeText != null) level2TimeText.text = "Best Time  " + SaveData.FormatTime(SaveData.GetBestTime(2));

        // hook up buttons
        if (level1Button != null) level1Button.onClick.AddListener(() => Go(level1Scene));
        else Debug.LogWarning("[StartScene] Level 1 button is not assigned.", this);
        if (level2Button != null) level2Button.onClick.AddListener(() => Go(level2Scene));
    }

    // load level (small delay so the click sound can finish)
    void Go(string s)
    {
        if (busy || string.IsNullOrEmpty(s)) return;
        if (!Application.CanStreamedLevelBeLoaded(s))
        {
            Debug.LogWarning($"[StartScene] Scene '{s}' is not in Build Settings.", this);
            return;
        }
        busy = true;
        StartCoroutine(Wait(s));
    }

    IEnumerator Wait(string s)
    {
        yield return new WaitForSecondsRealtime(loadDelay < 0f ? 0f : loadDelay);
        SceneManager.LoadScene(s);
    }
}
