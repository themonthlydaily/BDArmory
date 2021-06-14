using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BDArmory.Misc
{
    /// <summary>
    /// A registry over all the asked for modules in all the asked for vessels.
    /// The lists are automatically updated whenever needed.
    /// Querying for a vessel or module that isn't yet in the registry causes the vessel or module to be added and tracked.
    /// 
    /// This removes the need for each module to scan for such modules, which often causes GC allocations and performance losses.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class VesselModuleRegistry : MonoBehaviour
    {
        static public VesselModuleRegistry Instance;
        static public Dictionary<Vessel, Dictionary<Type, List<UnityEngine.Object>>> registry;
        void Awake()
        {
            if (Instance != null)
            { Destroy(Instance); }
            Instance = this;

            if (registry == null)
            { registry = new Dictionary<Vessel, Dictionary<Type, List<UnityEngine.Object>>>(); }
        }

        void Start()
        {
            GameEvents.onVesselPartCountChanged.Add(OnVesselModified);
        }

        void OnDestroy()
        {
            GameEvents.onVesselPartCountChanged.Remove(OnVesselModified);
            registry.Clear();
        }

        /// <summary>
        /// Get an enumerator over the modules of the specified type in the specified vessel.
        /// </summary>
        /// <typeparam name="T">The module type to get.</typeparam>
        /// <param name="vessel">The vessel to get the modules from.</param>
        /// <returns></returns>
        public IEnumerable<T> GetModules<T>(Vessel vessel) where T : class
        {
            if (vessel == null) yield break;

            if (!registry.ContainsKey(vessel))
            { AddVesselToRegistry(vessel); }

            if (!registry[vessel].ContainsKey(typeof(T)))
            { UpdateVesselModulesInRegistry<T>(vessel); }

            yield return registry[vessel][typeof(T)].AsEnumerable() as T;
        }

        void AddVesselToRegistry(Vessel vessel)
        {
            registry.Add(vessel, new Dictionary<Type, List<UnityEngine.Object>>());
        }

        void AddVesselModuleTypeToRegistry<T>(Vessel vessel) where T : class
        {
            if (!registry[vessel].ContainsKey(typeof(T)))
            { registry[vessel].Add(typeof(T), new List<UnityEngine.Object>()); }
        }

        void UpdateVesselModulesInRegistry<T>(Vessel vessel) where T : class
        {
            if (!registry.ContainsKey(vessel)) { AddVesselToRegistry(vessel); }
            if (!registry[vessel].ContainsKey(typeof(T))) { AddVesselModuleTypeToRegistry<T>(vessel); }
            registry[vessel][typeof(T)].Clear();
            registry[vessel][typeof(T)].AddRange(vessel.FindPartModulesImplementing<T>().AsEnumerable() as IEnumerable<UnityEngine.Object>);
        }

        void UpdateVesselModulesInRegistryHelper<T>(Vessel vessel, T moduleTypeDynamicObject) where T : class
        {
            UpdateVesselModulesInRegistry<T>(vessel);
        }

        void OnVesselModified(Vessel vessel)
        {
            if (vessel == null || !vessel.loaded || vessel.packed) return;
            if (registry.ContainsKey(vessel))
            {
                foreach (var moduleType in registry[vessel].Keys)
                {
                    UpdateVesselModulesInRegistryHelper(vessel, moduleType);
                }
            }
        }

        public static void DumpRegistry()
        {
            Debug.Log("DEBUG Dumping vessel module registry:");
            foreach (var vessel in registry.Keys)
            {
                foreach (var moduleType in registry[vessel].Keys)
                {
                    Debug.Log($"DEBUG {vessel.vesselName} has {registry[vessel][moduleType].Count} modules of type {moduleType.Name}");
                }
            }
        }
    }
}