using System.Collections.Generic;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using VContainer;

namespace Unity.FPS.UI
{
    // ============================================================================
    // Compass — горизонтальная полоска компаса вверху экрана.
    //
    // Принцип: каждый зарегистрированный объект мира (CompassElement) имеет свой
    // CompassMarker — маленькую иконку. Каждый кадр пересчитываем угол от forward
    // игрока до направления на объект; преобразуем угол в X-координату на полоске.
    //
    // Если угол вне видимого «конуса» (VisibilityAngle) — маркер прозрачен.
    // Удалённые объекты — мельче (lerp 1→MinScale по DistanceMinScale).
    // ============================================================================
    public class Compass : MonoBehaviour
    {
        public RectTransform CompasRect;
        // Угловой охват компаса. 180 = видна вся передняя полусфера.
        public float VisibilityAngle = 180f;
        // На сколько Y объекта в мире смещает Y маркера (поднимает «выше» на компасе).
        public float HeightDifferenceMultiplier = 2f;
        // Минимальный масштаб маркера (для далёких объектов).
        public float MinScale = 0.5f;
        // На какой дистанции маркер достигает MinScale.
        public float DistanceMinScale = 50f;
        // Сколько процентов высоты компаса можем использовать (чтобы маркеры
        // не вылезали за рамку при сильном смещении по Y).
        public float CompasMarginRatio = 0.8f;

        public GameObject MarkerDirectionPrefab;

        Transform m_PlayerTransform;
        // По Transform мирового объекта быстро находим его CompassMarker.
        Dictionary<Transform, CompassMarker> m_ElementsDictionnary = new Dictionary<Transform, CompassMarker>();

        // Пиксели на градус — для перевода «угол» → «позиция X на полоске».
        float m_WidthMultiplier;
        float m_HeightOffset;

        [Inject]
        public void Construct(PlayerCharacterController playerCharacterController)
        {
            m_PlayerTransform = playerCharacterController.transform;
        }

        void Start()
        {
            // Кешируем коэффициент конверсии «угол → пиксель».
            m_WidthMultiplier = CompasRect.rect.width / VisibilityAngle;
            m_HeightOffset = -CompasRect.rect.height / 2;
        }

        void Update()
        {
            Vector3 playerPosition = m_PlayerTransform.position;
            // Forward игрока, спроецированный на горизонталь — компас 2D.
            Vector3 playerForwardFlat = Vector3.ProjectOnPlane(m_PlayerTransform.forward, Vector3.up);
            float halfVisAngle = VisibilityAngle * 0.5f;
            float clampedHalfHeight = CompasRect.rect.height * 0.5f * CompasMarginRatio;

            foreach (var element in m_ElementsDictionnary)
            {
                float distanceRatio = 1f;
                float heightDifference = 0f;
                float angle;

                if (element.Value.IsDirection)
                {
                    // Маркер «сторона света» — угол берётся не от мировой позиции,
                    // а от локального направления маркера (фиксированное N/S/E/W).
                    angle = Vector3.SignedAngle(m_PlayerTransform.forward,
                        element.Key.transform.localPosition.normalized, Vector3.up);
                }
                else
                {
                    // Обычный объект — угол между forward'ом игрока и направлением на объект.
                    Vector3 directionVector = element.Key.transform.position - playerPosition;
                    Vector3 dirFlat = Vector3.ProjectOnPlane(directionVector, Vector3.up).normalized;
                    angle = Vector3.SignedAngle(playerForwardFlat, dirFlat, Vector3.up);

                    // Объект выше нас — поднимем маркер на компасе. Зажимаем,
                    // чтобы не вылез за CompasMarginRatio.
                    heightDifference = Mathf.Clamp(directionVector.y * HeightDifferenceMultiplier,
                        -clampedHalfHeight, clampedHalfHeight);

                    // Доля «как далеко»: 0 рядом, 1 на DistanceMinScale и дальше.
                    distanceRatio = Mathf.Clamp01(directionVector.magnitude / DistanceMinScale);
                }

                // Внутри видимого охвата — показываем, иначе прозрачен.
                if (angle > -halfVisAngle && angle < halfVisAngle)
                {
                    element.Value.CanvasGroup.alpha = 1;
                    element.Value.CanvasGroup.transform.localPosition = new Vector2(m_WidthMultiplier * angle,
                        heightDifference + m_HeightOffset);
                    element.Value.CanvasGroup.transform.localScale =
                        Vector3.one * Mathf.Lerp(1, MinScale, distanceRatio);
                }
                else
                {
                    element.Value.CanvasGroup.alpha = 0;
                }
            }
        }

        // CompassElement вызывает при появлении объекта.
        public void RegisterCompassElement(Transform element, CompassMarker marker)
        {
            marker.transform.SetParent(CompasRect);
            m_ElementsDictionnary.Add(element, marker);
        }

        public void UnregisterCompassElement(Transform element)
        {
            // Удаляем и сам маркер тоже.
            if (m_ElementsDictionnary.TryGetValue(element, out CompassMarker marker) && marker.CanvasGroup != null)
                Destroy(marker.CanvasGroup.gameObject);
            m_ElementsDictionnary.Remove(element);
        }
    }
}
