using UnityEngine;

namespace Unity.FPS.Game
{
    // ============================================================================
    // MinMaxParameters — три структуры для интерполяции «от min до max» по
    // нормализованному коэффициенту (0..1).
    //
    // Зачем struct, а не class: эти штуки — простые данные (POD), их часто
    // создают пачками. struct живёт в стеке, не создаёт мусора для GC.
    //
    // Зачем [System.Serializable]: чтобы Unity мог показать их в Inspector
    // как сворачивающийся блок с двумя полями Min/Max.
    //
    // GetValueFromRatio(0) вернёт Min, GetValueFromRatio(1) вернёт Max,
    // GetValueFromRatio(0.5) — середину. Удобно для эффектов: «по мере зарядки
    // оружия цвет меняется от синего к красному».
    // ============================================================================

    [System.Serializable]
    public struct MinMaxFloat
    {
        public float Min;
        public float Max;

        // Mathf.Lerp(a, b, t) — линейная интерполяция: a + (b-a)*t,
        // зажимая t в [0..1]. Базовая операция плавных переходов.
        public float GetValueFromRatio(float ratio)
        {
            return Mathf.Lerp(Min, Max, ratio);
        }
    }

    [System.Serializable]
    public struct MinMaxColor
    {
        // [ColorUsage(true, true)] разрешает выбирать цвет с прозрачностью (alpha)
        // и HDR-яркостью (значения > 1) — нужно для эмиссии материалов и эффектов.
        [ColorUsage(true, true)] public Color Min;
        [ColorUsage(true, true)] public Color Max;

        public Color GetValueFromRatio(float ratio)
        {
            return Color.Lerp(Min, Max, ratio);
        }
    }

    [System.Serializable]
    public struct MinMaxVector3
    {
        public Vector3 Min;
        public Vector3 Max;

        // Vector3.Lerp — покомпонентная интерполяция X, Y, Z.
        public Vector3 GetValueFromRatio(float ratio)
        {
            return Vector3.Lerp(Min, Max, ratio);
        }
    }
}
