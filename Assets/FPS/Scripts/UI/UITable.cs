using UnityEngine;

namespace Unity.FPS.UI
{
    // The component that is used to display the Objectives, the Notification and the game messages like a list
    // When a new one is created, the previous ones move down to make room for the new one

    // ============================================================================
    // UITable — вертикальный список UI-элементов с фиксированным отступом.
    // Используется для списка целей в HUD, всплывашек, сообщений.
    //
    // При добавлении нового элемента (UpdateTable) пересчитывает позиции всех
    // детей, размещая их сверху-вниз (или снизу-вверх если Down=true) с отступом.
    //
    // Это самописная замена VerticalLayoutGroup — нужна тут потому, что отступы
    // считаются с учётом pivot.y каждого элемента, а это нестандартно.
    // ============================================================================
    public class UITable : MonoBehaviour
    {
        [Tooltip("How much space should there be between items?")]
        public float Offset;

        [Tooltip("Add new the new items below existing items.")]
        public bool Down;

        public void UpdateTable(GameObject newItem)
        {
            if (newItem != null)
                // Сбрасываем масштаб — иначе анимация «масштаб 0→1» сломает позиции.
                newItem.GetComponent<RectTransform>().localScale = Vector3.one;

            float height = 0;
            for (int i = 0; i < transform.childCount; i++)
            {
                RectTransform child = transform.GetChild(i).GetComponent<RectTransform>();
                Vector2 size = child.sizeDelta;
                // Учитываем pivot: (1-pivot.y)*size.y = расстояние от pivot до нижнего края.
                height += Down ? -(1 - child.pivot.y) * size.y : (1 - child.pivot.y) * size.y;
                // Между элементами добавляем Offset (кроме первого).
                if (i != 0)
                    height += Down ? -Offset : Offset;

                Vector2 newPos = Vector2.zero;

                newPos.y = height;
                newPos.x = 0;//-child.pivot.x * size.x * hi.localScale.x;
                child.anchoredPosition = newPos;
            }
        }
    }
}
