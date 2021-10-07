using KSP.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using BDArmory.Core.Utils;
using BDArmory.FX;
using BDArmory.UI;
using BDArmory.Core;

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

        PartResource fuel;
        PartResource solid;
        public bool isOnFire = false;

        public void Start()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                UI_FloatRange FBEditor = (UI_FloatRange)Fields["FireBottles"].uiControlEditor;
                FBEditor.onFieldChanged = FBSetup;
                origMass = part.mass;
                FBSetup(null, null);
            }
            fuel = part.Resources.Where(pr => pr.resourceName == "LiquidFuel").FirstOrDefault();
            solid = part.Resources.Where(pr => pr.resourceName == "SolidFuel").FirstOrDefault();

            var engine = part.FindModuleImplementing<ModuleEngines>();
            if (engine != null)
            {
                Events["ToggleTankOption"].guiActiveEditor = false;
                if (solid != null && engine.throttleLocked && !engine.allowShutdown)
                {
                    part.RemoveModule(this); //don't add firebottles to SRBs
                }
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
            if (HighLogic.LoadedSceneIsFlight && FlightGlobals.ready && vessel != null && !vessel.packed && !BDArmorySetup.GameIsPaused)
            {
                if (BDArmorySettings.BD_FIRES_ENABLED && BDArmorySettings.BD_FIRE_HEATDMG)
                {
                    if (((fuel != null && fuel.amount > 0) && part.temperature > 493) || ((solid != null && solid.amount > 0) && part.temperature > 600)) //autoignition temp of kerosene is 220 c
                    {
                        if (!isOnFire)
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
                            BulletHitFX.AttachFire(transform.position, part, 50, fireStarter);
                            Debug.Log("[SelfSealingTank] Fuel auto-ignition! " + part.name + " is on fire! Fuel quantity: " + fuel.amount + "; temperature: " + part.temperature);
                            isOnFire = true;
                        }
                    }
                }
            }
        }
    }
}
