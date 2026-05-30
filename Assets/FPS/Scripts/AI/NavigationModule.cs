using UnityEngine;

namespace Unity.FPS.AI
{
    // Component used to override values on start from the NavmeshAgent component in order to change
    // how the agent  is moving
    // ============================================================================
    // NavigationModule — простой контейнер параметров движения врага.
    //
    // Зачем отдельный компонент: настройки NavMeshAgent (скорость, ускорение)
    // часто переопределяют для разных типов врагов. Вместо того, чтобы дублировать
    // префаб NavMeshAgent — храним свои значения в этом модуле, и EnemyController.Start
    // присваивает их в агент.
    //
    // Сам модуль не содержит логики — только данные. Это «data tag» в стиле ECS.
    // ============================================================================
    public class NavigationModule : MonoBehaviour
    {
        [Header("Parameters")] [Tooltip("The maximum speed at which the enemy is moving (in world units per second).")]
        public float MoveSpeed = 0f;

        [Tooltip("The maximum speed at which the enemy is rotating (degrees per second).")]
        public float AngularSpeed = 0f;

        [Tooltip("The acceleration to reach the maximum speed (in world units per second squared).")]
        public float Acceleration = 0f;
    }
}
