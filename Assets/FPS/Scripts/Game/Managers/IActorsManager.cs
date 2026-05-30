using System.Collections.Generic;
using UnityEngine;

namespace Unity.FPS.Game
{
    // ============================================================================
    // IActorsManager — интерфейс реестра Actor'ов.
    //
    // Зачем интерфейс, если есть один класс ActorsManager:
    //  - Dependency Injection: VContainer/Zenject могут связать любую реализацию.
    //  - Тесты: можно подменить на мок и проверить логику без сцены.
    //  - Снижение связанности: код, который использует Actors, не зависит от конкретного класса.
    //
    // Извлечение интерфейса — обычно первый шаг к нормальной архитектуре.
    // ============================================================================
    public interface IActorsManager
    {
        List<Actor> Actors { get; }
        GameObject Player { get; }
        void SetPlayer(GameObject player);
    }
}
