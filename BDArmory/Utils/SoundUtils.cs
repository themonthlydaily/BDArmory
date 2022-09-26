using System.Collections.Generic;
using UnityEngine;

using BDArmory.Settings;

namespace BDArmory.Utils
{
    public static class SoundUtils
    {
        static Dictionary<string, AudioClip> audioClips = new Dictionary<string, AudioClip>(); // Cache audio clips so that they're not fetched from the GameDatabase every time. Really, the GameDatabase should be doing this!

        public static AudioClip GetAudioClip(string soundPath)
        {
            if (!audioClips.TryGetValue(soundPath, out AudioClip audioClip) || audioClip is null)
            {
                audioClip = GameDatabase.Instance.GetAudioClip(soundPath);
                if (audioClip is null) Debug.LogError($"[BDArmory.SoundUtils]: {soundPath} did not give a valid audioclip.");
                else if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.SoundUtils]: Adding audioclip {soundPath} to the cache.");
                audioClips[soundPath] = audioClip;
            }
            return audioClip;
        }

        public static void ClearAudioCache() => audioClips.Clear(); // Maybe someone has a reason for doing this to reload sounds dynamically? They'd need a way to refresh the GameDatabase too though.
    }
}