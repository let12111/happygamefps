using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.FPS.UI
{
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
            m_Camera = Camera.main;
        }

        void Update()
        {
            float fill = Health.CurrentHealth / Health.MaxHealth;
            HealthBarImage.fillAmount = fill;

            HealthBarPivot.LookAt(m_Camera.transform.position);

            if (HideFullHealthBar)
            {
                bool shouldBeVisible = fill < 1f;
                if (shouldBeVisible != m_BarVisible)
                {
                    m_BarVisible = shouldBeVisible;
                    HealthBarPivot.gameObject.SetActive(m_BarVisible);
                }
            }
        }
    }
}