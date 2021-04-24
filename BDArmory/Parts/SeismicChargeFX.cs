using System;
using BDArmory.Core.Extension;
using UnityEngine;

namespace BDArmory.Parts
{
    public class SeismicChargeFX : MonoBehaviour
    {
        //orphaned, incredibly limited in scope, functionality and requiring a specific FX model that isn't present, but,
        //if the proper collider setup can be recovered/reverse-engineered, this has potential...
        AudioSource audioSource;

        public static float originalShipVolume;
        public static float originalMusicVolume;
        public static float originalAmbienceVolume;

        [KSPField]
        public string FXName = "lightFlare";
        [KSPField]
        public string DetonationSound = "BDArmory/Sounds/seismicCharge";

        static string DefaultExplosionPath = "BDArmory/Models/seismicCharge/seismicExplosion";

        float startTime;

        Transform lightFlare;
        Rigidbody rb;

        Vector3 explosionCoords;
        Vector3 explosionUp;

        void Start()
        {
            transform.localScale = 2 * Vector3.one;
            lightFlare = gameObject.transform.Find("FXName");
            startTime = Time.time;

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.minDistance = 5000;
            audioSource.maxDistance = 5000;
            audioSource.dopplerLevel = 0f;
            audioSource.pitch = UnityEngine.Random.Range(0.93f, 1f);
            audioSource.volume = Mathf.Sqrt(originalShipVolume);

            audioSource.PlayOneShot(GameDatabase.Instance.GetAudioClip(DetonationSound));

            rb = gameObject.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.velocity = Vector3.zero;
            rb.isKinematic = true;

            explosionCoords = transform.position;
            explosionUp = transform.up;

        }

        void FixedUpdate()
        {
            //lightFlare.LookAt(transform.position, transform.up);
            lightFlare.LookAt(explosionCoords, explosionUp);
            if (Time.time - startTime < 1.25f)
            {
                //
                GameSettings.SHIP_VOLUME = Mathf.MoveTowards(GameSettings.SHIP_VOLUME, 0, originalShipVolume / 0.7f);
                GameSettings.MUSIC_VOLUME = Mathf.MoveTowards(GameSettings.MUSIC_VOLUME, 0, originalShipVolume / 0.7f);
                GameSettings.AMBIENCE_VOLUME = Mathf.MoveTowards(GameSettings.AMBIENCE_VOLUME, 0,
                    originalShipVolume / 0.7f);
            }
            else if (Time.time - startTime < 7.35f / audioSource.pitch)
            {
                //make it fade in more slowly
                GameSettings.SHIP_VOLUME = Mathf.MoveTowards(GameSettings.SHIP_VOLUME, originalShipVolume,
                    originalShipVolume / 3f * Time.fixedDeltaTime);
                GameSettings.MUSIC_VOLUME = Mathf.MoveTowards(GameSettings.MUSIC_VOLUME, originalMusicVolume,
                    originalMusicVolume / 3f * Time.fixedDeltaTime);
                GameSettings.AMBIENCE_VOLUME = Mathf.MoveTowards(GameSettings.AMBIENCE_VOLUME, originalAmbienceVolume,
                    originalAmbienceVolume / 3f * Time.fixedDeltaTime);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void OnTriggerEnter(Collider other)
        {
            //hitting parts
            Part explodePart = null;     
            try
            {
                explodePart = other.gameObject.GetComponentUpwards<Part>();
                //explodePart.Unpack();
            }
            catch (NullReferenceException e)
            {
                Debug.LogWarning("[BDArmory.SeismicChargeFX]: Exception thrown in OnTriggerEnter: Where part?" + e.Message + "\n" + e.StackTrace);
            }

            if (explodePart != null)
            {
                explodePart.Destroy();
                //make a variant that breaks all connedctions/attach nodes, make it a shatter weapon?
                Debug.Log("[SeismicCharge] Hit a part");
            }
            else
            {
                //hitting buildings
                DestructibleBuilding hitBuilding = null;
                try
                {
                    hitBuilding = other.gameObject.GetComponentUpwards<DestructibleBuilding>();
                }
                catch (NullReferenceException e)
                {
                    Debug.LogWarning("[BDArmory.SeismicChargeFX]: Exception thrown in OnTriggerEnter: Where building? " + e.Message + "\n" + e.StackTrace);
                }
                if (hitBuilding != null && hitBuilding.IsIntact)
                {
                    hitBuilding.Demolish();
                    Debug.Log("[SeismicCharge] Hit a building");
                }
            }
            ///Destroy(gameObject);
        }

        public static void CreateSeismicExplosion(Vector3 pos, Quaternion rot, string Modelpath = null)
        {
            var FXTemplate = GameDatabase.Instance.GetModel(Modelpath);
            if (FXTemplate == null)
            {
                Debug.LogError("[BDArmory.SeismicCharge]: " + Modelpath + " was not found, using the default explosion instead. Please fix your model.");
                FXTemplate = GameDatabase.Instance.GetModel(DefaultExplosionPath);
            }
            GameObject explosionModel = FXTemplate;
            GameObject explosionObject = (GameObject)Instantiate(explosionModel, pos, rot);
            explosionObject.SetActive(true);
            explosionObject.AddComponent<SeismicChargeFX>();
        }
    }
}
