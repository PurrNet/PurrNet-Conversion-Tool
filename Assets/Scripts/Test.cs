using PurrNet;
using UnityEngine;

public class Test : MonoBehaviour
{
    void Update()
    {
        if(NetworkManager.main)
            Debug.LogError($"Is client: {NetworkManager.main.isClient}");
    }
}
