using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using NetworkBehaviour = FishNet.Object.NetworkBehaviour;

public class Test : NetworkBehaviour
{
    [SerializeField] private int _myNumber;

    private readonly SyncVar<int> _syncVar = new();

    private void Awake()
    {
        _syncVar.OnChange += OnSyncChange;
    }

    private void OnDestroy()
    {
        _syncVar.OnChange -= OnSyncChange;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        enabled = IsServerInitialized;
    }

    private void Update()
    {
        if (!IsServerInitialized)
            return;

        if (Input.GetKeyDown(KeyCode.A))
            TellNumber(_myNumber);
    }

    [ServerRpc(RequireOwnership = false)]
    private void TellNumber(int myNumber)
    {
        _syncVar.Value = myNumber;
        AllReceiveNumber(myNumber);
    }

    [ObserversRpc(BufferLast = true)]
    private void AllReceiveNumber(int number)
    {
        Debug.Log($"Received number: {number}");
    }

    private void OnSyncChange(int prev, int next, bool asServer)
    {
        Debug.Log($"SyncVar just changed to {next} on {gameObject.name}");
    }
}