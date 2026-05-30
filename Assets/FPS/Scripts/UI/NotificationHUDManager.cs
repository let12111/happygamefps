using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using VContainer;

namespace Unity.FPS.UI
{
    // ============================================================================
    // NotificationHUDManager — спавнит NotificationToast'ы в ответ на события:
    //  - подбор оружия (кроме первого, чтобы не сбивать UX в начале игры);
    //  - разблокировка джетпака;
    //  - текст из ObjectiveUpdateEvent.NotificationText.
    //
    // Зависимости (PlayerWeaponsManager, Jetpack) приходят через DI.
    // ============================================================================
    public class NotificationHUDManager : MonoBehaviour
    {
        [Tooltip("UI panel containing the layoutGroup for displaying notifications")]
        public RectTransform NotificationPanel;

        [Tooltip("Prefab for the notifications")]
        public GameObject NotificationPrefab;

        PlayerWeaponsManager m_PlayerWeaponsManager;
        Jetpack m_Jetpack;

        [Inject]
        public void Construct(PlayerWeaponsManager playerWeaponsManager, Jetpack jetpack)
        {
            m_PlayerWeaponsManager = playerWeaponsManager;
            m_Jetpack = jetpack;
        }

        void Start()
        {
            m_PlayerWeaponsManager.OnAddedWeapon += OnPickupWeapon;
            m_Jetpack.OnUnlockJetpack += OnUnlockJetpack;

            EventManager.AddListener<ObjectiveUpdateEvent>(OnObjectiveUpdateEvent);
        }

        void OnObjectiveUpdateEvent(ObjectiveUpdateEvent evt)
        {
            // NotificationText опционален — пустой не показываем.
            if (!string.IsNullOrEmpty(evt.NotificationText))
                CreateNotification(evt.NotificationText);
        }

        void OnPickupWeapon(WeaponController weaponController, int index)
        {
            // Первое оружие (index 0) — стартовое, не показываем уведомление.
            // Иначе игрок видел бы «получено оружие» сразу при загрузке.
            if (index != 0)
                CreateNotification($"Picked up weapon : {weaponController.WeaponName}");
        }

        void OnUnlockJetpack(bool unlock)
        {
            CreateNotification("Jetpack unlocked");
        }

        public void CreateNotification(string text)
        {
            GameObject notificationInstance = Instantiate(NotificationPrefab, NotificationPanel);
            // SetSiblingIndex(0) → новый toast сверху, старые сдвигаются вниз.
            notificationInstance.transform.SetSiblingIndex(0);

            NotificationToast toast = notificationInstance.GetComponent<NotificationToast>();
            if (toast)
            {
                toast.Initialize(text);
            }
        }

        void OnDestroy()
        {
            if (m_PlayerWeaponsManager != null)
                m_PlayerWeaponsManager.OnAddedWeapon -= OnPickupWeapon;

            if (m_Jetpack != null)
                m_Jetpack.OnUnlockJetpack -= OnUnlockJetpack;

            EventManager.RemoveListener<ObjectiveUpdateEvent>(OnObjectiveUpdateEvent);
        }
    }
}
