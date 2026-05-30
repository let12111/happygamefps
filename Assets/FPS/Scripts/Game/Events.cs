using UnityEngine;

namespace Unity.FPS.Game
{
    // The Game Events used across the Game.
    // Anytime there is a need for a new event, it should be added here.

    // ============================================================================
    // Events — статический каталог всех игровых событий.
    // Зачем именно так: системы (игрок, враги, UI, менеджер игры) не должны знать
    // друг о друге напрямую. Вместо этого они шлют объект-событие через EventManager,
    // и каждый подписчик сам решает, что с ним делать. Это паттерн pub/sub
    // (publish/subscribe), он сильно уменьшает связанность кода.
    //
    // Здесь хранятся ОДИН экземпляр каждого типа события — их переиспользуют,
    // меняя поля перед Broadcast. Это экономит мусор для GC (каждый кадр не
    // создаётся новый объект события).
    // ============================================================================
    public static class Events
    {
        // Прогресс цели изменился (счётчик +1, описание поменялось и т.п.).
        public static ObjectiveUpdateEvent ObjectiveUpdateEvent = new ObjectiveUpdateEvent();
        // Все цели в сцене выполнены — это сигнал «победа близко».
        public static AllObjectivesCompletedEvent AllObjectivesCompletedEvent = new AllObjectivesCompletedEvent();
        // Игра завершена — поле Win говорит победа/поражение.
        public static GameOverEvent GameOverEvent = new GameOverEvent();
        // Игрок умер (отдельно от GameOver, чтобы UI мог реагировать раньше).
        public static PlayerDeathEvent PlayerDeathEvent = new PlayerDeathEvent();
        // Убит враг — обновляет счётчик и может закрывать цели «убей N врагов».
        public static EnemyKillEvent EnemyKillEvent = new EnemyKillEvent();
        // Игрок подобрал предмет — нужно ObjectivePickupItem и т.п.
        public static PickupEvent PickupEvent = new PickupEvent();
        // Подбор патронов — отдельное событие, потому что у него своя обработка
        // (надо знать какому оружию патроны).
        public static AmmoPickupEvent AmmoPickupEvent = new AmmoPickupEvent();
        // Кому-то нанесли урон — слушает UI (мигание экрана) и враги (агрессия).
        public static DamageEvent DamageEvent = new DamageEvent();
        // Запрос показать сообщение игроку (например, объяснение цели в UI).
        public static DisplayMessageEvent DisplayMessageEvent = new DisplayMessageEvent();
    }

    // GameEvent — базовый класс для всех событий. Он лежит в EventManager.cs.
    // Ниже каждое событие наследуется от него и добавляет свои поля-данные.

    // Событие: «обновлена цель».
    public class ObjectiveUpdateEvent : GameEvent
    {
        public Objective Objective;          // Какая именно цель обновилась.
        public string DescriptionText;       // Текст описания (для HUD).
        public string CounterText;           // Счётчик в виде строки, напр. «2 / 5».
        public bool IsComplete;              // Эта цель только что завершена?
        public string NotificationText;      // Текст всплывашки, если нужно показать.
    }

    // Маркерное событие без полей — все цели выполнены.
    public class AllObjectivesCompletedEvent : GameEvent { }

    // Событие конца игры — содержит флаг победы.
    public class GameOverEvent : GameEvent
    {
        public bool Win;
    }

    // Маркер смерти игрока — без полей, факта события достаточно.
    public class PlayerDeathEvent : GameEvent { }

    // Убийство врага — UI показывает счётчик «осталось N», цель проверяет ноль.
    public class EnemyKillEvent : GameEvent
    {
        public GameObject Enemy;             // Сам убитый враг.
        public int RemainingEnemyCount;      // Сколько ещё врагов в сцене.
    }

    // Подбор любого предмета (общий случай).
    public class PickupEvent : GameEvent
    {
        public GameObject Pickup;
    }

    // Специализация — подбор патронов, чтобы знать, какому оружию они пошли.
    public class AmmoPickupEvent : GameEvent
    {
        public WeaponController Weapon;
    }

    // Урон: кто нанёс и сколько. UI рисует красную рамку, AI запоминает атакующего.
    public class DamageEvent : GameEvent
    {
        public GameObject Sender;
        public float DamageValue;
    }

    // Запрос показать текстовое сообщение через DisplayMessageManager.
    public class DisplayMessageEvent : GameEvent
    {
        public string Message;
        public float DelayBeforeDisplay;     // Задержка перед показом — нужно
                                             // для синхронизации с анимациями/звуком.
    }
}
