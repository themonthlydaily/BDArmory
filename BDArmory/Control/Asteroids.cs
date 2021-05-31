using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
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

        protected float altitude;
        protected float radius;
        protected Vector2d geoCoords;
        protected System.Random RNG;

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
            foreach (var asteroid in managedAsteroids)
            {
                Destroy(asteroid);
            }
            managedAsteroids.Clear();
        }

        protected void SetupAsteroids(float _altitude, float _radius, Vector2d _geoCoords)
        {
            altitude = _altitude < 10f ? _altitude * 100f : (_altitude - 9f) * 1000f;
            radius = _radius * 1000f;
            geoCoords = _geoCoords;
            //  = new FloatCurve(new Keyframe[] { new Keyframe(0f, 0f), new Keyframe(1f, 0f) }); // Set the asteroid spawn chance to 0.
        }

        protected Vessel SpawnAsteroid(Vector3d position, int untrackedObjectClassIndex = -1)
        {
            if (untrackedObjectClassIndex < 0)
            {
                untrackedObjectClassIndex = RNG.Next(UntrackedObjectClasses.Length);
            }
            var asteroid = DiscoverableObjectsUtil.SpawnAsteroid(DiscoverableObjectsUtil.GenerateAsteroidName(), GetOrbitForApoapsis2(position), (uint)RNG.Next(), UntrackedObjectClasses[untrackedObjectClassIndex], 0, BDArmorySettings.COMPETITION_DURATION);
            if (asteroid != null && asteroid.vesselRef != null)
                return asteroid.vesselRef;
            else
                return null;
        }

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

        Orbit GetOrbitForApoapsis2(Vector3d position)
        {
            double latitude, longitude, altitude;
            FlightGlobals.currentMainBody.GetLatLonAlt(position, out latitude, out longitude, out altitude);
            longitude = (longitude + FlightGlobals.currentMainBody.rotationAngle + 180d) % 360d; // Compensate coordinates for planet rotation then normalise to 0°—360°.
            var orbitVelocity = FlightGlobals.currentMainBody.getRFrmVel(position);
            var orbitPosition = position - FlightGlobals.currentMainBody.transform.position;
            // Debug.Log($"DEBUG lat: {latitude}, lon: {longitude}, alt: {altitude}, pos: {orbitPosition}, vel: {orbitVelocity}");
            var orbit = new Orbit();
            orbit.UpdateFromStateVectors(orbitPosition.xzy, orbitVelocity.xzy, FlightGlobals.currentMainBody, Planetarium.GetUniversalTime());
            return orbit;
        }

        public void CheckOrbit()
        {
            if (FlightGlobals.ActiveVessel == null) { return; }
            var orbit = FlightGlobals.ActiveVessel.orbit;
            Debug.Log($"DEBUG Current vessel's orbit: inc: {orbit.inclination}, e: {orbit.eccentricity}, sma: {orbit.semiMajorAxis}, lan: {orbit.LAN}, argPe: {orbit.argumentOfPeriapsis}, mEp: {orbit.meanAnomalyAtEpoch}");
            orbit = GetOrbitForApoapsis(FlightGlobals.ActiveVessel.transform.position);
            Debug.Log($"DEBUG Predicted orbit:        inc: {orbit.inclination}, e: {orbit.eccentricity}, sma: {orbit.semiMajorAxis}, lan: {orbit.LAN}, argPe: {orbit.argumentOfPeriapsis}, mEp: {orbit.meanAnomalyAtEpoch}");
            orbit = GetOrbitForApoapsis2(FlightGlobals.ActiveVessel.transform.position);
            Debug.Log($"DEBUG Predicted orbit 3:      inc: {orbit.inclination}, e: {orbit.eccentricity}, sma: {orbit.semiMajorAxis}, lan: {orbit.LAN}, argPe: {orbit.argumentOfPeriapsis}, mEp: {orbit.meanAnomalyAtEpoch}");
            orbit.UpdateFromStateVectors(FlightGlobals.ActiveVessel.orbit.pos, FlightGlobals.ActiveVessel.orbit.vel, FlightGlobals.currentMainBody, Planetarium.GetUniversalTime());
            Debug.Log($"DEBUG Reconstructed 4:        inc: {orbit.inclination}, e: {orbit.eccentricity}, sma: {orbit.semiMajorAxis}, lan: {orbit.LAN}, argPe: {orbit.argumentOfPeriapsis}, mEp: {orbit.meanAnomalyAtEpoch}, pos: {FlightGlobals.ActiveVessel.orbit.pos}, vel: {FlightGlobals.ActiveVessel.orbit.vel}");
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

        public void Reset()
        {
            floating = false;
            if (floatingCoroutine != null)
            { StopCoroutine(floatingCoroutine); }
            managedAsteroids.Clear();
        }

        public void SpawnField(int _numberOfAsteroids, float _altitude, float _radius, Vector2d _geoCoords)
        {
            var numberOfAsteroids = _numberOfAsteroids;
            SetupAsteroids(_altitude, _radius, _geoCoords);
            Debug.Log($"[BDArmory.Asteroids]: Spawning asteroid field with {numberOfAsteroids} asteroids with height {altitude}km and radius {radius}km at coordinate ({geoCoords.x:F4}, {geoCoords.y:F4}).");

            var upDirection = (FlightGlobals.currentMainBody.GetWorldSurfacePosition(geoCoords.x, geoCoords.y, altitude) - FlightGlobals.currentMainBody.transform.position).normalized;
            var refDirection = Math.Abs(Vector3.Dot(Vector3.up, upDirection)) < 0.71f ? Vector3.up : Vector3.forward; // Avoid that the reference direction is colinear with the local surface normal.
            Debug.Log($"[BDArmory.Asteroids]: Number of asteroids: {numberOfAsteroids}");  // FIXME Add in-game warning about the number of asteroids being spawned
            asteroids = new Vessel[numberOfAsteroids];
            for (int i = 0; i < asteroids.Length; ++i)
            {
                var direction = Vector3.ProjectOnPlane(Quaternion.AngleAxis((float)RNG.NextDouble() * 360f, upDirection) * refDirection, upDirection).normalized;
                var x = (float)RNG.NextDouble();
                var distance = Mathf.Sqrt(1f - x) * radius;
                var height = RNG.NextDouble() * (altitude - 50f) + 50f;
                var position = height * upDirection + direction * distance;
                var asteroid = SpawnAsteroid(position);
                if (asteroid != null)
                {
                    asteroids[i] = asteroid;
                    managedAsteroids.Add(asteroid);
                }
            }

            floatingCoroutine = StartCoroutine(Float());
        }

        IEnumerator Float()
        {
            var wait = new WaitForFixedUpdate();
            floating = true;
            while (floating)
            {
                var gee = FlightGlobals.getGeeForceAtPosition(FlightGlobals.currentMainBody.GetWorldSurfacePosition(geoCoords.x, geoCoords.y, altitude));
                for (int i = 0; i < asteroids.Length; ++i)
                {
                    if (asteroids[i] == null || asteroids[i].packed || !asteroids[i].loaded || asteroids[i].rootPart.Rigidbody == null) continue;
                    asteroids[i].rootPart.Rigidbody.AddForce(-gee - asteroids[i].srf_velocity, ForceMode.Acceleration);
                }
                yield return wait;
            }
        }
    }
}