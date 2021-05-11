using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BDArmory.Core;
using BDArmory.Misc;

namespace BDArmory.Control
{
    public class Asteroids : MonoBehaviour
    {
        public static HashSet<Vessel> managedAsteroids;
        protected static UntrackedObjectClass[] UntrackedObjectClasses = (UntrackedObjectClass[])Enum.GetValues(typeof(UntrackedObjectClass)); // Get the UntrackedObjectClasses as an array of enum values.

        protected float density;
        protected float altitude;
        protected float radius;

        protected virtual void Awake()
        {
            if (managedAsteroids == null)
            {
                managedAsteroids = new HashSet<Vessel>();
            }
        }

        protected void SetupAsteroids(float _density, float _altitude, float _radius)
        {
            density = _density;
            altitude = _altitude * 1000f;
            radius = _radius * 1000f;
        }
    }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class AsteroidRain : Asteroids
    {
        public static AsteroidRain Instance;
        protected override void Awake()
        {
            if (Instance)
                Destroy(Instance);
            Instance = this;
            base.Awake();
        }

        int untrackedObjectClassIndex = 0;

        public void SpawnRain(float _density, float _altitude, float _radius)
        {
            // if (BDArmorySettings.DRAW_DEBUG_LABELS)
            SetupAsteroids(_density, _altitude, _radius);
            Debug.Log($"[BDArmory.Asteroids]: Spawning asteroid rain with density {density}, altitude {altitude}km and radius {radius}km.");
            var RNG = new System.Random();
            var spawnOrbit = new Orbit(FlightGlobals.ActiveVessel.orbit);
            Debug.Log($"BDArmory.DEBUG Spawning asteroid of class {UntrackedObjectClasses[untrackedObjectClassIndex]} in orbit: inc {spawnOrbit.inclination}, e {spawnOrbit.eccentricity}, sma {spawnOrbit.semiMajorAxis}, lan {spawnOrbit.LAN}, argPe {spawnOrbit.argumentOfPeriapsis}, mEp {spawnOrbit.meanAnomalyAtEpoch}, t {spawnOrbit.ObT}, body {spawnOrbit.referenceBody}"); // FIXME These parameters aren't giving the same orbit!
            // spawnOrbit = new Orbit(spawnOrbit.inclination, spawnOrbit.eccentricity, spawnOrbit.semiMajorAxis, spawnOrbit.LAN, spawnOrbit.argumentOfPeriapsis, spawnOrbit.meanAnomalyAtEpoch, -spawnOrbit.ObT, FlightGlobals.currentMainBody);
            spawnOrbit.semiMajorAxis += 2f * altitude;
            // Debug.Log($"BDArmory.DEBUG new orbit: inc {spawnOrbit.inclination}, e {spawnOrbit.eccentricity}, sma {spawnOrbit.semiMajorAxis}, lan {spawnOrbit.LAN}, argPe {spawnOrbit.argumentOfPeriapsis}, mEp {spawnOrbit.meanAnomalyAtEpoch}, t {spawnOrbit.ObT}, body {spawnOrbit.referenceBody}");
            // spawnOrbit = new Orbit(spawnOrbit.inclination, 1, (FlightGlobals.currentMainBody.Radius + altitude) / 2f, spawnOrbit.LAN, spawnOrbit.argumentOfPeriapsis, spawnOrbit.meanAnomalyAtEpoch, 0, FlightGlobals.currentMainBody);
            var asteroid = DiscoverableObjectsUtil.SpawnAsteroid(DiscoverableObjectsUtil.GenerateAsteroidName(), spawnOrbit, (uint)RNG.Next(), UntrackedObjectClasses[untrackedObjectClassIndex++], 100, 200);
            untrackedObjectClassIndex %= UntrackedObjectClasses.Length;
            if (asteroid != null && asteroid.vesselRef != null)
            {
                var upDirection = -FlightGlobals.getGeeForceAtPosition(asteroid.vesselRef.transform.position).normalized;
                var refDirection = Math.Abs(Vector3.Dot(Vector3.up, upDirection)) < 0.9f ? Vector3.up : Vector3.forward; // Avoid that the reference direction is colinear with the local surface normal.
                var direction = Vector3.ProjectOnPlane(Quaternion.AngleAxis((float)RNG.NextDouble(), upDirection) * refDirection, upDirection).normalized;
                var distance = (float)RNG.NextDouble() * radius;
                var position = altitude * upDirection + direction * distance;
                asteroid.vesselRef.SetPosition(position);
                managedAsteroids.Add(asteroid.vesselRef);
                Debug.Log($"DEBUG Vpos: {FlightGlobals.ActiveVessel.transform.position}, Apos: {asteroid.vesselRef.transform.position}");
                DelayedSwitchVessel(asteroid.vesselRef);
            }
            else
            {
                Debug.Log("DEBUG Failed to spawn asteroid.");
            }
        }

        IEnumerator DelayedSwitchVessel(Vessel v)
        {
            while (v != null && (!v.loaded || v.packed)) yield return new WaitForFixedUpdate();
            if (v != null) FlightGlobals.ForceSetActiveVessel(v);
        }
    }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class AsteroidField : Asteroids
    {
        public static AsteroidField Instance;
        protected override void Awake()
        {
            if (Instance)
                Destroy(Instance);
            Instance = this;
        }

        public void SpawnField(float density, float altitude, float radius)
        {
            if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.Asteroids]: Spawning asteroid field with density {density}, altitude {altitude}km and radius {radius}km.");
            SetupAsteroids(density, altitude, radius);
        }
    }
}