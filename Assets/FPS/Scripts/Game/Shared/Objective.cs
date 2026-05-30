using System;
using UnityEngine;

namespace Unity.FPS.Game
{
    // ============================================================================
    // Objective — БАЗОВЫЙ абстрактный класс всех игровых задач уровня.
    // Конкретные реализации:
    //   - ObjectiveKillEnemies («убей N врагов»)
    //   - ObjectivePickupItem («подбери предмет X»)
    //   - ObjectiveReachPoint («дойди до точки»)
    //
    // Зачем abstract: класс задаёт общий интерфейс и поведение (заголовок,
    // описание, события создания/завершения), но саму логику «когда выполнена»
    // определяют наследники.
    //
    // События OnObjectiveCreated/Completed — статические. Это значит, что
    // ObjectiveManager подписывается ОДИН раз, а получает уведомления о всех
    // целях в сцене сразу.
    // ============================================================================
    public abstract class Objective : MonoBehaviour
    {
        [Tooltip("Name of the objective that will be shown on screen")]
        public string Title;

        [Tooltip("Short text explaining the objective that will be shown on screen")]
        public string Description;

        // Необязательная цель не блокирует победу.
        [Tooltip("Whether the objective is required to win or not")]
        public bool IsOptional;

        // Задержка перед показом цели — нужна, чтобы скриптить «сначала пройди
        // первую цель, потом через 2 секунды появится вторая».
        [Tooltip("Delay before the objective becomes visible")]
        public float DelayVisible;

        // Флаг «уже сделана». private set — снаружи только читать,
        // менять через CompleteObjective.
        public bool IsCompleted { get; private set; }
        // Блокирует ли цель победу. Опциональные и завершённые — не блокируют.
        public bool IsBlocking() => !(IsOptional || IsCompleted);

        // Статические события — на ВСЕ Objective сразу. ObjectiveManager
        // подписывается один раз и узнаёт о любой созданной/завершённой цели.
        public static event Action<Objective> OnObjectiveCreated;
        public static event Action<Objective> OnObjectiveCompleted;

        // virtual — наследник может переопределить и вызвать base.Start().
        // protected — Unity сможет вызвать Start даже в потомках, но снаружи он скрыт.
        protected virtual void Start()
        {
            // Регистрируем себя в системе целей.
            OnObjectiveCreated?.Invoke(this);

            // Просим UI показать заголовок цели игроку.
            // Берём заранее созданный экземпляр из Events — не плодим мусор.
            DisplayMessageEvent displayMessage = Events.DisplayMessageEvent;
            displayMessage.Message = Title;
            displayMessage.DelayBeforeDisplay = 0.0f;
            EventManager.Broadcast(displayMessage);
        }

        // Обновление UI цели (без завершения). Например: счётчик
        // «убито 2/5 врагов» обновился.
        public void UpdateObjective(string descriptionText, string counterText, string notificationText)
        {
            ObjectiveUpdateEvent evt = Events.ObjectiveUpdateEvent;
            evt.Objective = this;
            evt.DescriptionText = descriptionText;
            evt.CounterText = counterText;
            evt.NotificationText = notificationText;
            evt.IsComplete = IsCompleted;
            EventManager.Broadcast(evt);
        }

        // Финал — цель выполнена. Ставим флаг, шлём событие в UI
        // и в OnObjectiveCompleted (ObjectiveManager проверит — все ли уже сделаны).
        public void CompleteObjective(string descriptionText, string counterText, string notificationText)
        {
            IsCompleted = true;

            ObjectiveUpdateEvent evt = Events.ObjectiveUpdateEvent;
            evt.Objective = this;
            evt.DescriptionText = descriptionText;
            evt.CounterText = counterText;
            evt.NotificationText = notificationText;
            evt.IsComplete = IsCompleted;
            EventManager.Broadcast(evt);

            OnObjectiveCompleted?.Invoke(this);
        }
    }
}
