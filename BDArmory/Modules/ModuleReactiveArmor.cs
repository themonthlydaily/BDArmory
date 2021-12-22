using System;
using System.Collections.Generic;
using System.Linq;
using BDArmory.Core;
using BDArmory.Core.Extension;
using BDArmory.Core.Module;
using BDArmory.FX;
using UnityEngine;

namespace BDArmory.Modules
{
    public class ModuleReactiveArmor : PartModule
    {
        [KSPField]
        public string sectionTransformName = "sections";

        [KSPField]
        public string armorName = "Reactive Armor";

        Transform[] sections;

        [KSPField]
        public bool NXRA = false; //non-explosive reactive armor?

        [KSPField]
        public float sensitivity = 30; //minimum caliber to trigger RA

        [KSPField]
        public float armorModifier = 1.25f; //armor thickness modifier

        public int sectionsRemaining = 1;
        private int sectionsCount = 1;

        Vector3 direction = default(Vector3);

        private string ExploModelPath = "BDArmory/Models/explosion/CASEexplosion";
        private string explSoundPath = "BDArmory/Sounds/explode1";
        public string SourceVessel = "";

        public void Start()
        {
            MakeArmorSectionArray();
            //UpdateSectionScales();
            if (HighLogic.LoadedSceneIsFlight)
            {
                SourceVessel = part.vessel.GetName();
            }
        }
        void MakeArmorSectionArray()
        {
            Transform segmentsTransform = part.FindModelTransform(sectionTransformName);
            sectionsCount = segmentsTransform.childCount;
            sections = new Transform[sectionsCount];
            for (int i = 0; i < sectionsCount; i++)
            {
                string sectionName = segmentsTransform.GetChild(i).name;
                int sectionIndex = int.Parse(sectionName.Substring(8)) - 1;
                sections[sectionIndex] = segmentsTransform.GetChild(i);
            }
            sections.Shuffle(); //randomize order sections get removed
            sectionsRemaining = sectionsCount;
            var HP = part.FindModuleImplementing<HitpointTracker>();
            if (HP != null)
            {
                HP.maxHitPoints = (sectionsCount * 300f); //set HP based on number of sections
                HP.Hitpoints = (sectionsCount * 300f); 
                HP.SetupPrefab(); //and update hitpoint slider
            }
        }

        public void UpdateSectionScales()
        {
            direction = -sections[sectionsRemaining-1].up; 

            ExplosionFx.CreateExplosion(sections[sectionsRemaining - 1].transform.position, 1, ExploModelPath, explSoundPath, ExplosionSourceType.BattleDamage, 30, part, SourceVessel, armorName, direction, 30, true);
            if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[ReactiveArmor] removing section, " + sectionsRemaining + " sections left");
            sectionsRemaining--;
            if (sectionsRemaining < 1)
            {
                part.Destroy();
            }
            else
            {
                var HP = part.FindModuleImplementing<HitpointTracker>();
                if (HP != null)
                {
                    HP.Hitpoints = Mathf.Clamp(HP.Hitpoints, 0, sectionsRemaining * 300f);
                }
                if (HP.Hitpoints < 0) part.Destroy();
            }
            for (int i = 0; i < sectionsCount; i++)
            {
                if (i < sectionsRemaining) sections[i].localScale = Vector3.one;
                else sections[i].localScale = Vector3.zero;
            }
        }
    }
}
