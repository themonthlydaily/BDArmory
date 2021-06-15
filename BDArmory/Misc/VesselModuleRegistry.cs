using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BDArmory.Core;

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
        #region Fields
        static public VesselModuleRegistry Instance;
        static public Dictionary<Vessel, Dictionary<Type, List<UnityEngine.Object>>> registry;
        static public Dictionary<Type, System.Reflection.MethodInfo> updateModuleCallbacks;
        #endregion

        #region Monobehaviour methods
        void Awake()
        {
            if (Instance != null) { Destroy(Instance); }
            Instance = this;

            if (registry == null) { registry = new Dictionary<Vessel, Dictionary<Type, List<UnityEngine.Object>>>(); }
            if (updateModuleCallbacks == null) { updateModuleCallbacks = new Dictionary<Type, System.Reflection.MethodInfo>(); }
        }

        void Start()
        {
            GameEvents.onVesselPartCountChanged.Add(OnVesselModified);
        }

        void OnDestroy()
        {
            GameEvents.onVesselPartCountChanged.Remove(OnVesselModified);
            registry.Clear();
            updateModuleCallbacks.Clear();
        }
        #endregion

        #region Private methods
        /// <summary>
        /// Add a vessel to track to the registry.
        /// </summary>
        /// <param name="vessel">The vessel.</param>
        void AddVesselToRegistry(Vessel vessel)
        {
            registry.Add(vessel, new Dictionary<Type, List<UnityEngine.Object>>());
        }

        /// <summary>
        /// Add a module type to track to a vessel in the registry.
        /// </summary>
        /// <typeparam name="T">The module type to track.</typeparam>
        /// <param name="vessel">The vessel.</param>
        void AddVesselModuleTypeToRegistry<T>(Vessel vessel) where T : class
        {
            if (!registry[vessel].ContainsKey(typeof(T)))
            {
                registry[vessel].Add(typeof(T), new List<UnityEngine.Object>());
                updateModuleCallbacks[typeof(T)] = typeof(VesselModuleRegistry).GetMethod(nameof(VesselModuleRegistry.UpdateVesselModulesInRegistry), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).MakeGenericMethod(typeof(T));
            }
        }

        /// <summary>
        /// Update the list of modules of the given type in the registry for the given vessel.
        /// </summary>
        /// <typeparam name="T">The module type.</typeparam>
        /// <param name="vessel">The vessel.</param>
        void UpdateVesselModulesInRegistry<T>(Vessel vessel) where T : class
        {
            if (!registry.ContainsKey(vessel)) { AddVesselToRegistry(vessel); }
            if (!registry[vessel].ContainsKey(typeof(T))) { AddVesselModuleTypeToRegistry<T>(vessel); }
            registry[vessel][typeof(T)] = vessel.FindPartModulesImplementing<T>().ConvertAll(m => m as UnityEngine.Object);
            if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log($"[BDArmory.VesselModuleRegistry]: Registry entry for {vessel.vesselName} updated to have {registry[vessel][typeof(T)].Count} modules of type {typeof(T).Name}.");
        }

        /// <summary>
        /// Update the registry entries when a tracked vessel gets modified.
        /// </summary>
        /// <param name="vessel">The vessel that was modified.</param>
        void OnVesselModified(Vessel vessel)
        {
            if (vessel == null || !vessel.loaded || vessel.packed) return;
            if (registry.ContainsKey(vessel))
            {
                foreach (var moduleType in registry[vessel].Keys.ToArray())
                {
                    // Get the method using reflection. This is needed as Unity doesn't support the dynamic keyword.
                    updateModuleCallbacks[moduleType].Invoke(this, new object[1] { vessel });
                }
            }
        }
        #endregion

        #region Public methods
        /// <summary>
        /// Get an enumerable over the modules of the specified type in the specified vessel.
        /// This is about 15-30 times faster than FindPartModulesImplementing, but still requires around the same amount of GC allocations due to boxing/unboxing.
        /// </summary>
        /// <typeparam name="T">The module type to get.</typeparam>
        /// <param name="vessel">The vessel to get the modules from.</param>
        /// <returns>An enumerable for use in foreach loops or .ToList calls if the vessel exists, else null.</returns>
        public IEnumerable<T> GetModules<T>(Vessel vessel) where T : class
        {
            if (vessel == null) return null;

            if (!registry.ContainsKey(vessel))
            { AddVesselToRegistry(vessel); }

            if (!registry[vessel].ContainsKey(typeof(T)))
            { UpdateVesselModulesInRegistry<T>(vessel); }

            return registry[vessel][typeof(T)].ConvertAll(m => m as T).AsEnumerable();
        }

        /// <summary>
        /// Get the first module of the specified type in the specified vessel.
        /// </summary>
        /// <typeparam name="T">The module type.</typeparam>
        /// <param name="vessel">The vessel.</param>
        /// <returns>The first module if it exists, else null.</returns>
        public T GetModule<T>(Vessel vessel) where T : class
        {
            var modules = GetModules<T>(vessel);
            return modules == null ? null : modules.FirstOrDefault();
        }

        /// <summary>
        /// Debugging: dump the registry to the log file.
        /// </summary>
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
        #endregion
    }
}