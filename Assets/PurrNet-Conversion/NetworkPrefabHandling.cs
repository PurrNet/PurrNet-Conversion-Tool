using UnityEngine;

namespace PurrNet.ConversionTool
{
    public class NetworkPrefabHandling
    {
        // Returns whether a prefab was converted or not
        public virtual bool ConvertPrefab(GameObject prefab)
        {
            return false;
        }
    }
}