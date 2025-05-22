using UnityEngine;

namespace PurrNet.ConversionTool
{
    public class NetworkSceneHandling
    {
        // Returns whether a scene object was converted or not
        public virtual bool ConvertSceneObject(GameObject sceneObject)
        {
            return false;
        }
    }
}
