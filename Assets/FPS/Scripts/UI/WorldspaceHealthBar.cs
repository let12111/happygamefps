using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.FPS.UI
{
    // ============================================================================
    // WorldspaceHealthBar — полоска HP «в мире», висящая над врагом.
    //
    // Работает в World Space Canvas. Нюансы:
    //  - смотрит на камеру через LookAt — чтобы выглядеть «билбордом»;
    //  - кеширует Camera.main в Start (Camera.main внутри = FindWithTag, дорого);
    //  - при HideFullHealthBar=true скрывается на полном HP (показывается только когда били).
    // ============================================================================
    public class WorldspaceHealthBar : MonoBehaviour
    {
        [Tooltip("Health component to track")] public Health Health;

        [Tooltip("Image component displaying health left")]
        public Image HealthBarImage;

        [Tooltip("The floating healthbar pivot transform")]
        public Transform HealthBarPivot;

        [Tooltip("Whether the health bar is visible when at full health or not")]
        public bool HideFullHealthBar = true;

        Camera m_Camera;
        bool m_BarVisible = true;

        void Start()
        {
            // Кешируем Camera.main один раз. См. CLAUDE.md «Camera.main Caching».
            m_Camera = Camera.main;
        }

        void Update()
        {
            if (m_Camera == null) return;

            float fill = Health.CurrentHealth / Health.MaxHealth;
            HealthBarImage.fillAmount = fill;

            // Билборд: полоска всегда повёрнута лицом к камере.
            HealthBarPivot.LookAt(m_Camera.transform.position);

            // Скрываем при полном HP — каждый дополнительный UI-элемент в кадре
            // это лишний DrawCall, лучше показывать только когда есть смысл.
            if (HideFullHealthBar)
            {
                bool shouldBeVisible = fill < 1f;
                // SetActive вызываем только при смене состояния — он не бесплатный.
                if (shouldBeVisible != m_BarVisible)
                {
                    m_BarVisible = shouldBeVisible;
                    HealthBarPivot.gameObject.SetActive(m_BarVisible);
                }
            }
        }
    }
}
