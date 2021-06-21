using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace BDArmory.Modules
{
    public class ModuleSpaceFriction : PartModule
    {
        [KSPField]
        private double frictionCoeff = 1.0f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Space Friction"), UI_Toggle(disabledText = "Disabled", enabledText = "Enabled", scene = UI_Scene.All, affectSymCounterparts = UI_Scene.All)]
        public bool FrictionEnabled = true;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "CounterGrav"), UI_Toggle(disabledText = "Disabled", enabledText = "Enabled", scene = UI_Scene.All, affectSymCounterparts = UI_Scene.All)]
        public bool AntiGravEnabled = true;

        public float maxVelocity = 300;

        [KSPField(isPersistant = true)]
        public float frictMult;

        public float driftMult = 2;

        private bool BDAcraft = false;
        public static bool GameIsPaused
        {
            get { return PauseMenu.isOpen || Time.timeScale == 0; }
        }
        BDModulePilotAI AI;
        public BDModulePilotAI pilot
        {
            get
            {
                if (AI) return AI;
                AI = VesselModuleRegistry.GetBDModulePilotAI(vessel, true); // FIXME should this be IBDAIControl?
                return AI;
            }
        }
        void Start()
        {
            /*
            if (HighLogic.LoadedSceneIsEditor)
            {
                if (pilot != null)
                {
                    Fields["FrictionEnabled"].guiActive = false;
                    Fields["FrictionEnabled"].guiActiveEditor = false;
                    Fields["AntiGravEnabled"].guiActive = false;
                    Fields["AntiGravEnabled"].guiActiveEditor = false;
                }
            }*/
            if (HighLogic.LoadedSceneIsFlight)
            {
                using (List<Part>.Enumerator part = this.vessel.Parts.GetEnumerator())
                    while (part.MoveNext())
                    {
                        if (part.Current == null) continue;
                        if (part.Current.isEngine())
                        {
                            var engine = part.Current.FindModuleImplementing<ModuleEngines>();
                            frictMult += engine.maxThrust; //double check this respects thrustLimiter
                                                           //maybe changethis to active engines, but since all engines should be activating at roundstart...
                        }
                    }
                if (pilot != null)
                {
                    BDAcraft = true;
                }
            }
            //driftmult = BDArmorySettings.SF_DragMult;
        }

        public void FixedUpdate()
        {
            if (HighLogic.LoadedSceneIsFlight && FlightGlobals.ready && !this.vessel.packed && !GameIsPaused)
            {
                if (this.part.vessel.situation == Vessel.Situations.FLYING || this.part.vessel.situation == Vessel.Situations.SUB_ORBITAL)
                {
                    if (FrictionEnabled)
                    {
                        if (this.part.vessel.speed > 10)
                        {
                            if (pilot != null)
                            {
                                maxVelocity = pilot.maxSpeed;
                            }
                            frictionCoeff = Mathf.Pow(((float)part.vessel.speed / maxVelocity), 3) * frictMult; //at maxSpeed, have friction be 100% of vessel's engines thrust
                                                                                                                //if (Vector3.Angle(this.part.vessel.srf_vel_direction, this.part.vessel.GetTransform().up) > 90)
                                                                                                                //{
                            frictionCoeff += (Vector3.Angle(this.part.vessel.srf_vel_direction, this.part.vessel.GetTransform().up) / 180) * driftMult; //greater AoA, greater drag
                            //}
                            part.vessel.rootPart.rb.AddForceAtPosition((-part.vessel.srf_vel_direction * frictionCoeff), part.vessel.CoM, ForceMode.Acceleration);
                        }
                    }
                    if (AntiGravEnabled) //have this disabled if no engines left?
                    {
                        if ((BDAcraft && pilot != null) || //have pilotless craft fall
                            (!BDAcraft))
                        {
                            /*
                            using (List<Part>.Enumerator part = this.part.vessel.Parts.GetEnumerator())
                                while (part.MoveNext())
                                {
                                    if (part.Current == null) continue;
                                    if (part.Current.PhysicsSignificance == 1) continue; //these don't have rigidbodies and will NRE if force applied

                                    part.Current.rb.AddForce(-FlightGlobals.getGeeForceAtPosition(part.Current.transform.position), ForceMode.Acceleration);
                                }
                            */
                            for (int i = 0; i < part.vessel.Parts.Count; i++)
                            {
                                if (part.vessel.parts[i].PhysicsSignificance != 1)
                                {
                                    part.vessel.Parts[i].Rigidbody.AddForce(-FlightGlobals.getGeeForceAtPosition(part.vessel.Parts[i].transform.position), ForceMode.Acceleration);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
