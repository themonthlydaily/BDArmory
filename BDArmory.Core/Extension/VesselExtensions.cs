using System;
using System.Collections.Generic;
using UnityEngine;

namespace BDArmory.Core.Extension
{
    public static class VesselExtensions
    {
        public static bool InOrbit(this Vessel v)
        {
            try
            {
                if (v == null) return false;
                return !v.LandedOrSplashed &&
                           (v.situation == Vessel.Situations.ORBITING ||
                            v.situation == Vessel.Situations.SUB_ORBITAL ||
                            v.situation == Vessel.Situations.ESCAPING);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BDArmory.VesselExtensions]: Exception thrown in InOrbit: " + e.Message + "\n" + e.StackTrace);
                return false;
            }
        }

        public static bool InVacuum(this Vessel v)
        {
            return v.atmDensity <= 0.001f;
        }

        public static Vector3d Velocity(this Vessel v)
        {
            try
            {
                if (v == null) return Vector3d.zero;
                if (!v.InOrbit())
                {
                    return v.srf_velocity;
                }
                else
                {
                    return v.obt_velocity;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BDArmory.VesselExtensions]: Exception thrown in Velocity: " + e.Message + "\n" + e.StackTrace);
                //return v.srf_velocity;
                return new Vector3d(0, 0, 0);
            }
        }

        public static double GetFutureAltitude(this Vessel vessel, float predictionTime = 10)
        {
            Vector3 futurePosition = vessel.CoM + vessel.Velocity() * predictionTime
                                                + 0.5f * vessel.acceleration_immediate * Mathf.Pow(predictionTime, 2);

            return GetRadarAltitudeAtPos(futurePosition);
        }

        public static Vector3 GetFuturePosition(this Vessel vessel, float predictionTime = 10)
        {
            return vessel.CoM + vessel.Velocity() * predictionTime + 0.5f * vessel.acceleration_immediate * Math.Pow(predictionTime, 2);
        }

        public static float GetRadarAltitudeAtPos(Vector3 position)
        {
            double latitudeAtPos = FlightGlobals.currentMainBody.GetLatitude(position);
            double longitudeAtPos = FlightGlobals.currentMainBody.GetLongitude(position);

            float radarAlt = Mathf.Clamp(
                (float)(FlightGlobals.currentMainBody.GetAltitude(position) -
                        FlightGlobals.currentMainBody.TerrainAltitude(latitudeAtPos, longitudeAtPos)), 0,
                (float)FlightGlobals.currentMainBody.GetAltitude(position));
            return radarAlt;
        }

        // Get a vessel's "radius".
        public static float GetRadius(this Vessel vessel)
        {
            //get vessel size
            Vector3 size = vessel.vesselSize;

            //get largest dimension
            float radius;

            if (size.x > size.y && size.x > size.z)
            {
                radius = size.x / 2;
            }
            else if (size.y > size.x && size.y > size.z)
            {
                radius = size.y / 2;
            }
            else if (size.z > size.x && size.z > size.y)
            {
                radius = size.z / 2;
            }
            else
            {
                radius = size.x / 2;
            }

            return radius;
        }

        /// <summary>
        /// A more efficient (in terms of GC allocations) version of vessel.FindPartModulesImplementing<T>().
        /// Only allocates a single instance of T (due to IEnumerable's boxing/unboxing) instead of a List<T>.
        /// Running speed is slightly slower than the original version.
        /// 
        /// For best efficiency and performance, implement the function inline to avoid the boxing/unboxing cost. This avoids any GC allocations and runs around 2x faster.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="vessel"></param>
        /// <returns>An enumerator over the parts containing the module.</returns>
        public static IEnumerable<T> FindPartModulesImplementing2<T>(this Vessel vessel) where T : PartModule
        {
            foreach (var part in vessel.Parts)
            {
                foreach (var module in part.Modules)
                {
                    if (module != null && module is T)
                        yield return (T)module;
                }
            }
        }
    }
}
