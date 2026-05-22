using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.FPS.UI
{
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

        void Awake()
        {
            m_Jetpack = FindAnyObjectByType<Jetpack>();
            DebugUtility.HandleErrorIfNullFindObject<Jetpack, JetpackCounter>(m_Jetpack, this);

            FillBarColorChange.Initialize(1f, 0f);
            MainCanvasGroup.gameObject.SetActive(false);
            m_JetpackUnlockedShown = false;
        }

        void Update()
        {
            bool isUnlocked = m_Jetpack.IsJetpackUnlocked;
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