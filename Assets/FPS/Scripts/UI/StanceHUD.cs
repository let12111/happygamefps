using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Unity.FPS.UI
{
    // ============================================================================
    // StanceHUD — иконка стойки игрока (стоит/присел). Реагирует на OnStanceChanged.
    // ============================================================================
    public class StanceHUD : MonoBehaviour
    {
        [Tooltip("Image component for the stance sprites")]
        public Image StanceImage;

        [Tooltip("Sprite to display when standing")]
        public Sprite StandingSprite;

        [Tooltip("Sprite to display when crouching")]
        public Sprite CrouchingSprite;

        PlayerCharacterController m_Character;

        [Inject]
        public void Construct(PlayerCharacterController character)
        {
            m_Character = character;
        }

        void Start()
        {
            m_Character.OnStanceChanged += OnStanceChanged;
            // Сразу установить иконку — без этого до первого приседа была бы пустота.
            OnStanceChanged(m_Character.IsCrouching);
        }

        void OnDestroy()
        {
            if (m_Character != null)
                m_Character.OnStanceChanged -= OnStanceChanged;
        }

        void OnStanceChanged(bool crouched)
        {
            StanceImage.sprite = crouched ? CrouchingSprite : StandingSprite;
        }
    }
}
