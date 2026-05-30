using System.Collections.Generic;
using UnityEngine;

namespace Unity.FPS.Game
{
    // ============================================================================
    // MeshCombiner — компонент уровня. Берёт кучу статичных мешей и объединяет их
    // в один большой меш для каждой комбинации «материал + submesh».
    //
    // Зачем: каждый MeshRenderer = отдельный draw call. На сложном уровне их могут
    // быть сотни, и GPU тратит время на переключение. После объединения остаётся
    // несколько крупных рендереров — заметно быстрее.
    //
    // Минус: combined-меш не двигается. Поэтому годится только для статичной геометрии
    // (стены, пол, декорации), не для дверей или подвижных платформ.
    //
    // Режим сетки (UseGrid) делит объединение на ячейки, чтобы не делать ОДИН
    // гигантский меш — тогда нельзя было бы делать frustum culling по частям.
    // ============================================================================
    public class MeshCombiner : MonoBehaviour
    {
        // Корни иерархии, чьих потомков с MeshRenderer'ом надо объединить.
        public List<GameObject> CombineParents = new List<GameObject>();

        // ----- Параметры режима сетки -----
        [Header("Grid parameters")] public bool UseGrid = false;
        public Vector3 GridCenter;
        public Vector3 GridExtents = new Vector3(10, 10, 10);
        // Сколько ячеек по каждой оси (например, 2x2x2 = 8 ячеек).
        public Vector3Int GridResolution = new Vector3Int(2, 2, 2);
        // Цвет рамки в редакторе для предпросмотра сетки.
        public Color GridPreviewColor = Color.green;

        // Автоматический запуск при старте сцены.
        void Start()
        {
            Combine();
        }

        public void Combine()
        {
            // Собираем все MeshRenderer'ы из корней-родителей.
            List<MeshRenderer> validRenderers = new List<MeshRenderer>();
            foreach (GameObject combineParent in CombineParents)
            {
                validRenderers.AddRange(combineParent.GetComponentsInChildren<MeshRenderer>());
            }

            if (UseGrid)
            {
                // Для каждой ячейки сетки — объединить меши внутри неё отдельно.
                for (int i = 0; i < GetGridCellCount(); i++)
                {
                    if (GetGridCellBounds(i, out Bounds bounds))
                    {
                        CombineAllInBounds(bounds, validRenderers);
                    }
                }
            }
            else
            {
                // Простой режим — все меши в один большой набор.
                // DestroyRendererAndFilter — удаляем у оригиналов рендерер и фильтр,
                // но сами объекты (с коллайдерами и пр.) сохраняем.
                MeshCombineUtility.Combine(validRenderers,
                    MeshCombineUtility.RendererDisposeMethod.DestroyRendererAndFilter, "Level_Combined");
            }
        }

        // Достаём меши, попадающие в bounds, и отправляем на склейку.
        void CombineAllInBounds(Bounds bounds, List<MeshRenderer> validRenderers)
        {
            List<MeshRenderer> renderersForThisCell = new List<MeshRenderer>();

            // Идём с конца, чтобы безопасно удалять элементы по индексу.
            for (int i = validRenderers.Count - 1; i >= 0; i--)
            {
                MeshRenderer m = validRenderers[i];
                // bounds.Intersects — пересекаются ли два AABB.
                if (bounds.Intersects(m.bounds))
                {
                    renderersForThisCell.Add(m);
                    // Убираем из общего списка — каждый меш попадёт ровно в одну ячейку.
                    validRenderers.Remove(m);
                }
            }

            if (renderersForThisCell.Count > 0)
            {
                MeshCombineUtility.Combine(renderersForThisCell,
                    MeshCombineUtility.RendererDisposeMethod.DestroyRendererAndFilter, "Level_Combined");
            }
        }

        // Общее число ячеек = произведение разрешения по осям.
        int GetGridCellCount()
        {
            return GridResolution.x * GridResolution.y * GridResolution.z;
        }

        // По линейному индексу ячейки достаём её координаты (x, y, z) и считаем
        // мировой Bounds. Index → (x, y, z): «развёртка» 3D-массива в 1D.
        public bool GetGridCellBounds(int index, out Bounds bounds)
        {
            bounds = default;
            if (index < 0 || index >= GetGridCellCount())
                return false;

            // Распаковка индекса. Формулы стандартные для линейной 3D-индексации.
            int xCoord = index / (GridResolution.y * GridResolution.z);
            int yCoord = (index / GridResolution.z) % GridResolution.y;
            int zCoord = index % GridResolution.z;

            // Угол сетки, от которого считаем ячейки. Центр - половина размеров.
            Vector3 gridBottomCorner = GridCenter - (GridExtents * 0.5f);
            // Размер одной ячейки.
            Vector3 cellSize = new Vector3(GridExtents.x / (float) GridResolution.x,
                GridExtents.y / (float) GridResolution.y, GridExtents.z / (float) GridResolution.z);
            // Центр конкретной ячейки = угол + (индекс * размер) + (половина размера).
            Vector3 cellCenter = gridBottomCorner + (new Vector3((xCoord * cellSize.x) + (cellSize.x * 0.5f),
                (yCoord * cellSize.y) + (cellSize.y * 0.5f),
                (zCoord * cellSize.z) + (cellSize.z * 0.5f)));

            bounds.center = cellCenter;
            bounds.size = cellSize;

            return true;
        }

        // OnDrawGizmosSelected — Unity вызывает только когда объект выделен в редакторе.
        // Рисуем сетку, чтобы при настройке было видно границы ячеек.
        void OnDrawGizmosSelected()
        {
            if (UseGrid)
            {
                Gizmos.color = GridPreviewColor;

                for (int i = 0; i < GetGridCellCount(); i++)
                {
                    if (GetGridCellBounds(i, out Bounds bounds))
                    {
                        // Wireframe-куб (только рёбра).
                        Gizmos.DrawWireCube(bounds.center, bounds.size);
                    }
                }
            }
        }
    }
}
