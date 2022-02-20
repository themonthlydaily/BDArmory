using System.Collections.Generic;

namespace BDArmory.Modules
{
    public class ModuleSpaceRadar : ModuleRadar
    {
        public void FixedUpdate() // runs every frame FIXME If this should run slower, then use a timer, not Update.
        {
            if (HighLogic.LoadedSceneIsFlight) // if in the flight scene
            {
                UpdateRadar(); // run the UpdateRadar code
            }
        }

        // This code determines if the radar is below the cutoff altitude and if so then
        // it disables the radar ... private so that it cannot be accessed by any other code
        private void UpdateRadar()
        {
            if (vessel.atmDensity >= 0.007) // below an atm density of 0.007 the radar will not work
            { // FIXME Everything below here is going to run every frame when in atmosphere. There must be a more efficient way of doing this.
                var radarParts = VesselModuleRegistry.GetModules<ModuleSpaceRadar>(vessel);
                if (radarParts == null) return;
                foreach (ModuleSpaceRadar radarPart in radarParts) // for each of the parts in the list do the following
                {
                    if (radarPart != null && radarPart.radarEnabled)
                    {
                        DisableRadar(); // disable the radar
                    }
                }
            }
        }
    }
}
