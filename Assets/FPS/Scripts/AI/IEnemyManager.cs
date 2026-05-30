using System.Collections.Generic;

namespace Unity.FPS.AI
{
    // ============================================================================
    // IEnemyManager — интерфейс реестра врагов. Зачем интерфейс — см. IActorsManager.
    // Кратко: DI + моки + независимость потребителей от конкретной реализации.
    // ============================================================================
    public interface IEnemyManager
    {
        List<EnemyController> Enemies { get; }
        int NumberOfEnemiesTotal { get; }
        int NumberOfEnemiesRemaining { get; }
        void RegisterEnemy(EnemyController enemy);
        void UnregisterEnemy(EnemyController enemyKilled);
    }
}
