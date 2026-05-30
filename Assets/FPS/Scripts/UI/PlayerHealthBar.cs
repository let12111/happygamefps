using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Unity.FPS.UI
{
    // ============================================================================
    // PlayerHealthBar — полоска HP игрока в HUD. Обновляется по событиям
    // OnDamaged/OnHealed (а не каждый кадр) — экономия в Update.
    //
    // DI через [Inject]: PlayerCharacterController приходит из GameLifetimeScope,
    // не нужно искать его FindObject'ом.
    // ============================================================================
    public class PlayerHealthBar : MonoBehaviour
    {
        [Tooltip("Image component dispplaying current health")]
        public Image HealthFillImage;

        Health m_PlayerHealth;

        [Inject]
        public void Construct(PlayerCharacterController playerCharacterController)
        {
            m_PlayerHealth = playerCharacterController.GetComponent<Health>();
            DebugUtility.HandleErrorIfNullGetComponent<Health, PlayerHealthBar>(m_PlayerHealth, this,
                playerCharacterController.gameObject);
        }

        void Start()
        {
            m_PlayerHealth.OnDamaged += OnHealthChanged;
            m_PlayerHealth.OnHealed += OnHealthHealed;
            UpdateHealthBar();
        }

        void OnDestroy()
        {
            if (m_PlayerHealth != null)
            {
                m_PlayerHealth.OnDamaged -= OnHealthChanged;
                m_PlayerHealth.OnHealed -= OnHealthHealed;
            }
        }

        // Лямбды-обёртки с одной целью — у OnDamaged и OnHealed разные сигнатуры.
        void OnHealthChanged(float amount, GameObject source) => UpdateHealthBar();
        void OnHealthHealed(float amount) => UpdateHealthBar();

        void UpdateHealthBar()
        {
            HealthFillImage.fillAmount = m_PlayerHealth.CurrentHealth / m_PlayerHealth.MaxHealth;
        }
    }
}
