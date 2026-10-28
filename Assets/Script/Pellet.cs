using UnityEngine;

// 豆子（普通豆 / 能量豆），并统计剩余数量
public class Pellet : MonoBehaviour
{
    public bool isPowerPellet;
    public static int Remaining { get; private set; }
    void OnEnable() { Remaining++; }
    void OnDisable() { Remaining = Remaining - 1; }
}
