using Unity.FPS.AI;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.FPS.UI
{
    // ============================================================================
    // CompassMarker — иконка на полоске компаса. Бывает двух типов:
    //  - "Направление" (N/E/S/W) — текстовая метка магнитной стороны;
    //  - "Враг" — графический маркер врага. Меняет цвет когда враг увидел игрока.
    //
    // Цвет переключается через подписку на onDetectedTarget/onLostTarget у EnemyController.
    // ============================================================================
    public class CompassMarker : MonoBehaviour
    {
        [Tooltip("Main marker image")] public Image MainImage;

        [Tooltip("Canvas group for the marker")]
        public CanvasGroup CanvasGroup;

        [Header("Enemy element")] [Tooltip("Default color for the marker")]
        public Color DefaultColor;

        [Tooltip("Alternative color for the marker")]
        public Color AltColor;

        [Header("Direction element")] [Tooltip("Use this marker as a magnetic direction")]
        public bool IsDirection;

        [Tooltip("Text content for the direction")]
        public TMPro.TextMeshProUGUI TextContent;

        EnemyController m_EnemyController;

        public void Initialize(CompassElement compassElement, string textDirection)
        {
            if (IsDirection && TextContent)
            {
                // Маркер «N/E/S/W» — пишем буквы стороны света.
                TextContent.text = textDirection;
            }
            else
            {
                // Маркер врага — подписываемся на его события.
                m_EnemyController = compassElement.transform.GetComponent<EnemyController>();

                if (m_EnemyController)
                {
                    m_EnemyController.onDetectedTarget += DetectTarget;
                    m_EnemyController.onLostTarget += LostTarget;

                    LostTarget();
                }
            }
        }

        // Враг увидел игрока — маркер в «тревожный» цвет.
        public void DetectTarget()
        {
            MainImage.color = AltColor;
        }

        public void LostTarget()
        {
            MainImage.color = DefaultColor;
        }
    }
}
