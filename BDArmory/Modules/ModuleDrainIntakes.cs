using BDArmory.FX;
using BDArmory.UI;
using System.Linq;
using UnityEngine;

namespace BDArmory.Modules
{
    public class ModuleDrainIntakes : PartModule
    {
        public float drainDuration = 20;

        public override void OnStart(StartState state)
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                part.force_activate();
            }
            base.OnStart(state);
        }

        public void Update()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                drainDuration -= Time.deltaTime;
                var Intake = part.FindModuleImplementing<ModuleResourceIntake>();
                if (drainDuration > 0)
                {
                    Intake.intakeEnabled = false;
                }
                else
                {
                    Intake.intakeEnabled = true;
                    part.RemoveModule(this);
                }
                
            }
        }
    }
}

