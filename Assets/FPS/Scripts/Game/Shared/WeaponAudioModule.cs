using UnityEngine;

namespace Unity.FPS.Game
{
    // ============================================================================
    // WeaponAudioModule — звуки оружия. Разделено на два типа:
    //  - Дискретные выстрелы (Manual/Charge): обычный PlayOneShot на каждый шот.
    //  - Непрерывный «луп»: миниган, лазер. Звук состоит из start → loop → end,
    //    и его сложнее логику включения/выключения — отсюда отдельный AudioSource.
    //
    // RequireComponent гарантирует наличие основного AudioSource. Второй
    // AudioSource создаётся динамически только если нужен луп.
    // ============================================================================
    [RequireComponent(typeof(AudioSource))]
    public class WeaponAudioModule : MonoBehaviour
    {
        [Header("Audio")]
        [Tooltip("Sound played when shooting")]
        public AudioClip ShootSfx;

        [Tooltip("Sound played when changing to this weapon")]
        public AudioClip ChangeWeaponSfx;

        // Включает режим непрерывного звука (с тремя клипами).
        [Tooltip("Continuous Shooting Sound")]
        public bool UseContinuousShootSound = false;

        // Три части луп-звука: запуск, циклический середняк, отключение.
        public AudioClip ContinuousShootStartSfx;
        public AudioClip ContinuousShootLoopSfx;
        public AudioClip ContinuousShootEndSfx;

        // Источник для всех дискретных звуков. Уже есть благодаря RequireComponent.
        AudioSource m_ShootAudioSource;
        // Источник специально для луп-звука. Отдельный, потому что у него loop=true
        // и он должен играть параллельно с PlayOneShot обычного источника.
        AudioSource m_ContinuousShootAudioSource;
        // Хочет ли игрок стрелять прямо сейчас (зажал клавишу/триггер).
        bool m_WantsToShoot;

        void Awake()
        {
            m_ShootAudioSource = GetComponent<AudioSource>();
            DebugUtility.HandleErrorIfNullGetComponent<AudioSource, WeaponAudioModule>(m_ShootAudioSource, this,
                gameObject);

            // Если оружие с лупом — создаём ВТОРОЙ AudioSource в рантайме.
            // Через AddComponent — Unity сделает это в Inspector невидимым,
            // но он будет работать как обычный компонент.
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

        // WeaponController передаёт сюда состояние ввода — мы решаем
        // запускать ли цикл или остановить.
        public void SetWantsToShoot(bool wantsToShoot) => m_WantsToShoot = wantsToShoot;

        // Логика «включить луп / выключить луп» — вызывается каждый кадр.
        public void UpdateContinuousShootSound(float currentAmmo)
        {
            if (!UseContinuousShootSound)
                return;

            // Хочет стрелять и есть патроны — запускаем (если ещё не).
            if (m_WantsToShoot && currentAmmo >= 1f)
            {
                if (!m_ContinuousShootAudioSource.isPlaying)
                {
                    // start-клип через основной источник (PlayOneShot не мешает основному).
                    m_ShootAudioSource.PlayOneShot(ShootSfx);
                    m_ShootAudioSource.PlayOneShot(ContinuousShootStartSfx);
                    // А циклический луп — через специальный источник с loop=true.
                    m_ContinuousShootAudioSource.Play();
                }
            }
            // Перестал хотеть/кончились патроны, но цикл всё ещё играет — глушим.
            else if (m_ContinuousShootAudioSource.isPlaying)
            {
                m_ShootAudioSource.PlayOneShot(ContinuousShootEndSfx);
                m_ContinuousShootAudioSource.Stop();
            }
        }

        // Обычный выстрел — играем разовый клип. Только если НЕ continuous-режим
        // (в нём звуком управляет UpdateContinuousShootSound).
        public void PlayShootSfx()
        {
            if (ShootSfx && !UseContinuousShootSound)
                m_ShootAudioSource.PlayOneShot(ShootSfx);
        }

        // Звук смены оружия (щелчок затвора).
        public void PlayChangeSfx()
        {
            if (ChangeWeaponSfx)
                m_ShootAudioSource.PlayOneShot(ChangeWeaponSfx);
        }
    }
}
