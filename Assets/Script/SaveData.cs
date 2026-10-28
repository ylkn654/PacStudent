using UnityEngine;

// 存档：每关的最高分和最佳时间（PlayerPrefs）
public static class SaveData
{
    // 读取
    public static int GetHighScore(int level) { return Mathf.Max(0, PlayerPrefs.GetInt("Level" + level + "_HighScore", 0)); }

    public static float GetBestTime(int level)
    {
        float f = PlayerPrefs.GetFloat($"Level{level}_BestTime", 0f);
        if (float.IsNaN(f)) return 0f;
        if (float.IsInfinity(f) || f < 0f) return 0f;
        return f;
    }

    // 保存：分数更高，或分数相同但用时更短才保存
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

    // 时间格式化成 分:秒:百分秒
    public static string FormatTime(float seconds)
    {
        if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f) seconds = 0f;
        int n = Mathf.Min(Mathf.FloorToInt(seconds * 100f), 599999);
        return $"{n / 6000:00}:{(n / 100) % 60:00}:{n % 100:00}";
    }
}
