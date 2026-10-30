using UnityEngine;

// pellet (normal / power), also counts how many are left
public class Pellet : MonoBehaviour
{
    public bool isPowerPellet;
    public static int Remaining { get; private set; }
    void OnEnable() { Remaining++; }
    void OnDisable() { Remaining = Remaining - 1; }
}
