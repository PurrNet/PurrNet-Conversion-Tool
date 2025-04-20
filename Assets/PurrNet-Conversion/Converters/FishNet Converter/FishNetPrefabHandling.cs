using PurrNet.ConversionTool;
using UnityEngine;

#if FISHNET
using System;
using FishNet.Component.Animating;
using FishNet.Component.Transforming;
using FishNet.Object;
#endif
public class FishNetPrefabHandling : NetworkPrefabHandling
{
#if FISHNET
    public override bool ConvertPrefab(GameObject prefab)
    {
        bool edited = false;
        var networkTransform = prefab.GetComponentsInChildren<NetworkTransform>();
        var networkAnimators = prefab.GetComponentsInChildren<NetworkAnimator>();
        var networkObjects = prefab.GetComponentsInChildren<NetworkObject>();

        for (var i = 0; i < networkTransform.Length; i++)
        {
            var nt = networkTransform[i];
            ConvertNetworkTransform(nt);
            GameObject.DestroyImmediate(nt, true);
            edited = true;
        }

        for (var i = 0; i < networkAnimators.Length; i++)
        {
            var na = networkAnimators[i];
            ConvertNetworkAnimator(na);
            GameObject.DestroyImmediate(na, true);
            edited = true;
        }

        for (var i = 0; i < networkObjects.Length; i++)
        {
            var nob = networkObjects[i];
            GameObject.DestroyImmediate(nob, true);
            edited = true;
        }

        return edited;
    }
    
    private void ConvertNetworkTransform(NetworkTransform nt)
    {
        var purrNa = nt.gameObject.AddComponent<PurrNet.NetworkTransform>();
        
        var ntType = nt.GetType();
        var purrNaType = purrNa.GetType();
        
        CopyField(ntType, nt, purrNaType, purrNa, "_synchronizeParent", "_syncParent");
        CopyField(ntType, nt, purrNaType, purrNa, "_synchronizeScale", "_syncScale");
        
        var syncModeEnum = purrNaType.Assembly.GetType("PurrNet.SyncMode");
        if (syncModeEnum == null)
        {
            Debug.LogError("Could not find PurrNet.SyncMode type");
            return;
        }
        
        var syncPositionField = ntType.GetField("_synchronizePosition", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var syncRotationField = ntType.GetField("_synchronizeRotation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var purrSyncPositionField = purrNaType.GetField("_syncPosition", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var purrSyncRotationField = purrNaType.GetField("_syncRotation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (syncPositionField != null && purrSyncPositionField != null)
        {
            bool fishNetValue = (bool)syncPositionField.GetValue(nt);
            object enumValue = fishNetValue ? 
                Enum.ToObject(syncModeEnum, syncModeEnum.GetField("World").GetValue(null)) : 
                Enum.ToObject(syncModeEnum, syncModeEnum.GetField("No").GetValue(null));
            purrSyncPositionField.SetValue(purrNa, enumValue);
        }
        
        if (syncRotationField != null && purrSyncRotationField != null)
        {
            bool fishNetValue = (bool)syncRotationField.GetValue(nt);
            object enumValue = fishNetValue ? 
                Enum.ToObject(syncModeEnum, syncModeEnum.GetField("World").GetValue(null)) : 
                Enum.ToObject(syncModeEnum, syncModeEnum.GetField("No").GetValue(null));
            purrSyncRotationField.SetValue(purrNa, enumValue);
        }
    }
    
    private void ConvertNetworkAnimator(NetworkAnimator na)
    {
        var purrNa = na.gameObject.AddComponent<PurrNet.NetworkAnimator>();
    }

    private void CopyField(Type sourceType, object sourceObj, Type targetType, object targetObj, string sourceFieldName, string targetFieldName)
    {
        var sourceField = sourceType.GetField(sourceFieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var targetField = targetType.GetField(targetFieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    
        if (sourceField != null && targetField != null)
        {
            var value = sourceField.GetValue(sourceObj);
            targetField.SetValue(targetObj, value);
        }
    }
#endif
}