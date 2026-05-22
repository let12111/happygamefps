using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.UI
{
    public class DisplayMessageManager : MonoBehaviour
    {
        public UITable DisplayMessageRect;
        public NotificationToast MessagePrefab;

        List<(float timestamp, float delay, string message, NotificationToast notification)> m_PendingMessages;

        void Awake()
        {
            EventManager.AddListener<DisplayMessageEvent>(OnDisplayMessageEvent);
            m_PendingMessages = new List<(float, float, string, NotificationToast)>();
        }

        void OnDisplayMessageEvent(DisplayMessageEvent evt)
        {
            NotificationToast notification = Instantiate(MessagePrefab, DisplayMessageRect.transform).GetComponent<NotificationToast>();
            m_PendingMessages.Add((Time.time, evt.DelayBeforeDisplay, evt.Message, notification));
        }

        void Update()
        {
            for (int i = m_PendingMessages.Count - 1; i >= 0; i--)
            {
                var message = m_PendingMessages[i];
                if (Time.time - message.timestamp > message.delay)
                {
                    message.Item4.Initialize(message.message);
                    DisplayMessage(message.notification);
                }
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