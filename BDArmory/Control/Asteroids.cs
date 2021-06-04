using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BDArmory.Core;
using BDArmory.Misc;
using BDArmory.UI;

namespace BDArmory.Control
{
    public class Asteroids : MonoBehaviour
    {
        public static HashSet<Vessel> managedAsteroids;
        protected static UntrackedObjectClass[] UntrackedObjectClasses = (UntrackedObjectClass[])Enum.GetValues(typeof(UntrackedObjectClass)); // Get the UntrackedObjectClasses as an array of enum values.

        #region Fields
        protected float altitude;
        protected float radius;
        protected Vector2d geoCoords;
        protected System.Random RNG;
        #endregion

        protected virtual void Awake()
        {
            if (managedAsteroids == null)
            {
                managedAsteroids = new HashSet<Vessel>();
            }
            if (RNG == null)
            {
                RNG = new System.Random();
            }
        }

        void OnDestroy()
        {
            StopAllCoroutines();
            foreach (var asteroid in managedAsteroids)
            {
                Destroy(asteroid);
            }
            managedAsteroids.Clear();
        }

        /// <summary>
        /// Set up common parameters for derived classes.
        /// </summary>
        /// <param name="_altitude"></param>
        /// <param name="_radius"></param>
        /// <param name="_geoCoords"></param>
        protected void SetupAsteroids(float _altitude, float _radius, Vector2d _geoCoords)
        {
            altitude = _altitude < 10f ? _altitude * 100f : (_altitude - 9f) * 1000f;
            radius = _radius * 1000f;
            geoCoords = _geoCoords;
            //  = new FloatCurve(new Keyframe[] { new Keyframe(0f, 0f), new Keyframe(1f, 0f) }); // Set the asteroid spawn chance to 0. FIXME we need to somehow stop the regular asteroids from spawning as they trigger the CollisionManager to pause the game for a while.
        }

        /// <summary>
        /// Spawn an asteroid of the given class at the given position.
        /// </summary>
        /// <param name="position">The position to spawn the asteroid.</param>
        /// <param name="untrackedObjectClassIndex">The class of the asteroid. -1 picks one at random.</param>
        /// <returns>The asteroid vessel.</returns>
        protected Vessel SpawnAsteroid(Vector3d position, int untrackedObjectClassIndex = -1)
        {
            if (untrackedObjectClassIndex < 0)
            {
                untrackedObjectClassIndex = RNG.Next(UntrackedObjectClasses.Length);
            }
            var asteroid = DiscoverableObjectsUtil.SpawnAsteroid(DiscoverableObjectsUtil.GenerateAsteroidName(), GetOrbitForApoapsis2(position), (uint)RNG.Next(), UntrackedObjectClasses[untrackedObjectClassIndex], 0, BDArmorySettings.COMPETITION_DURATION);
            if (asteroid != null && asteroid.vesselRef != null)
            { return asteroid.vesselRef; }
            else
            { return null; }
        }

        /// <summary>
        /// Calculate an orbit that has the specified position as the apoapsis and orbital velocity that matches that of the ground below.
        /// This doesn't quite give the correct orbits for some reason, use GetOrbitForApoapsis2 instead.
        /// </summary>
        /// <param name="position">The position of the apoapsis.</param>
        /// <returns>The orbit.</returns>
        Orbit GetOrbitForApoapsis(Vector3d position)
        {
            // FIXME this is still giving orbits that are slightly off, e.g., an asteroid field at the KSC is coming out oval instead of round.
            // Figure out the orbit of an asteroid with apoapsis at the spawn point and the same velocity as that of the surface under the spawn point.
            double latitude, longitude, altitude;
            FlightGlobals.currentMainBody.GetLatLonAlt(position, out latitude, out longitude, out altitude);
            longitude = (longitude + FlightGlobals.currentMainBody.rotationAngle + 180d) % 360d; // Compensate coordinates for planet rotation then normalise to 0°—360°.
            var inclination = Math.Abs(latitude);
            var apoapsisAltitude = FlightGlobals.currentMainBody.Radius + altitude;
            var velocity = 2d * Math.PI * (FlightGlobals.currentMainBody.Radius + altitude) * Math.Cos(Mathf.Deg2Rad * latitude) / FlightGlobals.currentMainBody.rotationPeriod;
            var semiMajorAxis = -FlightGlobals.currentMainBody.gravParameter / (velocity * velocity / 2d - FlightGlobals.currentMainBody.gravParameter / apoapsisAltitude) / 2d;
            var eccentricity = apoapsisAltitude / semiMajorAxis - 1d;
            var upDirection = (FlightGlobals.currentMainBody.GetWorldSurfacePosition(latitude, longitude, altitude) - FlightGlobals.currentMainBody.transform.position).normalized;
            var longitudeOfAscendingNode = (Mathf.Rad2Deg * Mathf.Acos(Vector3.Dot(Vector3.Cross(upDirection, Vector3d.Cross(Vector3d.up, upDirection)).normalized, Vector3.forward)) + longitude + (latitude > 0 ? 0d : 180d)) % 360d;
            var argumentOfPeriapsis = latitude < 0d ? 90d : 270d;
            var meanAnomalyAtEpoch = Math.PI;
            return new Orbit(inclination, eccentricity, semiMajorAxis, longitudeOfAscendingNode, argumentOfPeriapsis, meanAnomalyAtEpoch, Planetarium.GetUniversalTime(), FlightGlobals.currentMainBody);
        }

        /// <summary>
        /// Calculate an orbit that has the specified position as the apoapsis and orbital velocity that matches that of the ground below.
        /// This one gives the correct orbit to within around float precision.
        /// </summary>
        /// <param name="position">The position of the apoapsis.</param>
        /// <returns>The orbit.</returns>
        Orbit GetOrbitForApoapsis2(Vector3d position)
        {
            double latitude, longitude, altitude;
            FlightGlobals.currentMainBody.GetLatLonAlt(position, out latitude, out longitude, out altitude);
            longitude = (longitude + FlightGlobals.currentMainBody.rotationAngle + 180d) % 360d; // Compensate coordinates for planet rotation then normalise to 0°—360°.
            var orbitVelocity = FlightGlobals.currentMainBody.getRFrmVel(position);
            var orbitPosition = position - FlightGlobals.currentMainBody.transform.position;
            var orbit = new Orbit();
            orbit.UpdateFromStateVectors(orbitPosition.xzy, orbitVelocity.xzy, FlightGlobals.currentMainBody, Planetarium.GetUniversalTime());
            return orbit;
        }

        /// <summary>
        /// Debugging: Compare orbit of current vessel with that of the generated ones.
        /// </summary>
        public void CheckOrbit()
        {
            if (FlightGlobals.ActiveVessel == null) { return; }
            var v = FlightGlobals.ActiveVessel;
            var orbit = FlightGlobals.ActiveVessel.orbit;
            Debug.Log($"DEBUG orbit.pos: {orbit.pos}, orbit.vel: {orbit.vel}");
            Debug.Log($"DEBUG       pos: {(v.CoM - (Vector3d)v.mainBody.transform.position).xzy},       vel: {v.mainBody.getRFrmVel(v.CoM).xzy}");
            Debug.Log($"DEBUG Δpos: {orbit.pos - ((Vector3d)v.CoM - (Vector3d)v.mainBody.transform.position).xzy}, Δvel: {orbit.vel - v.mainBody.getRFrmVel(v.CoM).xzy}");
            Debug.Log($"DEBUG Current vessel's orbit: inc: {orbit.inclination}, e: {orbit.eccentricity}, sma: {orbit.semiMajorAxis}, lan: {orbit.LAN}, argPe: {orbit.argumentOfPeriapsis}, mEp: {orbit.meanAnomalyAtEpoch}");
            orbit = GetOrbitForApoapsis(v.CoM);
            Debug.Log($"DEBUG Predicted orbit:        inc: {orbit.inclination}, e: {orbit.eccentricity}, sma: {orbit.semiMajorAxis}, lan: {orbit.LAN}, argPe: {orbit.argumentOfPeriapsis}, mEp: {orbit.meanAnomalyAtEpoch}");
            orbit = GetOrbitForApoapsis2(v.CoM);
            Debug.Log($"DEBUG Predicted orbit 2:      inc: {orbit.inclination}, e: {orbit.eccentricity}, sma: {orbit.semiMajorAxis}, lan: {orbit.LAN}, argPe: {orbit.argumentOfPeriapsis}, mEp: {orbit.meanAnomalyAtEpoch}");
        }
    }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class AsteroidRain : Asteroids
    {
        public static AsteroidRain Instance;
        bool raining = true;
        float density;
        Coroutine rainCoroutine;
        Coroutine cleanUpCoroutine;
        HashSet<Vessel> beingRemoved = new HashSet<Vessel>();

        // Pooling of asteroids
        List<Vessel> asteroidPool;
        int lastPoolIndex = 0;

        protected override void Awake()
        {
            if (Instance)
                Destroy(Instance);
            Instance = this;
            base.Awake();
        }

        void OnDestroy()
        {
            if (asteroidPool != null)
                foreach (var asteroid in asteroidPool)
                    if (asteroid != null)
                        Destroy(asteroid);
        }

        public void Reset()
        {
            raining = false;
            if (rainCoroutine != null)
            { StopCoroutine(rainCoroutine); }
            // if (cleanUpCoroutine != null)
            // { StopCoroutine(cleanUpCoroutine); }
            // managedAsteroids.Clear();
            // beingRemoved.Clear();
        }

        public void SpawnRain(float _density, float _altitude, float _radius, Vector2d _geoCoords)
        {
            density = _density;
            SetupAsteroids(_altitude, _radius, _geoCoords);
            Debug.Log($"[BDArmory.Asteroids]: Spawning asteroid rain with density {density}, altitude {altitude / 1000f}km and radius {radius / 1000f}km at coordinates ({geoCoords.x:F4}, {geoCoords.y:F4}).");

            var timeToFall = Mathf.Sqrt(altitude * 2f / (float)FlightGlobals.getGeeForceAtPosition(FlightGlobals.currentMainBody.GetWorldSurfacePosition(geoCoords.x, geoCoords.y, altitude)).magnitude) + 5f; // Plus a bit for clean-up.
            var spawnInterval = 3e5f / (radius * radius * density); // α / (area * density)
            SetupAsteroidPool(Mathf.RoundToInt(timeToFall / spawnInterval));

            if (rainCoroutine != null)
            { StopCoroutine(rainCoroutine); }
            rainCoroutine = StartCoroutine(Rain());
            if (cleanUpCoroutine != null)
            { StopCoroutine(cleanUpCoroutine); }
            cleanUpCoroutine = StartCoroutine(CleanUp(1f));
        }

        IEnumerator Rain()
        {
            raining = true;
            var upDirection = (FlightGlobals.currentMainBody.GetWorldSurfacePosition(geoCoords.x, geoCoords.y, altitude) - FlightGlobals.currentMainBody.transform.position).normalized;
            var refDirection = Math.Abs(Vector3.Dot(Vector3.up, upDirection)) < 0.71f ? Vector3.up : Vector3.forward; // Avoid that the reference direction is colinear with the local surface normal.
            var densityWaitTime = 3e5f / (radius * radius * density); // α / (area * density)
            var densityWait = new WaitForSeconds(densityWaitTime);
            var waitForFixedUpdate = new WaitForFixedUpdate();
            while (raining)
            {
                var asteroid = GetAsteroid();
                if (asteroid != null)
                {
                    var direction = Vector3.ProjectOnPlane(Quaternion.AngleAxis((float)RNG.NextDouble() * 360f, upDirection) * refDirection, upDirection).normalized;
                    var x = (float)RNG.NextDouble();
                    var distance = Mathf.Sqrt(1f - x) * radius;
                    var position = altitude * upDirection + direction * distance;
                    StartCoroutine(RepositionWhenReady(asteroid, position));
                }

                if (asteroid != null && densityWaitTime > TimeWarp.fixedDeltaTime)
                    yield return densityWait;
                else
                    yield return waitForFixedUpdate;
            }
        }

        IEnumerator RepositionWhenReady(Vessel asteroid, Vector3 position)
        {
            var wait = new WaitForFixedUpdate();
            asteroid.gameObject.SetActive(true);
            while (asteroid != null && (asteroid.packed || !asteroid.loaded)) yield return wait;
            if (asteroid != null)
            {
                asteroid.transform.position = position;
                asteroid.SetWorldVelocity(Vector3.zero);
            }
        }

        IEnumerator CleanUp(float delay)
        {
            var wait = new WaitForSeconds(1f); // Don't bother checking too often.
            while (raining)
            {
                foreach (var asteroid in managedAsteroids)
                {
                    if (asteroid == null) continue;
                    if (asteroid.gameObject.activeInHierarchy && asteroid.LandedOrSplashed && !beingRemoved.Contains(asteroid))
                    {
                        beingRemoved.Add(asteroid);
                        StartCoroutine(RemoveAfterDelay(asteroid, delay));
                    }
                }
                yield return wait;
            }
        }

        IEnumerator RemoveAfterDelay(Vessel asteroid, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (asteroid != null)
            {
                asteroid.transform.position = FlightGlobals.currentMainBody.GetWorldSurfacePosition(geoCoords.x, geoCoords.y, altitude); // Reset the position for next time.
                asteroid.Landed = false;
                asteroid.Splashed = false;
                asteroid.gameObject.SetActive(false);
                beingRemoved.Remove(asteroid);
            }
        }

        public void Kick()
        {
            Debug.Log("DEBUG Kicking asteroids");
            StartCoroutine(ApplyKick(1f));
        }
        IEnumerator ApplyKick(float period)
        {
            var startTime = Time.time;
            while (Time.time - startTime < period)
            {
                foreach (var asteroid in managedAsteroids)
                {
                    if (asteroid == null || asteroid.rootPart == null || asteroid.rootPart.Rigidbody == null) continue;
                    asteroid.rootPart.Rigidbody.AddForceAtPosition(-10f * FlightGlobals.getGeeForceAtPosition(asteroid.transform.position), asteroid.CoM, ForceMode.Acceleration);
                }
                yield return new WaitForFixedUpdate();
            }
        }

        public void Twitch()
        {
            Debug.Log("DEBUG Twitching asteroids");
            foreach (var asteroid in managedAsteroids)
            {
                var offset = new Vector3((float)RNG.NextDouble() * 2f - 1f, (float)RNG.NextDouble() * 2f - 1f, (float)RNG.NextDouble() * 2f - 1f);
                var upDirection = -FlightGlobals.getGeeForceAtPosition(asteroid.transform.position).normalized;
                asteroid.transform.position += offset * 10f + upDirection * 10f;
            }
        }

        void SetupAsteroidPool(int count)
        {
            if (asteroidPool == null)
            { asteroidPool = new List<Vessel>(); }
            if (count > asteroidPool.Count)
                AddAsteroidsToPool(count - asteroidPool.Count);
        }

        void ReplacePooledAsteroid(int i)
        {
            Debug.Log($"[BDArmory.Asteroids]: Replacing asteroid at position {i}.");
            var asteroid = SpawnAsteroid(FlightGlobals.currentMainBody.GetWorldSurfacePosition(geoCoords.x, geoCoords.y, altitude));
            if (asteroid != null)
            {
                asteroid.gameObject.SetActive(false);
                asteroidPool[i] = asteroid;
                managedAsteroids.Add(asteroid); // Common hashset of managed asteroids.
            }
        }

        void AddAsteroidsToPool(int count)
        {
            Debug.Log($"[BDArmory.Asteroids]: Increasing asteroid pool size to {asteroidPool.Count + count}.");
            for (int i = 0; i < count; ++i)
            {
                var asteroid = SpawnAsteroid(FlightGlobals.currentMainBody.GetWorldSurfacePosition(geoCoords.x, geoCoords.y, altitude + 1000));
                if (asteroid != null)
                {
                    asteroid.gameObject.SetActive(false);
                    asteroidPool.Add(asteroid);
                    managedAsteroids.Add(asteroid); // Common hashset of managed asteroids.
                }
            }
        }

        protected Vessel GetAsteroid()
        {
            // Start at the last index returned and cycle round for efficiency. This makes this a typically O(1) seek operation.
            for (int i = lastPoolIndex + 1; i < asteroidPool.Count; ++i)
            {
                if (asteroidPool[i] == null)
                {
                    ReplacePooledAsteroid(i);
                }
                if (!asteroidPool[i].gameObject.activeInHierarchy)
                {
                    lastPoolIndex = i;
                    return asteroidPool[i];
                }
            }
            for (int i = 0; i < lastPoolIndex + 1; ++i)
            {
                if (asteroidPool[i] == null)
                {
                    ReplacePooledAsteroid(i);
                }
                if (!asteroidPool[i].gameObject.activeInHierarchy)
                {
                    lastPoolIndex = i;
                    return asteroidPool[i];
                }
            }

            var size = (int)(asteroidPool.Count * 1.2) + 1; // Grow by 20% + 1
            AddAsteroidsToPool(size - asteroidPool.Count);

            return asteroidPool[asteroidPool.Count - 1]; // Return the last entry in the pool
        }

    }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class AsteroidField : Asteroids
    {
        public static AsteroidField Instance;
        Vessel[] asteroids;
        bool floating;
        Coroutine floatingCoroutine;

        protected override void Awake()
        {
            if (Instance)
                Destroy(Instance);
            Instance = this;
            base.Awake();
        }

        /// <summary>
        /// Reset the asteroid field. 
        /// </summary>
        public void Reset()
        {
            floating = false;
            if (floatingCoroutine != null)
            { StopCoroutine(floatingCoroutine); }
            managedAsteroids.Clear();
        }

        /// <summary>
        /// Spawn an asteroid field.
        /// </summary>
        /// <param name="_numberOfAsteroids">The number of asteroids in the field.</param>
        /// <param name="_altitude">The maximum altitude AGL of the field, minimum altitude AGL is 50m.</param>
        /// <param name="_radius">The radius of the field from the spawn point.</param>
        /// <param name="_geoCoords">The spawn point (centre) of the field.</param>
        public void SpawnField(int _numberOfAsteroids, float _altitude, float _radius, Vector2d _geoCoords)
        {
            var numberOfAsteroids = _numberOfAsteroids;
            SetupAsteroids(_altitude, _radius, _geoCoords);
            Debug.Log($"[BDArmory.Asteroids]: Spawning asteroid field with {numberOfAsteroids} asteroids with height {altitude}m and radius {radius / 1000f}km at coordinate ({geoCoords.x:F4}, {geoCoords.y:F4}).");

            var spawnPoint = FlightGlobals.currentMainBody.GetWorldSurfacePosition(geoCoords.x, geoCoords.y, altitude);
            var upDirection = (spawnPoint - FlightGlobals.currentMainBody.transform.position).normalized;
            var refDirection = Math.Abs(Vector3.Dot(Vector3.up, upDirection)) < 0.71f ? Vector3.up : Vector3.forward; // Avoid that the reference direction is colinear with the local surface normal.
            asteroids = new Vessel[numberOfAsteroids];
            for (int i = 0; i < asteroids.Length; ++i)
            {
                var direction = Vector3.ProjectOnPlane(Quaternion.AngleAxis((float)RNG.NextDouble() * 360f, upDirection) * refDirection, upDirection).normalized;
                var x = (float)RNG.NextDouble();
                var distance = Mathf.Sqrt(1f - x) * radius;
                var height = RNG.NextDouble() * (altitude - 50f) + 50f;
                var position = spawnPoint + direction * distance;
                position += (height - Misc.Misc.GetRadarAltitudeAtPos(position, false)) * upDirection;
                var asteroid = SpawnAsteroid(position);
                if (asteroid != null)
                {
                    asteroids[i] = asteroid;
                    managedAsteroids.Add(asteroid);
                }
            }

            floatingCoroutine = StartCoroutine(Float());
            StartCoroutine(CleanOutAsteroids());
        }

        /// <summary>
        /// Apply forces to counteract gravity, decay overall motion and add Brownian noise.
        /// </summary>
        IEnumerator Float()
        {
            var wait = new WaitForFixedUpdate();
            floating = true;
            while (floating)
            {
                for (int i = 0; i < asteroids.Length; ++i)
                {
                    if (asteroids[i] == null || asteroids[i].packed || !asteroids[i].loaded || asteroids[i].rootPart.Rigidbody == null) continue;
                    var nudge = new Vector3d(RNG.NextDouble() - 0.5d, RNG.NextDouble() - 0.5d, RNG.NextDouble() - 0.5d) * 240d;
                    asteroids[i].rootPart.Rigidbody.AddForce(-FlightGlobals.getGeeForceAtPosition(asteroids[i].transform.position) - asteroids[i].srf_velocity + nudge, ForceMode.Acceleration); // Float and reduce motion.
                }
                yield return wait;
            }
        }

        /// <summary>
        /// Apply random torques to the asteroids to spin them up a bit. 
        /// </summary>
        IEnumerator InitialRotation()
        {
            var wait = new WaitForFixedUpdate();
            var startTime = Time.time;
            while (Time.time - startTime < 10f) // Apply small random torques for 10s.
            {
                for (int i = 0; i < asteroids.Length; ++i)
                {
                    if (asteroids[i] == null || asteroids[i].packed || !asteroids[i].loaded || asteroids[i].rootPart.Rigidbody == null) continue;
                    asteroids[i].rootPart.Rigidbody.AddTorque(new Vector3d(RNG.NextDouble() - 0.5d, RNG.NextDouble() - 0.5d, RNG.NextDouble() - 0.5d) * 5f, ForceMode.Acceleration);
                }
                yield return wait;
            }
        }

        /// <summary>
        /// Strip out various modules from the asteroids as they make excessive amounts of GC allocations. 
        /// Then set up the initial asteroid rotations.
        /// </summary>
        IEnumerator CleanOutAsteroids()
        {
            var wait = new WaitForFixedUpdate();
            while (asteroids.Any(a => a != null && (a.packed || !a.loaded))) yield return wait;
            for (int i = 0; i < asteroids.Length; ++i)
            {
                if (asteroids[i] == null) continue;
                using (var info = asteroids[i].FindPartModulesImplementing<ModuleAsteroid>().GetEnumerator())
                    while (info.MoveNext())
                    {
                        if (info.Current == null) continue;
                        Destroy(info.Current);
                    }
                using (var info = asteroids[i].FindPartModulesImplementing<ModuleAsteroidInfo>().GetEnumerator())
                    while (info.MoveNext())
                    {
                        if (info.Current == null) continue;
                        Destroy(info.Current);
                    }
                using (var info = asteroids[i].FindPartModulesImplementing<ModuleAsteroidResource>().GetEnumerator())
                    while (info.MoveNext())
                    {
                        if (info.Current == null) continue;
                        Destroy(info.Current);
                    }
            }

            yield return InitialRotation();
        }
    }
}