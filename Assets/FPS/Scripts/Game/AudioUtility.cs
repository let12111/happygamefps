using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Unity.FPS.Game
{
    public class AudioUtility
    {
        static AudioManager s_AudioManager;
        static readonly Dictionary<AudioGroups, AudioMixerGroup> s_AudioGroupCache =
            new Dictionary<AudioGroups, AudioMixerGroup>();

        public enum AudioGroups
        {
            DamageTick,
            Impact,
            EnemyDetection,
            Pickup,
            WeaponShoot,
            WeaponOverheat,
            WeaponChargeBuildup,
            WeaponChargeLoop,
            HUDVictory,
            HUDObjective,
            EnemyAttack
        }

        public static void CreateSFX(AudioClip clip, Vector3 position, AudioGroups audioGroup, float spatialBlend,
            float rolloffDistanceMin = 1f)
        {
            GameObject impactSfxInstance = new GameObject();
            impactSfxInstance.transform.position = position;
            AudioSource source = impactSfxInstance.AddComponent<AudioSource>();
            source.clip = clip;
            source.spatialBlend = spatialBlend;
            source.minDistance = rolloffDistanceMin;
            source.Play();

            source.outputAudioMixerGroup = GetAudioGroup(audioGroup);

            TimedSelfDestruct timedSelfDestruct = impactSfxInstance.AddComponent<TimedSelfDestruct>();
            timedSelfDestruct.LifeTime = clip.length;
        }

        public static AudioMixerGroup GetAudioGroup(AudioGroups group)
        {
            if (s_AudioGroupCache.TryGetValue(group, out AudioMixerGroup cached))
                return cached;

            if (s_AudioManager == null)
                s_AudioManager = Object.FindAnyObjectByType<AudioManager>();

            var groups = s_AudioManager.FindMatchingGroups(group.ToString());
            AudioMixerGroup result = groups.Length > 0 ? groups[0] : null;

            if (result == null)
                Debug.LogWarning("Didn't find audio group for " + group.ToString());

            s_AudioGroupCache[group] = result;
            return result;
        }

        public static void SetMasterVolume(float value)
        {
            if (s_AudioManager == null)
                s_AudioManager = Object.FindAnyObjectByType<AudioManager>();

            if (value <= 0)
                value = 0.001f;
            float valueInDb = Mathf.Log10(value) * 20;

            s_AudioManager.SetFloat("MasterVolume", valueInDb);
        }

        public static float GetMasterVolume()
        {
            if (s_AudioManager == null)
                s_AudioManager = Object.FindAnyObjectByType<AudioManager>();

            s_AudioManager.GetFloat("MasterVolume", out var valueInDb);
            return Mathf.Pow(10f, valueInDb / 20.0f);
        }
    }
}
