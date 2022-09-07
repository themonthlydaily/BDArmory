using System;
using System.Collections.Generic;
using UnityEngine;

namespace BDArmory.Damage
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class BuildingDamage : MonoBehaviour
    {
        static Dictionary<DestructibleBuilding, float> buildingsDamaged = new Dictionary<DestructibleBuilding, float>();

        public static void RegisterDamage(DestructibleBuilding building)
        {
            if (!buildingsDamaged.ContainsKey(building))
            {
                buildingsDamaged.Add(building, building.FacilityDamageFraction);
                //Debug.Log("[BDArmory.BuildingDamage] registered " + building.name + " tracking " + buildingsDamaged.Count + " buildings");
            }
        }
        float buildingRegenTimer = 1; //regen 1 HP per second
        float RegenFactor = 1; //could always turn these into customizable settings if you want faster/slower healing buildings.
        void Update()
        {
            if (buildingsDamaged.Count > 0 && !HighLogic.LoadedSceneIsFlight)
            {
                buildingsDamaged.Clear();
            }
            if (UI.BDArmorySetup.GameIsPaused) return;

            if (buildingsDamaged.Count > 0)
            {
                buildingRegenTimer -= Time.fixedDeltaTime;
                if (buildingRegenTimer < 0)
                {
                    foreach (KeyValuePair<DestructibleBuilding, float> building in buildingsDamaged)
                    {
                        if (!building.Key.IsIntact)
                        {
                            buildingsDamaged.Remove(building.Key);
                            //Debug.Log("[BDArmory.BuildingDamage] building destroyed or null! Removing");
                        }
                        if (building.Key.FacilityDamageFraction > building.Value)
                        {
                            building.Key.FacilityDamageFraction -= RegenFactor;
                            //Debug.Log("[BDArmory.BuildingDamage] " + building.Key.name + " current HP: " + building.Key.FacilityDamageFraction);
                        }
                        else
                        {
                            //Debug.Log("[BDArmory.BuildingDamage] " + building.Key.name + " regenned to full HP, removing from list");
                            buildingsDamaged.Remove(building.Key);
                        }
                    }
                    buildingRegenTimer = 1;
                }
            }
        }
    }
}
