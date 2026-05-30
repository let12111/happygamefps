using UnityEngine;
using UnityEngine.UI;

namespace Unity.FPS.UI
{
    // ============================================================================
    // FillBarColorChange — вспомогательный компонент полосок (HP, ammo, jetpack).
    // Меняет цвета при крайних значениях:
    //  - на полной полоске — мигает «обновлено!» (FlashForegroundColorFull);
    //  - на низкой — мигает «опасно!» (FlashBackgroundColorEmpty);
    //  - в обычном диапазоне плавно возвращается к нейтральным цветам.
    //
    // UpdateVisual вызывается каждый кадр родителем (AmmoCounter, JetpackCounter).
    // ============================================================================
    public class FillBarColorChange : MonoBehaviour
    {
        [Header("Foreground")] [Tooltip("Image for the foreground")]
        public Image ForegroundImage;

        [Tooltip("Default foreground color")] public Color DefaultForegroundColor;

        [Tooltip("Flash foreground color when full")]
        public Color FlashForegroundColorFull;

        [Header("Background")] [Tooltip("Image for the background")]
        public Image BackgroundImage;

        [Tooltip("Flash background color when empty")]
        public Color DefaultBackgroundColor;

        [Tooltip("Sharpness for the color change")]
        public Color FlashBackgroundColorEmpty;

        [Header("Values")] [Tooltip("Value to consider full")]
        public float FullValue = 1f;

        [Tooltip("Value to consider empty")] public float EmptyValue = 0f;

        [Tooltip("Sharpness for the color change")]
        public float ColorChangeSharpness = 5f;

        // Прошлое значение — нужно чтобы вспышка «полно» сработала ОДИН раз при переходе.
        float m_PreviousValue;

        public void Initialize(float fullValueRatio, float emptyValueRatio)
        {
            FullValue = fullValueRatio;
            EmptyValue = emptyValueRatio;

            m_PreviousValue = fullValueRatio;
        }

        public void UpdateVisual(float currentRatio)
        {
            // Только что пришли к полной — flash-цвет «готово».
            if (currentRatio == FullValue && currentRatio != m_PreviousValue)
            {
                ForegroundImage.color = FlashForegroundColorFull;
            }
            // Опустились ниже минимума — flash-цвет «опасно».
            else if (currentRatio < EmptyValue)
            {
                BackgroundImage.color = FlashBackgroundColorEmpty;
            }
            else
            {
                // Иначе плавно возвращаемся к нейтральным цветам.
                ForegroundImage.color = Color.Lerp(ForegroundImage.color, DefaultForegroundColor,
                    Time.deltaTime * ColorChangeSharpness);
                BackgroundImage.color = Color.Lerp(BackgroundImage.color, DefaultBackgroundColor,
                    Time.deltaTime * ColorChangeSharpness);
            }

            m_PreviousValue = currentRatio;
        }
    }
}
