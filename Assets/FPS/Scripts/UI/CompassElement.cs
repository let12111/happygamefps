using Unity.FPS.Game;
using UnityEngine;
using VContainer;

namespace Unity.FPS.UI
{
    // ============================================================================
    // CompassElement — компонент-«пометка» для объекта мира, который должен быть
    // виден на компасе. Висит на враге, на точке-цели, на пикапе.
    //
    // В Start спавнит свой CompassMarker и регистрирует его в Compass'е.
    // В OnDestroy — снимает с регистрации, чтобы маркер тоже удалился.
    // ============================================================================
    public class CompassElement : MonoBehaviour
    {
        [Tooltip("The marker on the compass for this element")]
        public CompassMarker CompassMarkerPrefab;

        // Только для маркеров-сторон света (N/S/E/W) — текст на маркере.
        [Tooltip("Text override for the marker, if it's a direction")]
        public string TextDirection;

        Compass m_Compass;

        [Inject]
        public void Construct(Compass compass)
        {
            m_Compass = compass;
        }

        void Start()
        {
            var markerInstance = Instantiate(CompassMarkerPrefab);
            markerInstance.Initialize(this, TextDirection);
            m_Compass.RegisterCompassElement(transform, markerInstance);
        }

        void OnDestroy()
        {
            // Compass мог быть уничтожен раньше (выгрузка сцены).
            if (m_Compass != null)
                m_Compass.UnregisterCompassElement(transform);
        }
    }
}
