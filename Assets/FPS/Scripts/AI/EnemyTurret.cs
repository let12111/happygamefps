using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.AI
{
    // ============================================================================
    // EnemyTurret — стационарный враг-турель. Не двигается, только крутит ствол.
    //
    // Автомат из 2 состояний:
    //   Idle → Attack (при обнаружении цели).
    //   Attack → Idle (при потере цели).
    //
    // Особенность турели: «ствол» и «пивот» — это разные Transform'ы. Пивот
    // надо повернуть так, чтобы дуло оружия (WeaponMuzzle) смотрело в цель.
    // Поэтому в Start запоминаем смещение между ними (m_RotationWeaponForwardToPivot)
    // и при нацеливании умножаем на это смещение.
    // ============================================================================
    [RequireComponent(typeof(EnemyController))]
    public class EnemyTurret : MonoBehaviour
    {
        public enum AIState
        {
            Idle,
            Attack,
        }

        public Transform TurretPivot;
        public Transform TurretAimPoint;
        public Animator Animator;
        // Скорость поворота когда УЖЕ стреляет (быстрая, чтобы цель не убежала).
        public float AimRotationSharpness = 5f;
        // Скорость поворота когда «всматривается» (плавнее).
        public float LookAtRotationSharpness = 2.5f;
        // Задержка после обнаружения до первого выстрела (дать игроку шанс убежать).
        public float DetectionFireDelay = 1f;
        // Время плавного возврата ствола в idle-позицию.
        public float AimingTransitionBlendTime = 1f;

        [Tooltip("The random hit damage effects")]
        public ParticleSystem[] RandomHitSparks;

        public ParticleSystem[] OnDetectVfx;
        public AudioClip OnDetectSfx;

        public AIState AiState { get; private set; }

        EnemyController m_EnemyController;
        Health m_Health;
        // Кешируем «угол между WeaponMuzzle.forward и Pivot.forward».
        Quaternion m_RotationWeaponForwardToPivot;
        float m_TimeStartedDetection;
        float m_TimeLostDetection;
        Quaternion m_PreviousPivotAimingRotation;
        Quaternion m_PivotAimingRotation;

        const string k_AnimOnDamagedParameter = "OnDamaged";
        const string k_AnimIsActiveParameter = "IsActive";

        void Start()
        {
            m_Health = GetComponent<Health>();
            DebugUtility.HandleErrorIfNullGetComponent<Health, EnemyTurret>(m_Health, this, gameObject);
            m_Health.OnDamaged += OnDamaged;

            m_EnemyController = GetComponent<EnemyController>();
            DebugUtility.HandleErrorIfNullGetComponent<EnemyController, EnemyTurret>(m_EnemyController, this,
                gameObject);

            m_EnemyController.onDetectedTarget += OnDetectedTarget;
            m_EnemyController.onLostTarget += OnLostTarget;

            // Remember the rotation offset between the pivot's forward and the weapon's forward
            // Если дуло пушки смотрит вправо, а пивот — вперёд, нам нужно
            // прибавлять угол 90° при повороте пивота. Inverse(muzzle) * pivot
            // даёт «насколько пивот ОТСТАЁТ от forward'а ствола».
            m_RotationWeaponForwardToPivot =
                Quaternion.Inverse(m_EnemyController.GetCurrentWeapon().WeaponMuzzle.rotation) * TurretPivot.rotation;

            // Start with idle
            AiState = AIState.Idle;

            m_TimeStartedDetection = Mathf.NegativeInfinity;
            m_PreviousPivotAimingRotation = TurretPivot.rotation;
        }

        void Update()
        {
            UpdateCurrentAiState();
        }

        // LateUpdate — после анимаций. Тут перезаписываем поворот пивота,
        // чтобы аниматор не «перетёр» наш расчёт.
        void LateUpdate()
        {
            UpdateTurretAiming();
        }

        void UpdateCurrentAiState()
        {
            // Handle logic
            switch (AiState)
            {
                case AIState.Attack:
                    if (m_EnemyController.KnownDetectedTarget == null) { AiState = AIState.Idle; break; }
                    bool mustShoot = Time.time > m_TimeStartedDetection + DetectionFireDelay;
                    // Calculate the desired rotation of our turret (aim at target)
                    Vector3 directionToTarget =
                        (m_EnemyController.KnownDetectedTarget.transform.position - TurretAimPoint.position).normalized;
                    // LookRotation(направление) * смещение = поворот ПИВОТА, при котором
                    // ствол будет смотреть в цель.
                    Quaternion offsettedTargetRotation =
                        Quaternion.LookRotation(directionToTarget) * m_RotationWeaponForwardToPivot;
                    // Плавный поворот: быстрее когда стреляем, медленнее когда «всматриваемся».
                    m_PivotAimingRotation = Quaternion.Slerp(m_PreviousPivotAimingRotation, offsettedTargetRotation,
                        (mustShoot ? AimRotationSharpness : LookAtRotationSharpness) * Time.deltaTime);

                    // shoot
                    if (mustShoot)
                    {
                        // Считаем направление, КУДА реально сейчас смотрит ствол —
                        // не туда, куда цель. Стреляем туда (так попадаем в раннюю
                        // фазу поворота, когда ствол ещё не довёлся).
                        Vector3 correctedDirectionToTarget =
                            (m_PivotAimingRotation * Quaternion.Inverse(m_RotationWeaponForwardToPivot)) *
                            Vector3.forward;

                        m_EnemyController.TryAttack(TurretAimPoint.position + correctedDirectionToTarget);
                    }

                    break;
            }
        }

        void UpdateTurretAiming()
        {
            switch (AiState)
            {
                case AIState.Attack:
                    // В атаке — применяем рассчитанный поворот.
                    TurretPivot.rotation = m_PivotAimingRotation;
                    break;
                default:
                    // Use the turret rotation of the animation
                    // В Idle — плавно переходим обратно к анимационному idle-повороту.
                    TurretPivot.rotation = Quaternion.Slerp(m_PivotAimingRotation, TurretPivot.rotation,
                        (Time.time - m_TimeLostDetection) / AimingTransitionBlendTime);
                    break;
            }

            m_PreviousPivotAimingRotation = TurretPivot.rotation;
        }

        void OnDamaged(float dmg, GameObject source)
        {
            if (RandomHitSparks.Length > 0)
            {
                int n = Random.Range(0, RandomHitSparks.Length);
                RandomHitSparks[n].Play();
            }

            Animator.SetTrigger(k_AnimOnDamagedParameter);
        }

        void OnDetectedTarget()
        {
            if (AiState == AIState.Idle)
            {
                AiState = AIState.Attack;
            }

            for (int i = 0; i < OnDetectVfx.Length; i++)
            {
                OnDetectVfx[i].Play();
            }

            if (OnDetectSfx)
            {
                AudioUtility.CreateSFX(OnDetectSfx, transform.position, AudioUtility.AudioGroups.EnemyDetection, 1f);
            }

            Animator.SetBool(k_AnimIsActiveParameter, true);
            // Запоминаем время обнаружения для DetectionFireDelay.
            m_TimeStartedDetection = Time.time;
        }

        void OnLostTarget()
        {
            if (AiState == AIState.Attack)
            {
                AiState = AIState.Idle;
            }

            for (int i = 0; i < OnDetectVfx.Length; i++)
            {
                OnDetectVfx[i].Stop();
            }

            Animator.SetBool(k_AnimIsActiveParameter, false);
            // Запоминаем время потери для плавного возврата.
            m_TimeLostDetection = Time.time;
        }
    }
}
