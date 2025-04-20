using PurrNet;
using UnityEngine;

public class Test : MonoBehaviour
{
    private string _testString = "Test String";
    void Update()
    {
        if (NetworkManager.main)
            Debug.LogError($"Is client: {NetworkManager.main.isClient}");
    }
}