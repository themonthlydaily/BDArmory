using BDArmory.CounterMeasure;
using BDArmory.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BDArmory.Modules
{
    //needs:
    //tie-in to TargetInfo; when cloaked need to reduce distance at which craft's TI can be aquired as a target
    //tie-in to Countermeasures, so AI pops cloak when getting shot at (guns, or missiles if RCS/heat reduction = true)
    //tie-in to ModuleWeapon/MissileFire, so autoFireCosAngle/FireAngle is substantially increased when trying to shoot at a cloaked target
    public class ModuleCloakingDevice : PartModule
    {
        [KSPField] public float CloakDuration = -1;

        [KSPField] public double resourceDrain = 1;

        [KSPField] public bool rcsReduction = false; //for sci-fi cloaking devices

        [KSPField] public float rcsReductionFactor = 1;

        [KSPField] public bool heatReduction = false; //for thermoptic camo

        [KSPField] public float heatReductionFactor = 1; //thermal signature reduction

        [KSPField(isPersistant = true, guiActive = true, guiName = "#LOC_BDArmory_Enabled")]//Enabled
        public bool cloakEnabled = false;

        private List<Part> CloakPartList;
        private float cloakTimer = -1;

        [KSPAction("Enable")]
        public void AGEnable(KSPActionParam param)
        {
            if (!cloakEnabled)
            {
                EnableCloak();
            }
        }

        [KSPAction("Disable")]
        public void AGDisable(KSPActionParam param)
        {
            if (cloakEnabled)
            {
                DisableCloak();
            }
        }

        [KSPAction("Toggle")]
        public void AGToggle(KSPActionParam param)
        {
            Toggle();
        }

        [KSPEvent(guiActiveEditor = false, guiActive = true, guiName = "#LOC_BDArmory_Toggle")]//Toggle
        public void Toggle()
        {
            if (cloakEnabled)
            {
                DisableCloak();
            }
            else
            {
                EnableCloak();
            }
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            if (!HighLogic.LoadedSceneIsFlight) return;
            part.force_activate();
            using (List<Part>.Enumerator craftPart = vessel.parts.GetEnumerator())
            {
                while (craftPart.MoveNext())
                {
                    CloakPartList.Add(craftPart.Current);
                }
            }
            //GameEvents.onVesselCreate.Add(OnVesselCreate);
        }

        void OnDestroy()
        {
            //GameEvents.onVesselCreate.Remove(OnVesselCreate);
            DisableCloak();
        }

        public void EnableCloak()
        {
            cloakEnabled = true;
        }

        public void DisableCloak()
        {
            cloakEnabled = false;
        }
        void Update()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                if (cloakTimer < 0) //have the cloak stuff proc once a sec
                {
                    using (List<Part>.Enumerator part = CloakPartList.GetEnumerator()) //using custom list instead of vessel.parts to grab parts no longer attached
                    {
                        while (part.MoveNext())
                        {
                            if (part.Current == null) continue;
                            if (cloakEnabled && (CloakDuration > 0 || CloakDuration == -1)) //time out cloak if generator has limited endurance
                            {
                                if (DrainEC(resourceDrain) && vessel.IsControllable) //have power and computer systems?
                                {
                                    part.Current.SetOpacity(0); //invisible
                                }
                                else //flat batteries/debris, reveal
                                {
                                    part.Current.SetOpacity(1);
                                }
                            }
                        }
                    }
                    cloakTimer = 1;
                }
                if (!BDArmorySetup.GameIsPaused)
                {
                    cloakTimer -= TimeWarp.fixedDeltaTime;
                }
            }
        }
        bool DrainEC(double consumptionRate)
        {
            if (consumptionRate != 0)
            {
                double chargeAvailable = part.RequestResource("ElectricCharge", consumptionRate, ResourceFlowMode.ALL_VESSEL);
                if (chargeAvailable < consumptionRate * 0.95f && !CheatOptions.InfiniteElectricity)
                {
                    return false;
                }
                else return true;
            }
            return false;
        }

        // RMB info in editor
        public override string GetInfo()
        {
            StringBuilder output = new StringBuilder();
            output.AppendLine($"EC/sec: {resourceDrain}");
            if (CloakDuration > 0)
            {
                output.AppendLine($"Cloak Duration: {CloakDuration} s");
            }
            if (heatReduction)
            {
                output.AppendLine($"Heat Signature reduction: {heatReduction}");
                if (heatReductionFactor < 1)
                {
                    output.AppendLine($" - factor: {heatReductionFactor}");
                }
            }
            if (rcsReduction)
            {
                output.AppendLine($"RCS reduction: {rcsReduction}");
                if (rcsReductionFactor < 1)
                {
                    output.AppendLine($" - factor: {rcsReductionFactor}");
                }
            }
            return output.ToString();
        }
    }
}
