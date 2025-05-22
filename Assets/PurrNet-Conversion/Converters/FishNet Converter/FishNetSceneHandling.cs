using System;
using System.Collections.Generic;
using UnityEngine;

#if FISHNET
using FishNet;
using FishNet.Object;
using FishNet.Component;
using FishNet.Managing.Client;
using FishNet.Managing.Debugging;
using FishNet.Managing.Predicting;
using FishNet.Managing.Scened;
using FishNet.Managing.Server;
using FishNet.Managing.Timing;
using FishNet.Managing.Transporting;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.Tugboat;
using FishNet.Transporting.Yak;
using PurrNet.Transports;
#endif

namespace PurrNet.ConversionTool
{
    public class FishNetSceneHandling : NetworkSceneHandling
    {
        public override bool ConvertSceneObject(GameObject sceneObject)
        {
#if FISHNET
            var nm = sceneObject.GetComponentInChildren<FishNet.Managing.NetworkManager>();
            if (nm)
            {
                HandleNetworkManagerConversion(nm);
                return true;
            }
            
            bool edited = false;
            var networkTransform = sceneObject.GetComponentsInChildren<FishNet.Component.Transforming.NetworkTransform>();
            var networkAnimators = sceneObject.GetComponentsInChildren<FishNet.Component.Animating.NetworkAnimator>();
            var networkObjects = sceneObject.GetComponentsInChildren<NetworkObject>();

            for (var i = 0; i < networkTransform.Length; i++)
            {
                var nt = networkTransform[i];
                FishNetPrefabHandling.ConvertNetworkTransform(nt);
                GameObject.DestroyImmediate(nt, true);
                edited = true;
            }

            for (var i = 0; i < networkAnimators.Length; i++)
            {
                var na = networkAnimators[i];
                FishNetPrefabHandling.ConvertNetworkAnimator(na);
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
#else
            return false;
#endif
        }

#if FISHNET
        private void HandleNetworkManagerConversion(FishNet.Managing.NetworkManager nm)
        {
            var purrNm = nm.gameObject.AddComponent<NetworkManager>();
            var componentsToDelete = new List<Component>();
            componentsToDelete.Add(nm);
            if (nm.TryGetComponent(out TimeManager tm))
            {
                componentsToDelete.Add(tm);
                var nmType = purrNm.GetType();
                var tickRateField = nmType.GetField("_tickRate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (tickRateField != null)
                    tickRateField.SetValue(purrNm, tm.TickRate);
            }
            
            if(nm.TryGetComponent(out ServerManager sm))
                componentsToDelete.Add(sm);
            
            if(nm.TryGetComponent(out ClientManager cm))
                componentsToDelete.Add(cm);
            
            if(nm.TryGetComponent(out DebugManager dm))
                componentsToDelete.Add(dm);
            
            if(nm.TryGetComponent(out PredictionManager pm))
                componentsToDelete.Add(pm);

            if (nm.TryGetComponent(out FishNet.Managing.Statistic.StatisticsManager stats))
            {
                componentsToDelete.Add(stats);
                var purrStats = nm.gameObject.AddComponent<StatisticsManager>();
            }
            
            if(nm.TryGetComponent(out SceneManager scenemanager))
                componentsToDelete.Add(scenemanager);

            if (nm.TryGetComponent(out Tugboat tugboat))
            {
                componentsToDelete.Add(tugboat);
                var udp = nm.gameObject.AddComponent<UDPTransport>();
                udp.address = tugboat.GetClientAddress();
                udp.serverPort = tugboat.GetPort();
            }

            if (nm.TryGetComponent(out Yak yak))
            {
                componentsToDelete.Add(yak);
                var localTransport = nm.gameObject.AddComponent<LocalTransport>();
            }

            if (nm.TryGetComponent(out Multipass mp))
            {
                componentsToDelete.Add(mp);
                var genericTransport = nm.gameObject.AddComponent<GenericTransport>();
            }

            if (nm.TryGetComponent(out TransportManager transportManager))
            {
                componentsToDelete.Add(transportManager);
                switch (transportManager.Transport)
                {
                    case Multipass multipass:
                        if (nm.TryGetComponent(out GenericTransport generic))
                            purrNm.transport = generic;
                        break;
                    case Tugboat tugboat1:
                        if (nm.TryGetComponent(out UDPTransport udp))
                            purrNm.transport = udp;
                        break;
                    case Yak yak1:
                        if (nm.TryGetComponent(out LocalTransport local))
                            purrNm.transport = local;
                        break;
                }
            }
            
            foreach (var comp in componentsToDelete)
                GameObject.DestroyImmediate(comp, true);
        }
#endif
    }
}
