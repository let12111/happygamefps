using System.Collections.Generic;
using UnityEngine;

namespace Unity.FPS.AI
{
    // ============================================================================
    // PatrolPath — маршрут патрулирования.
    //
    // Это просто список Transform'ов (точек). EnemyController.UpdatePathDestination
    // двигает индекс «следующей точки» по этому списку и при достижении конца
    // замыкается на начало (цикличный путь).
    //
    // EnemiesToAssign — удобно: один PatrolPath сам назначит себя нескольким
    // врагам. Альтернатива — назначать вручную каждому врагу.
    // ============================================================================
    public class PatrolPath : MonoBehaviour
    {
        [Tooltip("Enemies that will be assigned to this path on Start")]
        public List<EnemyController> EnemiesToAssign = new List<EnemyController>();

        [Tooltip("The Nodes making up the path")]
        public List<Transform> PathNodes = new List<Transform>();

        void Start()
        {
            // Привязываемся к каждому врагу из списка.
            foreach (var enemy in EnemiesToAssign)
            {
                enemy.PatrolPath = this;
            }
        }

        // Расстояние от origin до точки index. -1 если индекс невалидный.
        public float GetDistanceToNode(Vector3 origin, int destinationNodeIndex)
        {
            if (destinationNodeIndex < 0 || destinationNodeIndex >= PathNodes.Count ||
                PathNodes[destinationNodeIndex] == null)
            {
                return -1f;
            }

            return (PathNodes[destinationNodeIndex].position - origin).magnitude;
        }

        public Vector3 GetPositionOfPathNode(int nodeIndex)
        {
            if (nodeIndex < 0 || nodeIndex >= PathNodes.Count || PathNodes[nodeIndex] == null)
            {
                return Vector3.zero;
            }

            return PathNodes[nodeIndex].position;
        }

        // Визуализация маршрута в редакторе: соединяем точки линиями и рисуем шарики.
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < PathNodes.Count; i++)
            {
                // Замыкаем: последняя точка → первая.
                int nextIndex = i + 1;
                if (nextIndex >= PathNodes.Count)
                {
                    nextIndex -= PathNodes.Count;
                }

                Gizmos.DrawLine(PathNodes[i].position, PathNodes[nextIndex].position);
                Gizmos.DrawSphere(PathNodes[i].position, 0.1f);
            }
        }
    }
}
