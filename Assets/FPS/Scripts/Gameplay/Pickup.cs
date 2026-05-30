using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.Gameplay
{
    // ============================================================================
    // Pickup — базовый класс всех подбираемых предметов.
    //
    // Что даёт:
    //  - покачивание вверх-вниз + вращение (визуальная привлекательность);
    //  - триггерная зона: при касании игрока вызывает OnPicked;
    //  - стандартный фидбек (звук + VFX) при подборе.
    //
    // Конкретные подклассы переопределяют OnPicked: HealthPickup лечит,
    // AmmoPickup даёт патроны, WeaponPickup добавляет оружие, JetpackPickup
    // разблокирует джетпак.
    //
    // [RequireComponent] — Rigidbody (kinematic) + Collider (trigger) обязательны.
    // ============================================================================
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public class Pickup : MonoBehaviour
    {
        [Tooltip("Frequency at which the item will move up and down")]
        public float VerticalBobFrequency = 1f;

        [Tooltip("Distance the item will move up and down")]
        public float BobbingAmount = 1f;

        [Tooltip("Rotation angle per second")] public float RotatingSpeed = 360f;

        [Tooltip("Sound played on pickup")] public AudioClip PickupSfx;
        [Tooltip("VFX spawned on pickup")] public GameObject PickupVfxPrefab;

        // public set для наследников, но snapshot — отдельная переменная m_StartPosition.
        public Rigidbody PickupRigidbody { get; private set; }

        Collider m_Collider;
        // Стартовая Y, относительно неё качаемся. Без неё мы бы плавали всё выше.
        Vector3 m_StartPosition;
        // Защита от двойного звука/VFX при подборе.
        bool m_HasPlayedFeedback;

        // virtual — наследники могут расширить (WeaponPickup переопределяет, чтобы сменить слой).
        // protected — Unity вызовет, но снаружи метод скрыт.
        protected virtual void Start()
        {
            PickupRigidbody = GetComponent<Rigidbody>();
            DebugUtility.HandleErrorIfNullGetComponent<Rigidbody, Pickup>(PickupRigidbody, this, gameObject);
            m_Collider = GetComponent<Collider>();
            DebugUtility.HandleErrorIfNullGetComponent<Collider, Pickup>(m_Collider, this, gameObject);

            // ensure the physics setup is a kinematic rigidbody trigger
            // Kinematic = на него не действует физика (он сам не падает),
            // но Unity всё равно вызовет OnTriggerEnter. isTrigger = безостановочное «прохождение».
            PickupRigidbody.isKinematic = true;
            m_Collider.isTrigger = true;

            // Remember start position for animation
            m_StartPosition = transform.position;
        }

        void Update()
        {
            // Handle bobbing
            // sin даёт от -1 до +1. Берём (sin*0.5+0.5) → 0..1 — чтобы не уходить НИЖЕ start.
            float bobbingAnimationPhase = ((Mathf.Sin(Time.time * VerticalBobFrequency) * 0.5f) + 0.5f) * BobbingAmount;
            transform.position = m_StartPosition + Vector3.up * bobbingAnimationPhase;

            // Handle rotating
            transform.Rotate(Vector3.up, RotatingSpeed * Time.deltaTime, Space.Self);
        }

        // OnTriggerEnter — Unity вызывает когда коллайдер вошёл в наш триггер.
        void OnTriggerEnter(Collider other)
        {
            // Только игрок может подбирать. У врагов нет PlayerCharacterController.
            PlayerCharacterController pickingPlayer = other.GetComponent<PlayerCharacterController>();

            if (pickingPlayer != null)
            {
                OnPicked(pickingPlayer);

                // Шлём общее событие подбора. На него слушают объективы и т.п.
                PickupEvent evt = Events.PickupEvent;
                evt.Pickup = gameObject;
                EventManager.Broadcast(evt);
            }
        }

        // Виртуальный hook — наследники реализуют свою логику.
        // Базовый класс по умолчанию проигрывает фидбек.
        protected virtual void OnPicked(PlayerCharacterController playerController)
        {
            PlayPickupFeedback();
        }

        public void PlayPickupFeedback()
        {
            // Защита от повторного вызова.
            if (m_HasPlayedFeedback)
                return;

            if (PickupSfx)
            {
                // 2D-звук (spatialBlend=0) — для UI-feedback стиля.
                AudioUtility.CreateSFX(PickupSfx, transform.position, AudioUtility.AudioGroups.Pickup, 0f);
            }

            // VFX подбора через пул, возврат через 5 секунд.
            if (PickupVfxPrefab)
            {
                var vfx = GameObjectPoolManager.Instance.Get(PickupVfxPrefab, transform.position, Quaternion.identity);
                GameObjectPoolManager.Instance.ReleaseDelayed(vfx, 5f);
            }

            m_HasPlayedFeedback = true;
        }
    }
}
