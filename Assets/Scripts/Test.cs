using PurrNet;
using UnityEngine;

public class Test : MonoBehaviour
{
    private ushort _testString = 33;
    void Update()
    {
        if (NetworkManager.main)
            Debug.LogError($"Is client: {NetworkManager.main.isClient}");
    }
}