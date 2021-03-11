using BDArmory.FX;
using BDArmory.UI;
using System.Linq;
using UnityEngine;

namespace BDArmory.Modules
{
    public class ModuleDrainIntakes : PartModule
    {
        public float drainRate = 999;
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
                if (drainDuration > 0)
                {
                    part.RequestResource("IntakeAir", (double)(drainRate * Time.deltaTime), ResourceFlowMode.ALL_VESSEL);
                    part.RequestResource("IntakeAtm", (double)(drainRate * Time.deltaTime), ResourceFlowMode.ALL_VESSEL);
                }
                else
                {
                    part.RemoveModule(this);
                }
            }
        }
    }
}

