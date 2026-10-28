using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 游戏界面：生命、分数、计时、提示文字和退出按钮
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
        SetScore(0); SetTime(0f); SetGhostTimer(0); SetBigText(null);
    }

    // 退出按钮：回到开始场景
    private void Back()
    {
        if (!string.IsNullOrEmpty(startSceneName) && Application.CanStreamedLevelBeLoaded(startSceneName)) SceneManager.LoadScene(startSceneName);
        else if (SceneManager.sceneCountInBuildSettings > 0) {
            Debug.LogWarning($"[HUD] Scene '{startSceneName}' is not in Build Settings. Loading scene 0 instead.", this);
            SceneManager.LoadScene(0);
        }
    }

    // 下面是更新各个界面元素
    public void SetLives(int lives)
    {
        if (lifeIcons == null) return;
        for (int i = 0; i < lifeIcons.Length; i++) { if (lifeIcons[i] != null) { if (i < lives) lifeIcons[i].enabled = true; else lifeIcons[i].enabled = false; } }
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

    // 屏幕中间的大字（倒计时 / Game Over），传 null 隐藏
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
