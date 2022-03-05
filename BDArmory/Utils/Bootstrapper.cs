using UnityEngine;

using BDArmory.Damage;

namespace BDArmory.Utils
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class Bootstrapper : MonoBehaviour
    {
        private void Awake()
        {
            Dependencies.Register<DamageService, ModuleDamageService>();
        }
    }
}
