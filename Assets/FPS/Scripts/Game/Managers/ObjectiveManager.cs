using System.Collections.Generic;
using UnityEngine;

namespace Unity.FPS.Game
{
    // ============================================================================
    // ObjectiveManager — следит за списком целей уровня. Когда все НЕ-опциональные
    // цели выполнены, шлёт AllObjectivesCompletedEvent (на это подписан
    // GameFlowManager и запускает финал победы).
    //
    // Регистрация целей — через static-событие Objective.OnObjectiveCreated:
    // каждая цель в своём Start() уведомляет систему, что появилась. Удобно
    // потому, что менеджеру не нужно собирать цели вручную через FindObjects.
    // ============================================================================
    public class ObjectiveManager : MonoBehaviour
    {
        // Все цели сцены.
        List<Objective> m_Objectives = new List<Objective>();
        // Защита: AllObjectivesCompletedEvent должен сработать ровно один раз.
        bool m_ObjectivesCompleted = false;

        void Awake()
        {
            // Подписываемся на событие «создана новая цель».
            // Statics-событие, поэтому AddListener тут не EventManager.
            Objective.OnObjectiveCreated += RegisterObjective;
        }

        void RegisterObjective(Objective objective) => m_Objectives.Add(objective);

        void Update()
        {
            // Нечего проверять — выходим. Это раньше, чем GetCount() в for —
            // одна ветвь вместо итерации.
            if (m_Objectives.Count == 0 || m_ObjectivesCompleted)
                return;

            for (int i = 0; i < m_Objectives.Count; i++)
            {
                // pass every objectives to check if they have been completed
                // Если есть хоть одна блокирующая (не опциональная и не выполненная) —
                // не победа.
                if (m_Objectives[i].IsBlocking())
                {
                    // break the loop as soon as we find one uncompleted objective
                    return;
                }
            }

            // Дошли сюда — все блокирующие цели выполнены.
            m_ObjectivesCompleted = true;
            EventManager.Broadcast(Events.AllObjectivesCompletedEvent);
        }

        // Чистим подписку — обязательно для static-событий, иначе подписки накапливаются.
        void OnDestroy()
        {
            Objective.OnObjectiveCreated -= RegisterObjective;
        }
    }
}
