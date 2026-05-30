using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.Events;

namespace Unity.FPS.Gameplay
{
    // ============================================================================
    // Jetpack — спецспособность игрока. Двойное нажатие прыжка в воздухе
    // включает реактивный ранец, который толкает игрока вверх, расходуя топливо.
    //
    // Логика:
    //  1) Чтобы джетпак включился, игрок должен быть В ВОЗДУХЕ и снова нажать прыжок.
    //  2) Топливо тратится при удержании; восстанавливается с задержкой после отпускания.
    //  3) Восстановление быстрее на земле, чем в воздухе.
    //  4) Кроме базового ускорения вверх, джетпак КОМПЕНСИРУЕТ гравитацию и
    //     даже отрицательную Y-скорость — иначе при падении подъём «застывал бы».
    // ============================================================================
    [RequireComponent(typeof(AudioSource))]
    public class Jetpack : MonoBehaviour
    {
        [Header("References")] [Tooltip("Audio source for jetpack sfx")]
        public AudioSource AudioSource;

        [Tooltip("Particles for jetpack vfx")] public ParticleSystem[] JetpackVfx;

        [Header("Parameters")] [Tooltip("Whether the jetpack is unlocked at the begining or not")]
        public bool IsJetpackUnlockedAtStart = true;

        [Tooltip("The strength with which the jetpack pushes the player up")]
        public float JetpackAcceleration = 7f;

        // 0 — гравитация не компенсируется (игрок будет падать одновременно с подъёмом).
        // 1 — мгновенная компенсация (отрицательная Y-скорость сразу занулится).
        [Range(0f, 1f)]
        [Tooltip(
            "This will affect how much using the jetpack will cancel the gravity value, to start going up faster. 0 is not at all, 1 is instant")]
        public float JetpackDownwardVelocityCancelingFactor = 1f;

        [Header("Durations")] [Tooltip("Time it takes to consume all the jetpack fuel")]
        public float ConsumeDuration = 1.5f;

        [Tooltip("Time it takes to completely refill the jetpack while on the ground")]
        public float RefillDurationGrounded = 2f;

        [Tooltip("Time it takes to completely refill the jetpack while in the air")]
        public float RefillDurationInTheAir = 5f;

        [Tooltip("Delay after last use before starting to refill")]
        public float RefillDelay = 1f;

        [Header("Audio")] [Tooltip("Sound played when using the jetpack")]
        public AudioClip JetpackSfx;

        bool m_CanUseJetpack;
        bool m_VfxEnabled;
        PlayerCharacterController m_PlayerCharacterController;
        PlayerInputHandler m_InputHandler;
        float m_LastTimeOfUse;

        // stored ratio for jetpack resource (1 is full, 0 is empty)
        public float CurrentFillRatio { get; private set; }
        public bool IsJetpackUnlocked { get; private set; }

        public bool IsPlayergrounded() => m_PlayerCharacterController.IsGrounded;

        // Событие «джетпак разблокирован». На него слушает UI (JetpackCounter).
        public UnityAction<bool> OnUnlockJetpack;

        void Start()
        {
            IsJetpackUnlocked = IsJetpackUnlockedAtStart;

            m_PlayerCharacterController = GetComponent<PlayerCharacterController>();
            DebugUtility.HandleErrorIfNullGetComponent<PlayerCharacterController, Jetpack>(m_PlayerCharacterController,
                this, gameObject);

            m_InputHandler = GetComponent<PlayerInputHandler>();
            DebugUtility.HandleErrorIfNullGetComponent<PlayerInputHandler, Jetpack>(m_InputHandler, this, gameObject);

            CurrentFillRatio = 1f;

            AudioSource.clip = JetpackSfx;
            AudioSource.loop = true;

            // Выключаем эмиссию VFX на старте — включим только при использовании.
            // emission — это вложенный module, его нужно «достать-поменять-присвоить».
            for (int i = 0; i < JetpackVfx.Length; i++)
            {
                var emission = JetpackVfx[i].emission;
                emission.enabled = false;
            }
        }

        void Update()
        {
            // jetpack can only be used if not grounded and jump has been pressed again once in-air
            // Логика «двойного прыжка»:
            //  - на земле джетпак выключен;
            //  - в воздухе НА ВТОРОЕ нажатие прыжка (не на то, что подняло нас в воздух) — включается.
            if (IsPlayergrounded())
            {
                m_CanUseJetpack = false;
            }
            else if (!m_PlayerCharacterController.HasJumpedThisFrame && m_InputHandler.GetJumpInputDown())
            {
                m_CanUseJetpack = true;
            }

            // jetpack usage
            // Условия включения: разрешён + разблокирован + есть топливо + кнопка зажата.
            bool jetpackIsInUse = m_CanUseJetpack && IsJetpackUnlocked && CurrentFillRatio > 0f &&
                                  m_InputHandler.GetJumpInputHeld();
            if (jetpackIsInUse)
            {
                // store the last time of use for refill delay
                m_LastTimeOfUse = Time.time;

                float totalAcceleration = JetpackAcceleration;

                // cancel out gravity
                // Без этого джетпак боролся бы с гравитацией и подъём был бы слабым.
                totalAcceleration += m_PlayerCharacterController.GravityDownForce;

                // Если игрок ещё падает — компенсируем эту скорость с множителем.
                // Деление на dt превращает «скорость» в «нужное ускорение, чтобы за этот кадр занулить».
                if (m_PlayerCharacterController.CharacterVelocity.y < 0f)
                {
                    // handle making the jetpack compensate for character's downward velocity with bonus acceleration
                    totalAcceleration += ((-m_PlayerCharacterController.CharacterVelocity.y / Time.deltaTime) *
                                          JetpackDownwardVelocityCancelingFactor);
                }

                // apply the acceleration to character's velocity
                m_PlayerCharacterController.CharacterVelocity += Vector3.up * totalAcceleration * Time.deltaTime;

                // consume fuel
                // Простое: за ConsumeDuration секунд потратим 1.0 ratio → 0.
                CurrentFillRatio = CurrentFillRatio - (Time.deltaTime / ConsumeDuration);

                // Включаем VFX (один раз, не каждый кадр).
                if (!m_VfxEnabled)
                {
                    m_VfxEnabled = true;
                    for (int i = 0; i < JetpackVfx.Length; i++)
                    {
                        var emission = JetpackVfx[i].emission;
                        emission.enabled = true;
                    }
                }

                if (!AudioSource.isPlaying)
                    AudioSource.Play();
            }
            else
            {
                // refill the meter over time
                // Восстановление после задержки RefillDelay. На земле быстрее.
                if (IsJetpackUnlocked && Time.time - m_LastTimeOfUse >= RefillDelay)
                {
                    float refillRate = 1 / (m_PlayerCharacterController.IsGrounded
                        ? RefillDurationGrounded
                        : RefillDurationInTheAir);
                    CurrentFillRatio = CurrentFillRatio + Time.deltaTime * refillRate;
                }

                // Выключаем VFX, симметрично включению.
                if (m_VfxEnabled)
                {
                    m_VfxEnabled = false;
                    for (int i = 0; i < JetpackVfx.Length; i++)
                    {
                        var emission = JetpackVfx[i].emission;
                        emission.enabled = false;
                    }
                }

                // keeps the ratio between 0 and 1
                CurrentFillRatio = Mathf.Clamp01(CurrentFillRatio);

                if (AudioSource.isPlaying)
                    AudioSource.Stop();
            }
        }

        // Подбор JetpackPickup'а. Возвращает false если уже разблокирован.
        public bool TryUnlock()
        {
            if (IsJetpackUnlocked)
                return false;

            OnUnlockJetpack?.Invoke(true);
            IsJetpackUnlocked = true;
            m_LastTimeOfUse = Time.time;
            return true;
        }
    }
}
