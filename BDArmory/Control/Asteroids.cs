using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BDArmory.Core;
using BDArmory.Misc;
using BDArmory.UI;
using BDArmory.Core.Extension;

namespace BDArmory.Control
{
    public class AsteroidUtils
    {
        public static UntrackedObjectClass[] UntrackedObjectClasses = (UntrackedObjectClass[])Enum.GetValues(typeof(UntrackedObjectClass)); // Get the UntrackedObjectClasses as an array of enum values.
        static System.Random RNG = new System.Random();


        /// <summary>
        /// Spawn an asteroid of the given class at the given position.
        /// </summary>
        /// <param name="position">The position to spawn the asteroid.</param>
        /// <param name="untrackedObjectClassIndex">The class of the asteroid. -1 picks one at random.</param>
        /// <returns>The asteroid vessel.</returns>
        public static Vessel SpawnAsteroid(Vector3d position, int untrackedObjectClassIndex = -1)
        {
            if (untrackedObjectClassIndex < 0)
            {
                untrackedObjectClassIndex = RNG.Next(UntrackedObjectClasses.Length);
            }
            var asteroid = DiscoverableObjectsUtil.SpawnAsteroid(DiscoverableObjectsUtil.GenerateAsteroidName(), GetOrbitForApoapsis2(position), (uint)RNG.Next(), UntrackedObjectClasses[untrackedObjectClassIndex], double.MaxValue, double.MaxValue);
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
        public static Orbit GetOrbitForApoapsis(Vector3d position)
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
        public static Orbit GetOrbitForApoapsis2(Vector3d position)
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
        public static void CheckOrbit()
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

        /// <summary>
        /// Strip out various modules from the asteroid as they make excessive amounts of GC allocations. 
        /// This seems only to be possible once the asteroid is active, loaded and unpacked.
        /// </summary>
        public static void CleanOutAsteroid(Vessel asteroid)
        {
            var mod = asteroid.GetComponent<ModuleAsteroid>();
            if (mod != null)
            {
                UnityEngine.Object.Destroy(mod);
            }
            var modInfo = asteroid.GetComponent<ModuleAsteroidInfo>();
            if (modInfo != null)
            {
                UnityEngine.Object.Destroy(modInfo);
            }
            var modResource = asteroid.GetComponent<ModuleAsteroidResource>();
            if (modResource != null)
            {
                UnityEngine.Object.Destroy(modResource);
            }
        }
    }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class AsteroidRain : MonoBehaviour
    {
        #region Fields
        public static AsteroidRain Instance;

        bool raining = true;
        float density;
        float altitude;
        float radius;
        Vector2d geoCoords;
        Vector3d spawnPoint;
        Vector3d upDirection;
        System.Random RNG;
        static float alpha = 5e5f;

        Coroutine rainCoroutine;
        Coroutine cleanUpCoroutine;
        HashSet<Vessel> beingRemoved = new HashSet<Vessel>();

        // Pooling of asteroids
        List<Vessel> asteroidPool;
        int lastPoolIndex = 0;
        public HashSet<string> asteroidNames;
        #endregion

        void Awake()
        {
            if (Instance)
                Destroy(Instance);
            Instance = this;

            if (asteroidNames == null)
            {
                asteroidNames = new HashSet<string>();
            }
            if (RNG == null)
            {
                RNG = new System.Random();
            }
            GameEvents.onGameSceneSwitchRequested.Add(HandleSceneChange);
            GameEvents.onGameAboutToQuicksave.Add(Reset);
        }

        void OnDestroy()
        {
            Reset();
            if (asteroidPool != null)
            {
                foreach (var asteroid in asteroidPool)
                { if (asteroid != null) Destroy(asteroid); }
            }
            GameEvents.onGameSceneSwitchRequested.Remove(HandleSceneChange);
            GameEvents.onGameAboutToQuicksave.Remove(Reset);
        }

        public void Reset()
        {
            raining = false;
            if (rainCoroutine != null)
            { StopCoroutine(rainCoroutine); }
            if (cleanUpCoroutine != null)
            { StopCoroutine(cleanUpCoroutine); }
            beingRemoved.Clear();
            if (asteroidPool != null)
            {
                foreach (var asteroid in asteroidPool)
                {
                    if (asteroid != null && asteroid.gameObject.activeInHierarchy)
                    { asteroid.gameObject.SetActive(false); }
                }
            }
        }

        public void HandleSceneChange(GameEvents.FromToAction<GameScenes, GameScenes> fromTo)
        {
            if (fromTo.from == GameScenes.FLIGHT)
            {
                Reset();
                if (fromTo.to != GameScenes.FLIGHT)
                {
                    if (asteroidPool != null)
                    {
                        foreach (var asteroid in asteroidPool)
                        { if (asteroid != null) Destroy(asteroid); }
                        asteroidPool.Clear();
                    }
                }
            }
        }

        public static int approxNumberOfAsteroids(float density, float altitude, float radius, Vector2d geoCoords)
        {
            if (FlightGlobals.currentMainBody == null) return 0;
            var timeToFall = Mathf.Sqrt(altitude * 2f / (float)FlightGlobals.getGeeForceAtPosition(FlightGlobals.currentMainBody.GetWorldSurfacePosition(geoCoords.x, geoCoords.y, altitude)).magnitude);
            var spawnInterval = alpha / (radius * radius * density); // α / (area * density)
            return Mathf.RoundToInt(timeToFall / spawnInterval);
        }

        public void SpawnRain(float _density, float _altitude, float _radius, Vector2d _geoCoords)
        {
            altitude = _altitude < 10f ? _altitude * 100f : (_altitude - 9f) * 1000f; // Convert to m.
            radius = _radius * 1000f; // Convert to m.
            density = _density;
            geoCoords = _geoCoords;
            Debug.Log($"[BDArmory.Asteroids]: Spawning asteroid rain with density {density}, altitude {altitude / 1000f}km and radius {radius / 1000f}km at coordinates ({geoCoords.x:F4}, {geoCoords.y:F4}).");

            BDACompetitionMode.Instance.competitionStatus.Add("Setting up Asteroid Rain, please be patient.");
            Reset();
            SetupAsteroidPool(approxNumberOfAsteroids(density, altitude, radius, geoCoords));

            if (rainCoroutine != null)
            { StopCoroutine(rainCoroutine); }
            rainCoroutine = StartCoroutine(Rain());
            if (cleanUpCoroutine != null)
            { StopCoroutine(cleanUpCoroutine); }
            cleanUpCoroutine = StartCoroutine(CleanUp(0.1f));
        }

        IEnumerator Rain()
        {
            raining = true;
            spawnPoint = FlightGlobals.currentMainBody.GetWorldSurfacePosition(geoCoords.x, geoCoords.y, altitude);
            upDirection = (spawnPoint - FlightGlobals.currentMainBody.transform.position).normalized;
            spawnPoint += (altitude - Misc.Misc.GetRadarAltitudeAtPos(spawnPoint, false)) * upDirection; // Adjust for terrain height.
            var refDirection = Math.Abs(Vector3d.Dot(Vector3.up, upDirection)) < 0.71f ? Vector3d.up : Vector3d.forward; // Avoid that the reference direction is colinear with the local surface normal.
            yield return PreCleanAsteroids();
            var densityWaitTime = alpha / (radius * radius * density); // α / (area * density)
            var densityWait = new WaitForSeconds(densityWaitTime);
            var waitForFixedUpdate = new WaitForFixedUpdate();
            while (raining)
            {
                upDirection = (FlightGlobals.currentMainBody.GetWorldSurfacePosition(geoCoords.x, geoCoords.y, altitude) - FlightGlobals.currentMainBody.transform.position).normalized;
                var asteroid = GetAsteroid();
                if (asteroid != null)
                {
                    AsteroidUtils.CleanOutAsteroid(asteroid);
                    asteroid.Landed = false;
                    asteroid.Splashed = false;
                    var direction = Vector3.ProjectOnPlane(Quaternion.AngleAxis((float)RNG.NextDouble() * 360f, upDirection) * refDirection, upDirection).normalized;
                    var x = (float)RNG.NextDouble();
                    var distance = Mathf.Sqrt(1f - x) * radius;
                    StartCoroutine(RepositionWhenReady(asteroid, direction * distance));
                }

                if (asteroid != null && densityWaitTime > TimeWarp.fixedDeltaTime)
                    yield return densityWait;
                else
                    yield return waitForFixedUpdate;
            }
        }

        IEnumerator RepositionWhenReady(Vessel asteroid, Vector3 offset)
        {
            var wait = new WaitForFixedUpdate();
            asteroid.gameObject.SetActive(true);
            while (asteroid != null && (asteroid.packed || !asteroid.loaded || asteroid.rootPart.Rigidbody == null)) yield return wait;
            if (asteroid != null)
            {
                AsteroidUtils.CleanOutAsteroid(asteroid); // Make sure KSP hasn't added them back in!
                spawnPoint = FlightGlobals.currentMainBody.GetWorldSurfacePosition(geoCoords.x, geoCoords.y, altitude);
                var position = spawnPoint + offset;
                position += (altitude - Misc.Misc.GetRadarAltitudeAtPos(position, false)) * upDirection;
                asteroid.transform.position = position;
                asteroid.SetWorldVelocity(-upDirection * 100f);
                asteroid.rootPart.Rigidbody.angularVelocity = Vector3.zero;
                var torque = Vector3d.zero;
                for (int i = 0; i < 100; ++i)
                {
                    torque.x += RNG.NextDouble() - 0.5d;
                    torque.y += RNG.NextDouble() - 0.5d;
                    torque.z += RNG.NextDouble() - 0.5d;
                }
                asteroid.rootPart.Rigidbody.AddTorque(torque * 100f, ForceMode.Acceleration);
            }
        }

        IEnumerator CleanUp(float interval)
        {
            var wait = new WaitForSeconds(interval); // Don't bother checking too often.
            while (raining)
            {
                foreach (var asteroid in asteroidPool)
                {
                    if (asteroid == null) continue;
                    var timeToImpact = (float)((asteroid.radarAltitude - asteroid.GetRadius()) / asteroid.srfSpeed) - Time.fixedDeltaTime;
                    if (asteroid.gameObject.activeInHierarchy && (timeToImpact < interval || asteroid.LandedOrSplashed) && !beingRemoved.Contains(asteroid))
                    {
                        StartCoroutine(RemoveAfterDelay(asteroid, timeToImpact));
                    }
                }
                yield return wait;
            }
        }

        IEnumerator RemoveAfterDelay(Vessel asteroid, float delay)
        {
            beingRemoved.Add(asteroid);
            yield return new WaitForSeconds(delay);
            if (asteroid != null)
            {
                asteroid.transform.position += 10000f * upDirection; // Put the asteroid where it won't immediately die on re-activating, since we apparently can't reposition it immediately upon activation.
                asteroid.SetWorldVelocity(Vector3.zero); // Also, reset its velocity.
                asteroid.gameObject.SetActive(false);
                beingRemoved.Remove(asteroid);
            }
            else
            { if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log($"[BDArmory.Asteroids]: Asteroid {asteroid.vesselName} is null, unable to remove."); }
        }

        int cleaningInProgress;
        IEnumerator PreCleanAsteroids()
        {
            var wait = new WaitForFixedUpdate();
            cleaningInProgress = 0;
            foreach (var asteroid in asteroidPool)
            {
                if (asteroid == null) continue;
                ++cleaningInProgress;
                StartCoroutine(PreCleanAsteroid(asteroid));
            }
            while (cleaningInProgress > 0)
            {
                yield return wait;
            }
            foreach (var asteroid in asteroidPool)
            {
                if (asteroid == null) continue;
                asteroid.gameObject.SetActive(false);
            }
        }

        IEnumerator PreCleanAsteroid(Vessel asteroid)
        {
            var wait = new WaitForFixedUpdate();
            asteroid.gameObject.SetActive(true);
            while (asteroid != null && (asteroid.packed || !asteroid.loaded)) yield return wait;
            AsteroidUtils.CleanOutAsteroid(asteroid);
            --cleaningInProgress;
        }

        void SetupAsteroidPool(int count)
        {
            if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log($"[BDArmory.Asteroids]: Setting up asteroid pool with {count} asteroids.");
            if (asteroidPool == null)
            { asteroidPool = new List<Vessel>(); }
            if (count > asteroidPool.Count)
            { AddAsteroidsToPool(count - asteroidPool.Count); }
            else
            {
                for (int i = count; i < asteroidPool.Count; ++i)
                { Destroy(asteroidPool[i]); }
                asteroidPool.RemoveRange(count, asteroidPool.Count - count);
            }
        }

        void ReplacePooledAsteroid(int i)
        {
            Debug.Log($"[BDArmory.Asteroids]: Replacing asteroid at position {i}.");
            var asteroid = AsteroidUtils.SpawnAsteroid(FlightGlobals.currentMainBody.GetWorldSurfacePosition(geoCoords.x, geoCoords.y, altitude + 10000));
            if (asteroid != null)
            {
                asteroid.gameObject.SetActive(false);
                asteroidPool[i] = asteroid;
                asteroidNames.Add(asteroid.vesselName);
            }
        }

        void AddAsteroidsToPool(int count)
        {
            Debug.Log($"[BDArmory.Asteroids]: Increasing asteroid pool size to {asteroidPool.Count + count}.");
            spawnPoint = FlightGlobals.currentMainBody.GetWorldSurfacePosition(geoCoords.x, geoCoords.y, altitude);
            upDirection = (spawnPoint - FlightGlobals.currentMainBody.transform.position).normalized;
            var refDirection = Math.Abs(Vector3d.Dot(Vector3.up, upDirection)) < 0.71f ? Vector3d.up : Vector3d.forward; // Avoid that the reference direction is colinear with the local surface normal.
            for (int i = 0; i < count; ++i)
            {
                var direction = Vector3.ProjectOnPlane(Quaternion.AngleAxis(i * 10f / count * 360f, upDirection) * refDirection, upDirection).normalized;
                var position = spawnPoint + (1e3f + 1e2f * i / 10f) * upDirection + 1e3f * direction;
                var asteroid = AsteroidUtils.SpawnAsteroid(position);
                if (asteroid != null)
                {
                    AsteroidUtils.CleanOutAsteroid(asteroid);
                    asteroid.gameObject.SetActive(false);
                    asteroidPool.Add(asteroid);
                    asteroidNames.Add(asteroid.vesselName);
                }
            }
        }

        Vessel GetAsteroid()
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

        public void CheckPooledAsteroids()
        {
            if (asteroidPool == null) { Debug.Log("DEBUG Asteroid pool is not set up yet."); return; }
            for (int i = 0; i < asteroidPool.Count; ++i)
            {
                if (asteroidPool[i] == null) { Debug.Log($"DEBUG asteroid at position {i} is null"); continue; }
                Debug.Log($"DEBUG Asteroid[{i}] modules removed? {asteroidPool[i].GetComponent<ModuleAsteroid>() == null && asteroidPool[i].GetComponent<ModuleAsteroidInfo>() == null && asteroidPool[i].GetComponent<ModuleAsteroidResource>() == null}");
            }
        }
    }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class AsteroidField : MonoBehaviour
    {
        #region Fields
        public static AsteroidField Instance;
        public HashSet<string> asteroidNames;
        Vessel[] asteroids;
        bool floating;
        Coroutine floatingCoroutine;
        System.Random RNG;
        #endregion

        void Awake()
        {
            if (Instance)
                Destroy(Instance);
            Instance = this;

            if (asteroidNames == null)
            {
                asteroidNames = new HashSet<string>();
            }
            if (RNG == null)
            {
                RNG = new System.Random();
            }
        }

        void OnDestroy()
        {
            Reset();
        }

        /// <summary>
        /// Reset the asteroid field. 
        /// </summary>
        public void Reset()
        {
            floating = false;
            if (floatingCoroutine != null)
            { StopCoroutine(floatingCoroutine); }
            asteroidNames.Clear();
            if (asteroids != null)
            {
                foreach (var asteroid in asteroids)
                { if (asteroid != null) Destroy(asteroid); }
            }
        }

        /// <summary>
        /// Spawn an asteroid field.
        /// </summary>
        /// <param name="_numberOfAsteroids">The number of asteroids in the field.</param>
        /// <param name="_altitude">The maximum altitude AGL of the field, minimum altitude AGL is 50m.</param>
        /// <param name="_radius">The radius of the field from the spawn point.</param>
        /// <param name="_geoCoords">The spawn point (centre) of the field.</param>
        public void SpawnField(int numberOfAsteroids, float altitude, float radius, Vector2d geoCoords)
        {
            altitude = altitude < 10f ? altitude * 100f : (altitude - 9f) * 1000f; // Convert to m.
            radius *= 1000f; // Convert to m.
            Debug.Log($"[BDArmory.Asteroids]: Spawning asteroid field with {numberOfAsteroids} asteroids with height {altitude}m and radius {radius / 1000f}km at coordinate ({geoCoords.x:F4}, {geoCoords.y:F4}).");
            BDACompetitionMode.Instance.competitionStatus.Add("Spawning Asteroid Field, please be patient.");

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
                var asteroid = AsteroidUtils.SpawnAsteroid(position);
                if (asteroid != null)
                {
                    asteroids[i] = asteroid;
                    asteroidNames.Add(asteroid.vesselName);
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
            Vector3d torque;
            while (Time.time - startTime < 10f) // Apply small random torques for 10s.
            {
                for (int i = 0; i < asteroids.Length; ++i)
                {
                    if (asteroids[i] == null || asteroids[i].packed || !asteroids[i].loaded || asteroids[i].rootPart.Rigidbody == null) continue;
                    torque.x = RNG.NextDouble() - 0.5d;
                    torque.y = RNG.NextDouble() - 0.5d;
                    torque.z = RNG.NextDouble() - 0.5d;
                    asteroids[i].rootPart.Rigidbody.AddTorque(torque * 5f, ForceMode.Acceleration);
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
                AsteroidUtils.CleanOutAsteroid(asteroids[i]);
            }

            yield return InitialRotation();
        }
    }
}