using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// in game HUD: lives, score, timer, big text and exit button
public class HUD : MonoBehaviour
{
    public Image[] lifeIcons;
    public Text scoreText;
    public Text timerText;
    public Text ghostTimerText;
    public Text levelNameText;
    public Button exitButton;

    [Header("Round start / game over")]
    public GameObject blocker;
    public Text bigText;

    public string startSceneName = "StartScene";

    void Awake()
    {
        if (exitButton != null) exitButton.onClick.AddListener(Back);
        SetScore(0);
        SetTime(0f);
        SetGhostTimer(0);
        SetBigText(null);
    }

    // exit button: back to the start scene
    private void Back()
    {
        if (!string.IsNullOrEmpty(startSceneName) && Application.CanStreamedLevelBeLoaded(startSceneName)) SceneManager.LoadScene(startSceneName);
        else if (SceneManager.sceneCountInBuildSettings > 0) {
            Debug.LogWarning($"[HUD] Scene '{startSceneName}' is not in Build Settings. Loading scene 0 instead.", this);
            SceneManager.LoadScene(0);
        }
    }

    // setters for each HUD element below
    public void SetLives(int lives)
    {
        if (lifeIcons == null) return;
        for (int i = 0; i < lifeIcons.Length; i++)
        {
            if (lifeIcons[i] != null)
            {
                if (i < lives) lifeIcons[i].enabled = true;
                else lifeIcons[i].enabled = false;
            }
        }
    }

    public void SetScore(int score) { if (scoreText != null) scoreText.text = Mathf.Max(0, score).ToString("000000"); }

    public void SetTime(float seconds)
    {
        if (timerText == null) return;
        timerText.text = SaveData.FormatTime(seconds);
    }

    public void SetGhostTimer(int seconds)
    {
        if (ghostTimerText != null)
        {
            ghostTimerText.gameObject.SetActive(seconds > 0);
            ghostTimerText.text = "Scared " + seconds.ToString();
        }
    }

    public void SetLevelName(string name)
    {
        if (levelNameText != null) levelNameText.text = name != null ? name : "";
    }

    // big text in the middle (countdown / Game Over), null hides it
    public void SetBigText(string text)
    {
        bool b = string.IsNullOrEmpty(text) == false;
        if (blocker != null) blocker.SetActive(b);
        if (bigText != null)
        {
            bigText.gameObject.SetActive(b);
            if (b) bigText.text = text;
        }
    }

    public void SetButtonsInteractable(bool on)
    {
        Button[] arr = GetComponentsInChildren<Button>(true);
        for (int k = 0; k < arr.Length; k++) if (arr[k] != null) arr[k].interactable = on;
    }
}
