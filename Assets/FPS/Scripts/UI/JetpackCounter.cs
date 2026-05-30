using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Unity.FPS.UI
{
    // ============================================================================
    // JetpackCounter — индикатор топлива джетпака в HUD.
    // Появляется только когда джетпак РАЗБЛОКИРОВАН — до этого скрыт.
    //
    // Использует FillBarColorChange для мигания пустой/полной полоски.
    // ============================================================================
    public class JetpackCounter : MonoBehaviour
    {
        [Tooltip("Image component representing jetpack fuel")]
        public Image JetpackFillImage;

        [Tooltip("Canvas group that contains the whole UI for the jetack")]
        public CanvasGroup MainCanvasGroup;

        [Tooltip("Component to animate the color when empty or full")]
        public FillBarColorChange FillBarColorChange;

        Jetpack m_Jetpack;
        bool m_JetpackUnlockedShown;

        [Inject]
        public void Construct(Jetpack jetpack)
        {
            m_Jetpack = jetpack;
        }

        void Awake()
        {
            FillBarColorChange.Initialize(1f, 0f);
            // Начинаем скрытыми. SetActive снова — когда разблокируется.
            MainCanvasGroup.gameObject.SetActive(false);
            m_JetpackUnlockedShown = false;
        }

        void Update()
        {
            bool isUnlocked = m_Jetpack.IsJetpackUnlocked;
            // Смена видимости — только при изменении (не каждый кадр).
            if (isUnlocked != m_JetpackUnlockedShown)
            {
                m_JetpackUnlockedShown = isUnlocked;
                MainCanvasGroup.gameObject.SetActive(isUnlocked);
            }

            if (isUnlocked)
            {
                JetpackFillImage.fillAmount = m_Jetpack.CurrentFillRatio;
                FillBarColorChange.UpdateVisual(m_Jetpack.CurrentFillRatio);
            }
        }
    }
}
