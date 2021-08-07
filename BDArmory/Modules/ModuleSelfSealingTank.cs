using KSP.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using BDArmory.Core.Utils;
using BDArmory.FX;

namespace BDArmory.Modules
{
    class ModuleSelfSealingTank : PartModule, IPartMassModifier
    {
        public float GetModuleMass(float baseMass, ModifierStagingSituation situation)
        {
            return partmass;
        }
        public ModifierChangeWhen GetModuleMassChangeWhen() => ModifierChangeWhen.FIXED;

        [KSPField(isPersistant = true)]
        public bool SSTank = false;

        [KSPEvent(advancedTweakable = true, guiActive = false, guiActiveEditor = true, guiName = "#LOC_BDArmory_SSTank", active = true)]//Self-Sealing Tank
        public void ToggleTankOption()
        {
            SSTank = !SSTank;

            if (SSTank == false)
            {
                Events["ToggleTankOption"].guiName = Localizer.Format("#LOC_BDArmory_SSTank_On");//"Enable self-sealing tank"

                using (IEnumerator<PartResource> resource = part.Resources.GetEnumerator())
                    while (resource.MoveNext())
                    {
                        if (resource.Current == null) continue;
                        resource.Current.maxAmount = Math.Floor(resource.Current.maxAmount * 1.11112);
                        resource.Current.amount = resource.Current.maxAmount;
                    }
            }
            else
            {
                Events["ToggleTankOption"].guiName = Localizer.Format("#LOC_BDArmory_SSTank_Off");//"Disable self-sealing tank"

                using (IEnumerator<PartResource> resource = part.Resources.GetEnumerator())
                    while (resource.MoveNext())
                    {
                        if (resource.Current == null) continue;
                        resource.Current.maxAmount *= 0.9;
                        resource.Current.amount = resource.Current.maxAmount;
                    }
            }
            Misc.Misc.RefreshAssociatedWindows(part);
        }

        [KSPField(advancedTweakable = true, guiActive = false, guiActiveEditor = true, guiName = "#LOC_BDArmory_AddedMass")]//CASE mass
        public float partmass = 0f;

        private float FBmass = 0f;
        private float origMass = 0f;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "#LOC_BDArmory_FireBottles"),//Fire Bottles

        UI_FloatRange(minValue = 0, maxValue = 5, stepIncrement = 1, scene = UI_Scene.All, affectSymCounterparts = UI_Scene.All)]
        public float FireBottles = 0;

        [KSPField(advancedTweakable = true, isPersistant = true, guiActive = true, guiName = "#LOC_BDArmory_FB_Remaining", guiActiveEditor = false), UI_Label(scene = UI_Scene.All)]
        public float FBRemaining;

        public void Start()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                UI_FloatRange FBEditor = (UI_FloatRange)Fields["FireBottles"].uiControlEditor;
                FBEditor.onFieldChanged = FBSetup;
                origMass = part.mass;
                FBSetup(null, null);
            }
            var engine = part.FindModuleImplementing<ModuleEngines>();
            if (engine != null)
            {
                Events["ToggleTankOption"].guiActiveEditor = false;
            }
        }
        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight) return;

            if (part.partInfo != null)
            {
                if (HighLogic.LoadedSceneIsEditor)
                {
                    FBSetup(null, null);
                }
                else
                {
                    if (part.vessel != null)
                    {
                        var SSTString = ConfigNodeUtils.FindPartModuleConfigNodeValue(part.partInfo.partConfig, "ModuleSelfSealingTank", "SSTank");
                        if (!string.IsNullOrEmpty(SSTString))
                        {
                            try
                            {
                                SSTank = bool.Parse(SSTString);
                                FBSetup(null, null);
                            }
                            catch (Exception e)
                            {
                                Debug.LogError("[BDArmory.ModuleSelfSealingTank]: Exception parsing SSTank: " + e.Message);
                            }
                        }
                        else
                        {
                            SSTank = false;
                        }
                    }
                    else
                    {
                        enabled = false;
                    }
                }
            }
        }
        void FBSetup(BaseField field, object obj)
        {
            FBmass = (0.01f * FireBottles);
            FBRemaining = FireBottles;
            partmass = FBmass;
            //part.mass = partmass;
            Misc.Misc.RefreshAssociatedWindows(part);
        }
        public override string GetInfo()
        {
            StringBuilder output = new StringBuilder();
            output.Append(Environment.NewLine);
            output.AppendLine($" Can outfit part with Fire Suppression Systems."); //localize this at some point, future me
            var engine = part.FindModuleImplementing<ModuleEngines>();
            var engineFX = part.FindModuleImplementing<ModuleEnginesFX>();
            if (engine == null || engineFX == null)
            {
                output.AppendLine($" Can upgrade to Self-Sealing Tank.");
            }
            output.AppendLine("");

            return output.ToString();
        }
        void Update()
        {
            if (HighLogic.LoadedSceneIsFlight && FlightGlobals.ready && vessel != null && !vessel.packed)
            {
                if (this.part.temperature > 493) //autoignition temp of kerosene is 220 c
                {
                    var isOnFire = part.GetComponentInChildren<FireFX>();
                    if (isOnFire == null)
                    {
                        string fireStarter;
                        var vesselFire = part.vessel.GetComponentInChildren<FireFX>();
                        if (vesselFire != null)
                        {
                            fireStarter = vesselFire.SourceVessel;
                        }
                        else
                        {
                            fireStarter = part.vessel.GetName();
                        }
                        Vector3 firePosition = part.transform.up * 10;
                        Ray LoSRay = new Ray(transform.position, (transform.position + firePosition) - transform.position);
                        RaycastHit hit;
                        if (Physics.Raycast(LoSRay, out hit, 10, 9076737)) // only add fires to parts in LoS of blast
                        {
                            BulletHitFX.AttachFire(hit, part, 50, fireStarter);
                        }
                        Debug.Log("[SelfSealingTank] Fuel auto-ignition! " + part.name + " is on fire!");
                    }
                }
            }
        }
    }
}
