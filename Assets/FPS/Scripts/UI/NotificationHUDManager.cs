using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace Unity.FPS.UI
{
    public class NotificationHUDManager : MonoBehaviour
    {
        [Tooltip("UI panel containing the layoutGroup for displaying notifications")]
        public RectTransform NotificationPanel;

        [Tooltip("Prefab for the notifications")]
        public GameObject NotificationPrefab;

        PlayerWeaponsManager m_PlayerWeaponsManager;
        Jetpack m_Jetpack;

        void Awake()
        {
            m_PlayerWeaponsManager = FindAnyObjectByType<PlayerWeaponsManager>();
            DebugUtility.HandleErrorIfNullFindObject<PlayerWeaponsManager, NotificationHUDManager>(m_PlayerWeaponsManager,
                this);
            m_PlayerWeaponsManager.OnAddedWeapon += OnPickupWeapon;

            m_Jetpack = FindAnyObjectByType<Jetpack>();
            DebugUtility.HandleErrorIfNullFindObject<Jetpack, NotificationHUDManager>(m_Jetpack, this);
            m_Jetpack.OnUnlockJetpack += OnUnlockJetpack;

            EventManager.AddListener<ObjectiveUpdateEvent>(OnObjectiveUpdateEvent);
        }

        void OnObjectiveUpdateEvent(ObjectiveUpdateEvent evt)
        {
            if (!string.IsNullOrEmpty(evt.NotificationText))
                CreateNotification(evt.NotificationText);
        }

        void OnPickupWeapon(WeaponController weaponController, int index)
        {
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