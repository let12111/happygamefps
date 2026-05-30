using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.Events;

namespace Unity.FPS.Gameplay
{
    // ============================================================================
    // PlayerCharacterController — главный класс движения игрока.
    //
    // Что делает:
    //  - читает ввод (через PlayerInputHandler);
    //  - двигает капсулу CharacterController'а;
    //  - крутит камеру (горизонталь = поворот тела, вертикаль = только камеры);
    //  - обрабатывает прыжок, присед, спринт, гравитацию;
    //  - проверяет «стоим на земле» через CapsuleCast;
    //  - воспроизводит шаги, прыжок, приземление, урон от падения.
    //
    // ВАЖНО: для движения используется не Rigidbody, а Unity CharacterController.
    // У него собственный коллайдер-капсула и Move()-логика, которая работает
    // с условиями земли и склонов лучше, чем сырая физика.
    //
    // [RequireComponent] — Unity сам добавит и не даст убрать CharacterController,
    // PlayerInputHandler и AudioSource.
    // ============================================================================
    [RequireComponent(typeof(CharacterController), typeof(PlayerInputHandler), typeof(AudioSource))]
    public class PlayerCharacterController : MonoBehaviour
    {
        [Header("References")] [Tooltip("Reference to the main camera used for the player")]
        public Camera PlayerCamera;

        [Tooltip("Audio source for footsteps, jump, etc...")]
        public AudioSource AudioSource;

        // ----- Общие параметры физики -----
        [Header("General")] [Tooltip("Force applied downward when in the air")]
        public float GravityDownForce = 20f;

        // -1 = все слои. Можно указать конкретный «GroundCheck» слой для точности.
        [Tooltip("Physic layers checked to consider the player grounded")]
        public LayerMask GroundCheckLayers = -1;

        [Tooltip("distance from the bottom of the character controller capsule to test for grounded")]
        public float GroundCheckDistance = 0.05f;

        // ----- Параметры движения -----
        [Header("Movement")] [Tooltip("Max movement speed when grounded (when not sprinting)")]
        public float MaxSpeedOnGround = 10f;

        // «Sharpness» = скорость интерполяции к целевой скорости. Высокое значение
        // = моментальный отклик; низкое = инерция «как танк».
        [Tooltip(
            "Sharpness for the movement when grounded, a low value will make the player accelerate and decelerate slowly, a high value will do the opposite")]
        public float MovementSharpnessOnGround = 15;

        [Tooltip("Max movement speed when crouching")] [Range(0, 1)]
        public float MaxSpeedCrouchedRatio = 0.5f;

        [Tooltip("Max movement speed when not grounded")]
        public float MaxSpeedInAir = 10f;

        [Tooltip("Acceleration speed when in the air")]
        public float AccelerationSpeedInAir = 25f;

        [Tooltip("Multiplicator for the sprint speed (based on grounded speed)")]
        public float SprintSpeedModifier = 2f;

        // Если игрок упал слишком низко (за карту) — умирает мгновенно.
        [Tooltip("Height at which the player dies instantly when falling off the map")]
        public float KillHeight = -50f;

        // ----- Параметры поворота -----
        [Header("Rotation")] [Tooltip("Rotation speed for moving the camera")]
        public float RotationSpeed = 200f;

        // При прицеливании скорость поворота снижается — точнее целиться.
        [Range(0.1f, 1f)] [Tooltip("Rotation speed multiplier when aiming")]
        public float AimingRotationMultiplier = 0.4f;

        [Header("Jump")] [Tooltip("Force applied upward when jumping")]
        public float JumpForce = 9f;

        // ----- Стойка -----
        [Header("Stance")] [Tooltip("Ratio (0-1) of the character height where the camera will be at")]
        public float CameraHeightRatio = 0.9f;

        [Tooltip("Height of character when standing")]
        public float CapsuleHeightStanding = 1.8f;

        [Tooltip("Height of character when crouching")]
        public float CapsuleHeightCrouching = 0.9f;

        [Tooltip("Speed of crouching transitions")]
        public float CrouchingSharpness = 10f;

        // ----- Аудио -----
        [Header("Audio")] [Tooltip("Amount of footstep sounds played when moving one meter")]
        public float FootstepSfxFrequency = 1f;

        [Tooltip("Amount of footstep sounds played when moving one meter while sprinting")]
        public float FootstepSfxFrequencyWhileSprinting = 1f;

        [Tooltip("Sound played for footsteps")]
        public AudioClip FootstepSfx;

        [Tooltip("Sound played when jumping")] public AudioClip JumpSfx;
        [Tooltip("Sound played when landing")] public AudioClip LandSfx;

        [Tooltip("Sound played when taking damage froma fall")]
        public AudioClip FallDamageSfx;

        // ----- Урон от падения -----
        [Header("Fall Damage")]
        [Tooltip("Whether the player will recieve damage when hitting the ground at high speed")]
        public bool ReceivesFallDamage;

        [Tooltip("Minimun fall speed for recieving fall damage")]
        public float MinSpeedForFallDamage = 10f;

        [Tooltip("Fall speed for recieving th emaximum amount of fall damage")]
        public float MaxSpeedForFallDamage = 30f;

        [Tooltip("Damage recieved when falling at the mimimum speed")]
        public float FallDamageAtMinSpeed = 10f;

        [Tooltip("Damage recieved when falling at the maximum speed")]
        public float FallDamageAtMaxSpeed = 50f;

        // Событие «изменилась стойка» — на это слушает UI (StanceHUD).
        public UnityAction<bool> OnStanceChanged;

        // ----- Открытые свойства состояния -----
        public Vector3 CharacterVelocity { get; set; }
        public bool IsGrounded { get; private set; }
        // Прыгнул ли В ЭТОМ кадре — нужно для предотвращения мгновенного «snap to ground».
        public bool HasJumpedThisFrame { get; private set; }
        public bool IsDead { get; private set; }
        public bool IsCrouching { get; private set; }

        // Множитель чувствительности поворота. Уменьшается при прицеливании.
        public float RotationMultiplier
        {
            get
            {
                if (m_WeaponsManager.IsAiming)
                {
                    return AimingRotationMultiplier;
                }

                return 1f;
            }
        }

        // ----- Внутренние ссылки -----
        Health m_Health;
        PlayerInputHandler m_InputHandler;
        CharacterController m_Controller;
        PlayerWeaponsManager m_WeaponsManager;
        Actor m_Actor;
        // Нормаль поверхности, на которой стоим. Используем для «движения вдоль склона».
        Vector3 m_GroundNormal;
        Vector3 m_CharacterVelocity;
        // Скорость в момент последнего столкновения — нужно для подсчёта урона от падения.
        Vector3 m_LatestImpactSpeed;
        float m_LastTimeJumped = 0f;
        // Накопленный угол наклона камеры по вертикали (от -89 до +89).
        float m_CameraVerticalAngle = 0f;
        // Сколько прошло метров — для частоты шагов.
        float m_FootstepDistanceCounter;
        float m_TargetCharacterHeight;

        // Буфер для OverlapCapsule при проверке «можем ли встать в полный рост».
        // 8 хватит — мало что обычно над головой.
        static readonly Collider[] s_StandingOverlapBuffer = new Collider[8];

        // Защитное окно после прыжка — в эти 200 мс не считаем «грунд», иначе
        // CharacterController мгновенно прилипнет обратно к земле.
        const float k_JumpGroundingPreventionTime = 0.2f;
        // Меньшее расстояние ground-check'a когда мы УЖЕ в воздухе — чтобы случайно
        // не зацепиться за невидимый бугорок и не «прилипнуть».
        const float k_GroundCheckDistanceInAir = 0.07f;

        void Awake()
        {
            // Регистрируем себя как «игрока» в ActorsManager. Многие системы (AI)
            // потом берут игрока именно из ActorsManager.Player.
            ActorsManager actorsManager = FindAnyObjectByType<ActorsManager>();
            if (actorsManager != null)
                actorsManager.SetPlayer(gameObject);
        }

        void Start()
        {
            // fetch components on the same gameObject
            // Кешируем все компоненты с проверкой на null.
            m_Controller = GetComponent<CharacterController>();
            DebugUtility.HandleErrorIfNullGetComponent<CharacterController, PlayerCharacterController>(m_Controller,
                this, gameObject);

            m_InputHandler = GetComponent<PlayerInputHandler>();
            DebugUtility.HandleErrorIfNullGetComponent<PlayerInputHandler, PlayerCharacterController>(m_InputHandler,
                this, gameObject);

            m_WeaponsManager = GetComponent<PlayerWeaponsManager>();
            DebugUtility.HandleErrorIfNullGetComponent<PlayerWeaponsManager, PlayerCharacterController>(
                m_WeaponsManager, this, gameObject);

            m_Health = GetComponent<Health>();
            DebugUtility.HandleErrorIfNullGetComponent<Health, PlayerCharacterController>(m_Health, this, gameObject);

            m_Actor = GetComponent<Actor>();
            DebugUtility.HandleErrorIfNullGetComponent<Actor, PlayerCharacterController>(m_Actor, this, gameObject);

            // enableOverlapRecovery — если игрок провалится в стену, контроллер
            // автоматически вытолкнет его обратно. Включаем — лучше для багов.
            m_Controller.enableOverlapRecovery = true;

            // Реагируем на смерть.
            m_Health.OnDie += OnDie;

            // force the crouch state to false when starting
            // На старте всегда стоим в полный рост, без анимации.
            SetCrouchingState(false, true);
            UpdateCharacterHeight(true);
        }

        void Update()
        {
            // check for Y kill
            // Упали ниже карты — мгновенная смерть.
            if (!IsDead && transform.position.y < KillHeight)
            {
                m_Health.Kill();
            }

            HasJumpedThisFrame = false;

            // Запоминаем «было ли на земле», чтобы засечь приземление.
            bool wasGrounded = IsGrounded;
            GroundCheck();

            // landing
            // Только что приземлились — проверяем урон от падения и звук.
            if (IsGrounded && !wasGrounded)
            {
                // Fall damage
                // Берём максимум модуля Y-скорости — либо текущая, либо с момента
                // удара, который мог снизить m_CharacterVelocity.y.
                float fallSpeed = -Mathf.Min(CharacterVelocity.y, m_LatestImpactSpeed.y);
                // ratio: 0 при минимальной скорости падения, 1 при максимальной.
                float fallSpeedRatio = (fallSpeed - MinSpeedForFallDamage) /
                                       (MaxSpeedForFallDamage - MinSpeedForFallDamage);
                if (ReceivesFallDamage && fallSpeedRatio > 0f)
                {
                    float dmgFromFall = Mathf.Lerp(FallDamageAtMinSpeed, FallDamageAtMaxSpeed, fallSpeedRatio);
                    m_Health.TakeDamage(dmgFromFall, null);

                    // fall damage SFX
                    AudioSource.PlayOneShot(FallDamageSfx);
                }
                else
                {
                    // land SFX
                    AudioSource.PlayOneShot(LandSfx);
                }
            }

            // crouching
            // Переключение приседа по нажатию.
            if (m_InputHandler.GetCrouchInputDown())
            {
                SetCrouchingState(!IsCrouching, false);
            }

            // Плавное изменение высоты капсулы и положения камеры (не force).
            UpdateCharacterHeight(false);

            // Главная логика — движение.
            HandleCharacterMovement();
        }

        void OnDie()
        {
            IsDead = true;

            // Tell the weapons manager to switch to a non-existing weapon in order to lower the weapon
            // Индекс -1 → ни одно оружие не активно — модель уходит вниз.
            m_WeaponsManager.SwitchToWeaponIndex(-1, true);

            EventManager.Broadcast(Events.PlayerDeathEvent);
        }

        void GroundCheck()
        {
            // Make sure that the ground check distance while already in air is very small, to prevent suddenly snapping to ground
            // В воздухе используем маленькое расстояние, на земле — побольше + skinWidth.
            float chosenGroundCheckDistance =
                IsGrounded ? (m_Controller.skinWidth + GroundCheckDistance) : k_GroundCheckDistanceInAir;

            // reset values before the ground check
            IsGrounded = false;
            m_GroundNormal = Vector3.up;

            // only try to detect ground if it's been a short amount of time since last jump; otherwise we may snap to the ground instantly after we try jumping
            // Защитный таймер: первые 200 мс после прыжка вообще не считаем grounded.
            if (Time.time >= m_LastTimeJumped + k_JumpGroundingPreventionTime)
            {
                // if we're grounded, collect info about the ground normal with a downward capsule cast representing our character capsule
                // CapsuleCast — бросаем нашу же капсулу вниз. Если что-то нашли — это поверхность.
                if (Physics.CapsuleCast(GetCapsuleBottomHemisphere(), GetCapsuleTopHemisphere(m_Controller.height),
                    m_Controller.radius, Vector3.down, out RaycastHit hit, chosenGroundCheckDistance, GroundCheckLayers,
                    QueryTriggerInteraction.Ignore))
                {
                    // storing the upward direction for the surface found
                    m_GroundNormal = hit.normal;

                    // Only consider this a valid ground hit if the ground normal goes in the same direction as the character up
                    // and if the slope angle is lower than the character controller's limit
                    // Условие grounded:
                    //  1) нормаль смотрит «вверх» (не потолок);
                    //  2) угол склона в пределах slopeLimit.
                    if (Vector3.Dot(hit.normal, transform.up) > 0f &&
                        IsNormalUnderSlopeLimit(m_GroundNormal))
                    {
                        IsGrounded = true;

                        // handle snapping to the ground
                        // Если мы немножко висим над поверхностью — «прилипаем»
                        // (двигаем вниз ровно на hit.distance). Без этого по неровному
                        // полу был бы постоянный «полёт» в маленьких ямках.
                        if (hit.distance > m_Controller.skinWidth)
                        {
                            m_Controller.Move(Vector3.down * hit.distance);
                        }
                    }
                }
            }
        }

        void HandleCharacterMovement()
        {
            // horizontal character rotation
            // Поворот туловища влево-вправо. По Y-оси, через mouse-X.
            {
                // rotate the transform with the input speed around its local Y axis
                transform.Rotate(
                    new Vector3(0f, (m_InputHandler.GetLookInputsHorizontal() * RotationSpeed * RotationMultiplier),
                        0f), Space.Self);
            }

            // vertical camera rotation
            // Камера крутится по X-оси отдельно, чтобы тело не наклонялось.
            {
                // add vertical inputs to the camera's vertical angle
                m_CameraVerticalAngle += m_InputHandler.GetLookInputsVertical() * RotationSpeed * RotationMultiplier;

                // limit the camera's vertical angle to min/max
                // Ограничение, чтобы не «перевернуться» через макушку.
                m_CameraVerticalAngle = Mathf.Clamp(m_CameraVerticalAngle, -89f, 89f);

                // apply the vertical angle as a local rotation to the camera transform along its right axis (makes it pivot up and down)
                PlayerCamera.transform.localEulerAngles = new Vector3(m_CameraVerticalAngle, 0, 0);
            }

            // character movement handling
            bool isSprinting = m_InputHandler.GetSprintInputHeld();
            {
                // Спринт несовместим с приседом — пытаемся встать.
                if (isSprinting)
                {
                    isSprinting = SetCrouchingState(false, false);
                }

                float speedModifier = isSprinting ? SprintSpeedModifier : 1f;

                // converts move input to a worldspace vector based on our character's transform orientation
                // Локальный (x,y,z) ввод от WASD → мировое направление, с учётом
                // куда сейчас повёрнут игрок.
                Vector3 worldspaceMoveInput = transform.TransformVector(m_InputHandler.GetMoveInput());

                // handle grounded movement
                if (IsGrounded)
                {
                    // calculate the desired velocity from inputs, max speed, and current slope
                    Vector3 targetVelocity = worldspaceMoveInput * MaxSpeedOnGround * speedModifier;
                    // reduce speed if crouching by crouch speed ratio
                    if (IsCrouching)
                        targetVelocity *= MaxSpeedCrouchedRatio;
                    // Учитываем уклон — двигаем НЕ просто горизонтально, а вдоль склона.
                    targetVelocity = GetDirectionReorientedOnSlope(targetVelocity.normalized, m_GroundNormal) *
                                     targetVelocity.magnitude;

                    // smoothly interpolate between our current velocity and the target velocity based on acceleration speed
                    // Lerp с (sharpness * dt) — паттерн «экспоненциального сглаживания».
                    CharacterVelocity = Vector3.Lerp(CharacterVelocity, targetVelocity,
                        MovementSharpnessOnGround * Time.deltaTime);

                    // jumping
                    if (IsGrounded && m_InputHandler.GetJumpInputDown())
                    {
                        // force the crouch state to false
                        // Прыгнуть из приседа = сначала встать. Если не получается (низкий потолок) — не прыгаем.
                        if (SetCrouchingState(false, false))
                        {
                            // start by canceling out the vertical component of our velocity
                            // Обнуляем Y чтобы прыжок не суммировался с падением.
                            CharacterVelocity = new Vector3(CharacterVelocity.x, 0f, CharacterVelocity.z);

                            // then, add the jumpSpeed value upwards
                            CharacterVelocity += Vector3.up * JumpForce;

                            // play sound
                            AudioSource.PlayOneShot(JumpSfx);

                            // remember last time we jumped because we need to prevent snapping to ground for a short time
                            m_LastTimeJumped = Time.time;
                            HasJumpedThisFrame = true;

                            // Force grounding to false
                            // Принудительно «оторвались» от земли на этот кадр.
                            IsGrounded = false;
                            m_GroundNormal = Vector3.up;
                        }
                    }

                    // footsteps sound
                    // Шаги: один раз на каждый «метр» (по факту 1/frequency единиц пути).
                    float chosenFootstepSfxFrequency =
                        (isSprinting ? FootstepSfxFrequencyWhileSprinting : FootstepSfxFrequency);
                    if (m_FootstepDistanceCounter >= 1f / chosenFootstepSfxFrequency)
                    {
                        m_FootstepDistanceCounter = 0f;
                        AudioSource.PlayOneShot(FootstepSfx);
                    }

                    // keep track of distance traveled for footsteps sound
                    // Накопитель «пройдено метров с прошлого шага».
                    m_FootstepDistanceCounter += CharacterVelocity.magnitude * Time.deltaTime;
                }
                // handle air movement
                else
                {
                    // add air acceleration
                    // В воздухе только добавляем ввод-ускорение (без Lerp), чтобы
                    // оставалась полётная инерция.
                    CharacterVelocity += worldspaceMoveInput * AccelerationSpeedInAir * Time.deltaTime;

                    // limit air speed to a maximum, but only horizontally
                    // Ограничиваем ТОЛЬКО горизонтальную составляющую — иначе
                    // нельзя было бы падать с большой скоростью.
                    float verticalVelocity = CharacterVelocity.y;
                    Vector3 horizontalVelocity = Vector3.ProjectOnPlane(CharacterVelocity, Vector3.up);
                    horizontalVelocity = Vector3.ClampMagnitude(horizontalVelocity, MaxSpeedInAir * speedModifier);
                    CharacterVelocity = horizontalVelocity + (Vector3.up * verticalVelocity);

                    // apply the gravity to the velocity
                    CharacterVelocity += Vector3.down * GravityDownForce * Time.deltaTime;
                }
            }

            // apply the final calculated velocity value as a character movement
            // Сохраняем позиции капсулы ДО движения — пригодятся для проверки удара.
            Vector3 capsuleBottomBeforeMove = GetCapsuleBottomHemisphere();
            Vector3 capsuleTopBeforeMove = GetCapsuleTopHemisphere(m_Controller.height);
            // Применяем движение через CharacterController — он сам решит коллизии.
            m_Controller.Move(CharacterVelocity * Time.deltaTime);

            // detect obstructions to adjust velocity accordingly
            // Если мы во что-то упёрлись — проецируем скорость на плоскость
            // удара (скользим вдоль стены).
            m_LatestImpactSpeed = Vector3.zero;
            if (Physics.CapsuleCast(capsuleBottomBeforeMove, capsuleTopBeforeMove, m_Controller.radius,
                CharacterVelocity.normalized, out RaycastHit hit, CharacterVelocity.magnitude * Time.deltaTime, -1,
                QueryTriggerInteraction.Ignore))
            {
                // We remember the last impact speed because the fall damage logic might need it
                m_LatestImpactSpeed = CharacterVelocity;

                CharacterVelocity = Vector3.ProjectOnPlane(CharacterVelocity, hit.normal);
            }
        }

        // Returns true if the slope angle represented by the given normal is under the slope angle limit of the character controller
        // Угол между «верхом» персонажа и нормалью поверхности должен быть в пределах slopeLimit.
        bool IsNormalUnderSlopeLimit(Vector3 normal)
        {
            return Vector3.Angle(transform.up, normal) <= m_Controller.slopeLimit;
        }

        // Gets the center point of the bottom hemisphere of the character controller capsule
        // Центры полусфер капсулы (нижней/верхней) — нужны для CapsuleCast.
        Vector3 GetCapsuleBottomHemisphere()
        {
            return transform.position + (transform.up * m_Controller.radius);
        }

        // Gets the center point of the top hemisphere of the character controller capsule
        Vector3 GetCapsuleTopHemisphere(float atHeight)
        {
            return transform.position + (transform.up * (atHeight - m_Controller.radius));
        }

        // Gets a reoriented direction that is tangent to a given slope
        // Берём желаемое направление и наклоняем его «вдоль» склона.
        // Двойной Cross — стандартный приём проецирования на плоскость.
        public Vector3 GetDirectionReorientedOnSlope(Vector3 direction, Vector3 slopeNormal)
        {
            Vector3 directionRight = Vector3.Cross(direction, transform.up);
            return Vector3.Cross(slopeNormal, directionRight).normalized;
        }

        void UpdateCharacterHeight(bool force)
        {
            // Update height instantly
            // force = моментальное изменение (старт, телепорт).
            if (force)
            {
                m_Controller.height = m_TargetCharacterHeight;
                m_Controller.center = Vector3.up * m_Controller.height * 0.5f;
                PlayerCamera.transform.localPosition = Vector3.up * m_TargetCharacterHeight * CameraHeightRatio;
                m_Actor.AimPoint.transform.localPosition = m_Controller.center;
            }
            // Update smooth height
            // Иначе — плавная интерполяция к целевой высоте.
            else if (m_Controller.height != m_TargetCharacterHeight)
            {
                // resize the capsule and adjust camera position
                m_Controller.height = Mathf.Lerp(m_Controller.height, m_TargetCharacterHeight,
                    CrouchingSharpness * Time.deltaTime);
                m_Controller.center = Vector3.up * m_Controller.height * 0.5f;
                PlayerCamera.transform.localPosition = Vector3.Lerp(PlayerCamera.transform.localPosition,
                    Vector3.up * m_TargetCharacterHeight * CameraHeightRatio, CrouchingSharpness * Time.deltaTime);
                m_Actor.AimPoint.transform.localPosition = m_Controller.center;
            }
        }

        // returns false if there was an obstruction
        // Переключение между стойкой и приседом. Возвращает true если успешно.
        bool SetCrouchingState(bool crouched, bool ignoreObstructions)
        {
            // set appropriate heights
            if (crouched)
            {
                m_TargetCharacterHeight = CapsuleHeightCrouching;
            }
            else
            {
                // Detect obstructions
                // При попытке встать — проверяем, не упрёмся ли макушкой в потолок.
                if (!ignoreObstructions)
                {
                    // OverlapCapsuleNonAlloc — NonAlloc-вариант проверки пересечений.
                    // Если найдено что-то кроме нашего же контроллера — нельзя встать.
                    int overlapCount = Physics.OverlapCapsuleNonAlloc(
                        GetCapsuleBottomHemisphere(),
                        GetCapsuleTopHemisphere(CapsuleHeightStanding),
                        m_Controller.radius,
                        s_StandingOverlapBuffer,
                        -1,
                        QueryTriggerInteraction.Ignore);
                    for (int i = 0; i < overlapCount; i++)
                    {
                        if (s_StandingOverlapBuffer[i] != m_Controller)
                            return false;
                    }
                }

                m_TargetCharacterHeight = CapsuleHeightStanding;
            }

            if (OnStanceChanged != null)
            {
                OnStanceChanged.Invoke(crouched);
            }

            IsCrouching = crouched;
            return true;
        }
    }
}
