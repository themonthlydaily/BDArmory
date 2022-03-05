using UnityEngine;

using BDArmory.Weapons;

namespace BDArmory.Guidances
{
    public interface IGuidance
    {
        Vector3 GetDirection(MissileBase missile, Vector3 targetPosition, Vector3 targetVelocity);
    }
}