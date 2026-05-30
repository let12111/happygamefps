
using UnityEngine;

namespace Unity.FPS.Gameplay
{
    // ============================================================================
    // PositionBobbing — упрощённая версия Pickup-bobbing'а без триггера/подбора.
    // Просто крутит объект вверх-вниз по синусу. Используется для декоративных
    // предметов в мире (например, парящие шары).
    // ============================================================================
    public class PositionBobbing : MonoBehaviour
    {
        [Tooltip("Frequency at which the item will move up and down")]
        public float VerticalBobFrequency = 1f;

        [Tooltip("Distance the item will move up and down")]
        public float BobbingAmount = 0.5f;

        Vector3 m_StartPosition;

        void Start()
        {
            // Remember start position for animation
            m_StartPosition = transform.position;
        }

        void Update()
        {
            // Handle bobbing
            // (sin*0.5+0.5) даёт 0..1 — объект не опускается ниже стартовой Y.
            float bobbingAnimationPhase = ((Mathf.Sin(Time.time * VerticalBobFrequency) * 0.5f) + 0.5f) * BobbingAmount;
            transform.position = m_StartPosition + Vector3.up * bobbingAnimationPhase;
        }
    }
}
