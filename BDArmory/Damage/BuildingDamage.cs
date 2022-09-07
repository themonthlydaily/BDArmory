using System;
using System.Collections.Generic;
using UnityEngine;

namespace BDArmory.Damage
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class BuildingDamage : MonoBehaviour
    {
        static List<DestructibleBuilding> buildingsDamaged = new List<DestructibleBuilding>();

        public static void RegisterDamage(DestructibleBuilding building)
        {
            if (!buildingsDamaged.Contains(building))
            {
                buildingsDamaged.Add(building);
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
                    for (int b = 0; b < buildingsDamaged.Count; b++)
                    {
                        if (!buildingsDamaged[b].IsIntact || buildingsDamaged[b] == null)
                        {
                            buildingsDamaged.Remove(buildingsDamaged[b]);
                            //Debug.Log("[BDArmory.BuildingDamage] building destroyed or null! Removing");
                        }
                        if (buildingsDamaged[b].FacilityDamageFraction > 100)
                        {
                            buildingsDamaged[b].FacilityDamageFraction -= RegenFactor;
                            //Debug.Log("[BDArmory.BuildingDamage] " + buildingsDamaged[b].name + " current HP: " + buildingsDamaged[b].FacilityDamageFraction);
                        }
                        else
                        {
                            //Debug.Log("[BDArmory.BuildingDamage] " + buildingsDamaged[b].name + " regenned to full HP, removing from list");
                            buildingsDamaged.RemoveAt(b);
                        }
                    }
                    buildingRegenTimer = 1;
                }
            }
        }
    }
}
