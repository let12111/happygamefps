using Unity.FPS.Game;
using UnityEngine;
using VContainer;

namespace Unity.FPS.UI
{
    public class CompassElement : MonoBehaviour
    {
        [Tooltip("The marker on the compass for this element")]
        public CompassMarker CompassMarkerPrefab;

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
            if (m_Compass != null)
                m_Compass.UnregisterCompassElement(transform);
        }
    }
}
