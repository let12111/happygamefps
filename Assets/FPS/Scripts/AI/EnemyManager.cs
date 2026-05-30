using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.AI
{
    // ============================================================================
    // EnemyManager — реестр всех живых врагов в сцене + источник EnemyKillEvent.
    //
    // Враг при Start вызывает RegisterEnemy, при OnDie — UnregisterEnemy.
    // UnregisterEnemy шлёт EnemyKillEvent, на который подписаны:
    //  - ObjectiveKillEnemies (для счётчика цели);
    //  - UI-счётчик врагов.
    //
    // Через IEnemyManager интерфейс — для DI и тестирования.
    // ============================================================================
    public class EnemyManager : MonoBehaviour, IEnemyManager
    {
        public List<EnemyController> Enemies { get; private set; }
        // Сколько было ВСЕГО — для UI «убито X из Y».
        public int NumberOfEnemiesTotal { get; private set; }
        // Сколько осталось — текущая длина списка.
        public int NumberOfEnemiesRemaining => Enemies.Count;

        void Awake()
        {
            Enemies = new List<EnemyController>();
        }

        public void RegisterEnemy(EnemyController enemy)
        {
            Enemies.Add(enemy);

            NumberOfEnemiesTotal++;
        }

        public void UnregisterEnemy(EnemyController enemyKilled)
        {
            // -1 потому что мы ЕЩЁ не удалили его из списка.
            int enemiesRemainingNotification = NumberOfEnemiesRemaining - 1;

            EnemyKillEvent evt = Events.EnemyKillEvent;
            evt.Enemy = enemyKilled.gameObject;
            evt.RemainingEnemyCount = enemiesRemainingNotification;
            EventManager.Broadcast(evt);

            // removes the enemy from the list, so that we can keep track of how many are left on the map
            Enemies.Remove(enemyKilled);
        }
    }
}
