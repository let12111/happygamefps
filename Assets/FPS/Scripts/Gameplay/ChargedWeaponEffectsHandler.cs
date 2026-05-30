using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.Gameplay
{
    // ============================================================================
    // ChargedWeaponEffectsHandler — визуально-звуковое сопровождение зарядки.
    //
    // Висит на charge-оружии и читает WeaponController.CurrentCharge (0..1).
    // По мере зарядки:
    //  - меняет размер «заряжающегося шара»;
    //  - крутит «обруч» вокруг быстрее;
    //  - дёргает орбитальные частицы;
    //  - проигрывает звук зарядки → переходит в loop когда заряд достиг конца.
    //
    // Использует MinMaxFloat/Vector3 для всех «от-до» параметров — удобно для художника
    // в Inspector.
    // ============================================================================
    [RequireComponent(typeof(AudioSource))]
    public class ChargedWeaponEffectsHandler : MonoBehaviour
    {
        [Header("Visual")] [Tooltip("Object that will be affected by charging scale & color changes")]
        public GameObject ChargingObject;

        [Tooltip("The spinning frame")] public GameObject SpinningFrame;

        [Tooltip("Scale of the charged object based on charge")]
        public MinMaxVector3 Scale;

        [Header("Particles")] [Tooltip("Particles to create when charging")]
        public GameObject DiskOrbitParticlePrefab;

        [Tooltip("Local position offset of the charge particles (relative to this transform)")]
        public Vector3 Offset;

        [Tooltip("Parent transform for the particles (Optional)")]
        public Transform ParentTransform;

        [Tooltip("Orbital velocity of the charge particles based on charge")]
        public MinMaxFloat OrbitY;

        [Tooltip("Radius of the charge particles based on charge")]
        public MinMaxVector3 Radius;

        [Tooltip("Idle spinning speed of the frame based on charge")]
        public MinMaxFloat SpinningSpeed;

        [Header("Sound")] [Tooltip("Audio clip for charge SFX")]
        public AudioClip ChargeSound;

        [Tooltip("Sound played in loop after the change is full for this weapon")]
        public AudioClip LoopChargeWeaponSfx;

        // Кросс-фейд: пока ChargeSound затухает, LoopSfx нарастает.
        [Tooltip("Duration of the cross fade between the charge and the loop sound")]
        public float FadeLoopDuration = 0.5f;

        // Альтернативный режим: вместо двух звуков — один лупящийся с меняющимся pitch'ем.
        [Tooltip(
            "If true, the ChargeSound will be ignored and the pitch on the LoopSound will be procedural, based on the charge amount")]
        public bool UseProceduralPitchOnLoopSfx;

        [Range(1.0f, 5.0f), Tooltip("Maximum procedural Pitch value")]
        public float MaxProceduralPitchValue = 2.0f;

        public GameObject ParticleInstance { get; set; }

        ParticleSystem m_DiskOrbitParticle;
        WeaponController m_WeaponController;
        // VelocityOverLifetime — вложенный module ParticleSystem. Менять надо через
        // локальную переменную (он struct), не через цепочку точек.
        ParticleSystem.VelocityOverLifetimeModule m_VelocityOverTimeModule;

        AudioSource m_AudioSource;
        AudioSource m_AudioSourceLoop;

        float m_LastChargeTriggerTimestamp;
        float m_ChargeRatio;
        float m_EndchargeTime;

        void Awake()
        {
            m_LastChargeTriggerTimestamp = 0.0f;

            // The charge effect needs it's own AudioSources, since it will play on top of the other gun sounds
            // Создаём ДВА AudioSource в рантайме (по одному на каждый звук),
            // чтобы они играли параллельно с обычным звуком оружия.
            m_AudioSource = gameObject.AddComponent<AudioSource>();
            m_AudioSource.clip = ChargeSound;
            m_AudioSource.playOnAwake = false;
            m_AudioSource.outputAudioMixerGroup =
                AudioUtility.GetAudioGroup(AudioUtility.AudioGroups.WeaponChargeBuildup);

            // create a second audio source, to play the sound with a delay
            m_AudioSourceLoop = gameObject.AddComponent<AudioSource>();
            m_AudioSourceLoop.clip = LoopChargeWeaponSfx;
            m_AudioSourceLoop.playOnAwake = false;
            m_AudioSourceLoop.loop = true;
            m_AudioSourceLoop.outputAudioMixerGroup =
                AudioUtility.GetAudioGroup(AudioUtility.AudioGroups.WeaponChargeLoop);
        }

        // Lazy-spawn системы частиц при первом Update. Это позволяет
        // переключаться между оружиями без лишних эффектов до активации.
        void SpawnParticleSystem()
        {
            ParticleInstance = Instantiate(DiskOrbitParticlePrefab,
                ParentTransform != null ? ParentTransform : transform);
            ParticleInstance.transform.localPosition += Offset;

            FindReferences();
        }

        public void FindReferences()
        {
            m_DiskOrbitParticle = ParticleInstance.GetComponent<ParticleSystem>();
            DebugUtility.HandleErrorIfNullGetComponent<ParticleSystem, ChargedWeaponEffectsHandler>(m_DiskOrbitParticle,
                this, ParticleInstance.gameObject);

            m_WeaponController = GetComponent<WeaponController>();
            DebugUtility.HandleErrorIfNullGetComponent<WeaponController, ChargedWeaponEffectsHandler>(
                m_WeaponController, this, gameObject);

            m_VelocityOverTimeModule = m_DiskOrbitParticle.velocityOverLifetime;
        }

        void Update()
        {
            if (ParticleInstance == null)
                SpawnParticleSystem();

            // Эффект виден только когда оружие активно.
            m_DiskOrbitParticle.gameObject.SetActive(m_WeaponController.IsWeaponActive);
            m_ChargeRatio = m_WeaponController.CurrentCharge;

            // Масштаб шара по заряду.
            ChargingObject.transform.localScale = Scale.GetValueFromRatio(m_ChargeRatio);
            if (SpinningFrame != null)
            {
                // Постоянно ДОКРУЧИВАЕМ обруч на (скорость * dt) — синтаксис
                // localRotation *= Quaternion.Euler — это аккумуляция.
                SpinningFrame.transform.localRotation *= Quaternion.Euler(0,
                    SpinningSpeed.GetValueFromRatio(m_ChargeRatio) * Time.deltaTime, 0);
            }

            m_VelocityOverTimeModule.orbitalY = OrbitY.GetValueFromRatio(m_ChargeRatio);
            // 1.1f — небольшое «overshoot», шар чуть «дёргается» сверх обычного.
            m_DiskOrbitParticle.transform.localScale = Radius.GetValueFromRatio(m_ChargeRatio * 1.1f);

            // update sound's volume and pitch
            if (m_ChargeRatio > 0)
            {
                // Новая зарядка: запускаем звук, если ещё не играет
                // и WeaponController зафиксировал свежий старт.
                if (!m_AudioSourceLoop.isPlaying &&
                    m_WeaponController.LastChargeTriggerTimestamp > m_LastChargeTriggerTimestamp)
                {
                    m_LastChargeTriggerTimestamp = m_WeaponController.LastChargeTriggerTimestamp;
                    if (!UseProceduralPitchOnLoopSfx)
                    {
                        // Запоминаем, когда ChargeSound закончит играть — после этого
                        // полностью переключимся на loop.
                        m_EndchargeTime = Time.time + ChargeSound.length;
                        m_AudioSource.Play();
                    }

                    m_AudioSourceLoop.Play();
                }

                if (!UseProceduralPitchOnLoopSfx)
                {
                    // Кросс-фейд: к концу длины ChargeSound его volume падает к 0, а loop — к 1.
                    float volumeRatio =
                        Mathf.Clamp01((m_EndchargeTime - Time.time - FadeLoopDuration) / FadeLoopDuration);
                    m_AudioSource.volume = volumeRatio;
                    m_AudioSourceLoop.volume = 1 - volumeRatio;
                }
                else
                {
                    // Процедурный pitch: чем больше заряд, тем выше тон.
                    m_AudioSourceLoop.pitch = Mathf.Lerp(1.0f, MaxProceduralPitchValue, m_ChargeRatio);
                }
            }
            else
            {
                // Заряд сброшен (выстрел или отмена) — глушим оба источника.
                m_AudioSource.Stop();
                m_AudioSourceLoop.Stop();
            }
        }
    }
}
