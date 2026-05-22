using System.Collections.Generic;

namespace Unity.FPS.AI
{
    public interface IEnemyManager
    {
        List<EnemyController> Enemies { get; }
        int NumberOfEnemiesTotal { get; }
        int NumberOfEnemiesRemaining { get; }
        void RegisterEnemy(EnemyController enemy);
        void UnregisterEnemy(EnemyController enemyKilled);
    }
}
