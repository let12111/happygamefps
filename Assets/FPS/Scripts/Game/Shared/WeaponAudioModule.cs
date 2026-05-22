using UnityEngine;

namespace Unity.FPS.Game
{
    [RequireComponent(typeof(AudioSource))]
    public class WeaponAudioModule : MonoBehaviour
    {
        [Header("Audio")]
        [Tooltip("Sound played when shooting")]
        public AudioClip ShootSfx;

        [Tooltip("Sound played when changing to this weapon")]
        public AudioClip ChangeWeaponSfx;

        [Tooltip("Continuous Shooting Sound")]
        public bool UseContinuousShootSound = false;

        public AudioClip ContinuousShootStartSfx;
        public AudioClip ContinuousShootLoopSfx;
        public AudioClip ContinuousShootEndSfx;

        AudioSource m_ShootAudioSource;
        AudioSource m_ContinuousShootAudioSource;
        bool m_WantsToShoot;

        void Awake()
        {
            m_ShootAudioSource = GetComponent<AudioSource>();
            DebugUtility.HandleErrorIfNullGetComponent<AudioSource, WeaponAudioModule>(m_ShootAudioSource, this,
                gameObject);

            if (UseContinuousShootSound)
            {
                m_ContinuousShootAudioSource = gameObject.AddComponent<AudioSource>();
                m_ContinuousShootAudioSource.playOnAwake = false;
                m_ContinuousShootAudioSource.clip = ContinuousShootLoopSfx;
                m_ContinuousShootAudioSource.outputAudioMixerGroup =
                    AudioUtility.GetAudioGroup(AudioUtility.AudioGroups.WeaponShoot);
                m_ContinuousShootAudioSource.loop = true;
            }
        }

        public void SetWantsToShoot(bool wantsToShoot) => m_WantsToShoot = wantsToShoot;

        public void UpdateContinuousShootSound(float currentAmmo)
        {
            if (!UseContinuousShootSound)
                return;

            if (m_WantsToShoot && currentAmmo >= 1f)
            {
                if (!m_ContinuousShootAudioSource.isPlaying)
                {
                    m_ShootAudioSource.PlayOneShot(ShootSfx);
                    m_ShootAudioSource.PlayOneShot(ContinuousShootStartSfx);
                    m_ContinuousShootAudioSource.Play();
                }
            }
            else if (m_ContinuousShootAudioSource.isPlaying)
            {
                m_ShootAudioSource.PlayOneShot(ContinuousShootEndSfx);
                m_ContinuousShootAudioSource.Stop();
            }
        }

        public void PlayShootSfx()
        {
            if (ShootSfx && !UseContinuousShootSound)
                m_ShootAudioSource.PlayOneShot(ShootSfx);
        }

        public void PlayChangeSfx()
        {
            if (ChangeWeaponSfx)
                m_ShootAudioSource.PlayOneShot(ChangeWeaponSfx);
        }
    }
}
