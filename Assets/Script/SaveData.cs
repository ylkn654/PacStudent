using UnityEngine;

// save data: best score and best time per level (PlayerPrefs)
public static class SaveData
{
    const int MaxCs = 99 * 6000 + 5999;   // 99:59:99 so the text never gets wider

    // read
    public static int GetHighScore(int level) { return Mathf.Max(0, PlayerPrefs.GetInt("Level" + level + "_HighScore", 0)); }

    public static float GetBestTime(int level)
    {
        float f = PlayerPrefs.GetFloat($"Level{level}_BestTime", 0f);
        if (float.IsNaN(f)) return 0f;
        if (float.IsInfinity(f) || f < 0f) return 0f;
        return f;
    }

    // save: only if the score is higher, or same score in less time
    public static bool TrySave(int level, int score, float time)
    {
        if (score < 0) score = 0;
        if (float.IsNaN(time) || float.IsInfinity(time) || time < 0f) time = 0f;

        bool flag = PlayerPrefs.HasKey($"Level{level}_HighScore");
        int hs = GetHighScore(level);
        if (flag && !(score > hs || (score == hs && time < GetBestTime(level)))) return false;

        PlayerPrefs.SetInt("Level" + level + "_HighScore", score);
        PlayerPrefs.SetFloat($"Level{level}_BestTime", time);
        PlayerPrefs.Save();
        return true;
    }

    // format time as min:sec:centisec
    public static string FormatTime(float seconds)
    {
        if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f) seconds = 0f;
        int n = Mathf.Min(Mathf.FloorToInt(seconds * 100f), MaxCs);
        return $"{n / 6000:00}:{(n / 100) % 60:00}:{n % 100:00}";
    }
}
