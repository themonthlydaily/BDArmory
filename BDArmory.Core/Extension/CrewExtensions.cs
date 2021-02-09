namespace BDArmory.Core.Extension
{
    public static class CrewExtensions
    {
        /// <summary>
        /// Reset the inventory of a crew member to the default of a chute and jetpack.
        /// </summary>
        /// <param name="crew">The crew member</param>
        public static void ResetInventory(this ProtoCrewMember crew)
        {
            if ((Versioning.version_major == 1 && Versioning.version_minor > 10) || Versioning.version_major > 1) // Introduced in 1.11
            {
                crew.SetDefaultInventory(); // Reset the inventory to a chute and a jetpack.
            }
            else // Nothing, crew didn't have inventory before. Chute and jetpack were built into KerbalEVA class.
            {
            }
        }
    }
}