using System;
using System.Collections.Generic;

namespace Unity.FPS.Game
{
    // GameEvent — пустой базовый класс. Все события наследуются от него.
    // Зачем такой «пустой» базис: даёт общий тип-ограничитель для generic-методов
    // (where T : GameEvent) — компилятор гарантирует, что в EventManager не подсунут
    // случайно строку или int.
    public class GameEvent
    {
    }

    // A simple Event System that can be used for remote systems communication
    // ============================================================================
    // EventManager — статическая шина событий. Pub/sub в одном файле.
    //
    // Зачем нужен: чтобы системы не знали друг о друге напрямую. Враг при смерти
    // не вызывает UI.UpdateCounter() — он Broadcast(EnemyKillEvent), а UI/цели
    // сами решают слушать или нет. Это сильно снижает связанность.
    //
    // Главные методы:
    //  - AddListener<T>(Action<T>): подписаться на событие типа T.
    //  - RemoveListener<T>(Action<T>): отписаться (обязательно в OnDestroy!).
    //  - Broadcast(GameEvent): разослать всем подписчикам.
    //
    // Особая хитрость: пользователь даёт нам Action<T> (типизированный делегат),
    // а внутри мы храним Action<GameEvent>. Чтобы это связать, на подписке
    // создаётся «лямбда-обёртка» (e) => evt((T)e), и она запоминается в
    // s_EventLookups, чтобы при отписке найти именно её.
    // ============================================================================
    public static class EventManager
    {
        // type → объединённый делегат подписчиков этого типа.
        static readonly Dictionary<Type, Action<GameEvent>> s_Events = new Dictionary<Type, Action<GameEvent>>();

        // оригинальный делегат подписчика → его лямбда-обёртка.
        // Без этой таблицы невозможно отписать конкретного слушателя:
        // на каждом += создаётся НОВАЯ лямбда, и без сохранённой ссылки
        // мы бы не знали что именно вычитать из s_Events.
        static readonly Dictionary<Delegate, Action<GameEvent>> s_EventLookups =
            new Dictionary<Delegate, Action<GameEvent>>();

        // where T : GameEvent — ограничение: T должен быть наследником GameEvent.
        public static void AddListener<T>(Action<T> evt) where T : GameEvent
        {
            // Если этот слушатель уже подписан — игнорируем (защита от дублей).
            if (!s_EventLookups.ContainsKey(evt))
            {
                // Лямбда: преобразует GameEvent → T и вызывает реальный обработчик.
                Action<GameEvent> newAction = (e) => evt((T) e);
                s_EventLookups[evt] = newAction;

                // Добавляем в общий список или создаём запись.
                if (s_Events.TryGetValue(typeof(T), out Action<GameEvent> internalAction))
                    s_Events[typeof(T)] = internalAction += newAction;
                else
                    s_Events[typeof(T)] = newAction;
            }
        }

        public static void RemoveListener<T>(Action<T> evt) where T : GameEvent
        {
            // Находим лямбду-обёртку по оригинальному делегату.
            if (s_EventLookups.TryGetValue(evt, out var action))
            {
                if (s_Events.TryGetValue(typeof(T), out var tempAction))
                {
                    // Снимаем эту конкретную обёртку из делегата-цепочки.
                    tempAction -= action;
                    // Если после снятия делегат пуст — удаляем запись типа целиком.
                    if (tempAction == null)
                        s_Events.Remove(typeof(T));
                    else
                        s_Events[typeof(T)] = tempAction;
                }

                s_EventLookups.Remove(evt);
            }
        }

        // Разослать событие всем подписчикам этого типа.
        public static void Broadcast(GameEvent evt)
        {
            // GetType() — РУНТАЙМНЫЙ тип объекта. Поэтому даже если параметр
            // объявлен как GameEvent, мы найдём подписчиков на ConcreteEvent.
            if (s_Events.TryGetValue(evt.GetType(), out var action))
                action.Invoke(evt);
        }

        // Полный сброс — нужен при загрузке новой сцены, чтобы не остались
        // «мёртвые» подписки на уничтоженные объекты.
        public static void Clear()
        {
            s_Events.Clear();
            s_EventLookups.Clear();
        }
    }
}
