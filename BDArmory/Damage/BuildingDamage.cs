using System.Collections.Generic;
using UnityEngine;
/*
namespace BDArmory.Damage
{
    [KSPAddon(KSPAddon.Startup.MainMenu, false)]
    public class BuildingDamage : ScenarioDestructibles
    {

        public override void OnAwake()
        {
            Debug.Log("[BDArmory.BuildingDamage]: Modifying Buildings");

            foreach (KeyValuePair<string, ProtoDestructible> bldg in protoDestructibles)
            {
                using (var building = bldg.Value.dBuildingRefs.GetEnumerator())
                    while( building.MoveNext())
                    {
                        building.Current.damageDecay = 600f;
                        building.Current.impactMomentumThreshold *= 150; //this triggeres every time the menu is visited, oops
                    }
            }
        }
         //disabling this for now, as it isn't needed. I suspect, but haven't tested, that multipying the impact tolerance also affects how hard you have to collide into a building with a part/craft to destroy it...
    }
}
*/