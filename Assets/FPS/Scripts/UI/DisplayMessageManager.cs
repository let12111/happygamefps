using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.UI
{
    // ============================================================================
    // DisplayMessageManager — менеджер «системных» сообщений на экране (заголовки
    // объективов, «Вы победили», и т.п.). Слушает DisplayMessageEvent.
    //
    // Сообщение может прийти с задержкой (DelayBeforeDisplay) — мы создаём toast
    // сразу, но Initialize вызываем когда задержка истечёт. Так позиция в UITable
    // зарезервирована, и порядок сохраняется.
    //
    // Кортеж (timestamp, delay, message, notification) хранит отложенные сообщения.
    // ============================================================================
    public class DisplayMessageManager : MonoBehaviour
    {
        public UITable DisplayMessageRect;
        public NotificationToast MessagePrefab;

        // Tuple-список: каждое сообщение помнит когда добавлено, сколько ждать,
        // что показать и ссылку на уже созданный toast.
        List<(float timestamp, float delay, string message, NotificationToast notification)> m_PendingMessages;

        void Awake()
        {
            EventManager.AddListener<DisplayMessageEvent>(OnDisplayMessageEvent);
            m_PendingMessages = new List<(float, float, string, NotificationToast)>();
        }

        void OnDisplayMessageEvent(DisplayMessageEvent evt)
        {
            // Создаём toast сразу — он будет в иерархии, но без текста до Initialize.
            NotificationToast notification = Instantiate(MessagePrefab, DisplayMessageRect.transform).GetComponent<NotificationToast>();
            m_PendingMessages.Add((Time.time, evt.DelayBeforeDisplay, evt.Message, notification));
        }

        void Update()
        {
            // Идём с конца — безопасное удаление по индексу.
            for (int i = m_PendingMessages.Count - 1; i >= 0; i--)
            {
                var message = m_PendingMessages[i];
                // Задержка истекла — инициализируем toast и зашиваем его в таблицу.
                if (Time.time - message.timestamp > message.delay)
                {
                    message.Item4.Initialize(message.message);
                    DisplayMessage(message.notification);
                }
                // Initialized = true → можно убрать из ожиданий.
                if (message.notification.Initialized)
                    m_PendingMessages.RemoveAt(i);
            }
        }

        void DisplayMessage(NotificationToast notification)
        {
            DisplayMessageRect.UpdateTable(notification.gameObject);
        }

        void OnDestroy()
        {
            EventManager.RemoveListener<DisplayMessageEvent>(OnDisplayMessageEvent);
        }
    }
}
