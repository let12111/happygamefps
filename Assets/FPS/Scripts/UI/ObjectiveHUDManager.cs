using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.UI
{
    // ============================================================================
    // ObjectiveHUDManager — список целей в HUD. Слушает:
    //  - Objective.OnObjectiveCreated   → создаёт ObjectiveToast;
    //  - Objective.OnObjectiveCompleted → запускает «затухание» toast'а;
    //  - ObjectiveUpdateEvent           → обновляет текст/счётчик существующего toast.
    //
    // Словарь Objective → ObjectiveToast: чтобы по приходящему событию быстро
    // найти соответствующий UI-элемент.
    // ============================================================================
    public class ObjectiveHUDManager : MonoBehaviour
    {
        [Tooltip("UI panel containing the layoutGroup for displaying objectives")]
        public RectTransform ObjectivePanel;

        [Tooltip("Prefab for the primary objectives")]
        public GameObject PrimaryObjectivePrefab;

        [Tooltip("Prefab for the primary objectives")]
        public GameObject SecondaryObjectivePrefab;

        Dictionary<Objective, ObjectiveToast> m_ObjectivesDictionnary;

        void Awake()
        {
            m_ObjectivesDictionnary = new Dictionary<Objective, ObjectiveToast>();

            EventManager.AddListener<ObjectiveUpdateEvent>(OnUpdateObjective);

            // Подписки на static-события Objective.
            Objective.OnObjectiveCreated += RegisterObjective;
            Objective.OnObjectiveCompleted += UnregisterObjective;
        }

        public void RegisterObjective(Objective objective)
        {
            // instanciate the Ui element for the new objective
            // Префаб разный для основных и опциональных целей (разный визуал).
            GameObject objectiveUIInstance =
                Instantiate(objective.IsOptional ? SecondaryObjectivePrefab : PrimaryObjectivePrefab, ObjectivePanel);

            // Основные ставим в начало списка (sibling index 0 = первый ребёнок).
            if (!objective.IsOptional)
                objectiveUIInstance.transform.SetSiblingIndex(0);

            ObjectiveToast toast = objectiveUIInstance.GetComponent<ObjectiveToast>();
            DebugUtility.HandleErrorIfNullGetComponent<ObjectiveToast, ObjectiveHUDManager>(toast, this,
                objectiveUIInstance.gameObject);

            // initialize the element and give it the objective description
            toast.Initialize(objective.Title, objective.Description, "", objective.IsOptional, objective.DelayVisible);

            m_ObjectivesDictionnary.Add(objective, toast);

            // Сразу пересобираем layout — иначе текст и размер «прыгнут» на следующем кадре.
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(ObjectivePanel);
        }

        public void UnregisterObjective(Objective objective)
        {
            // if the objective if in the list, make it fade out, and remove it from the list
            if (m_ObjectivesDictionnary.TryGetValue(objective, out ObjectiveToast toast) && toast != null)
            {
                // Complete запустит fade-out, потом toast сам себя Destroy.
                toast.Complete();
            }

            m_ObjectivesDictionnary.Remove(objective);
        }

        void OnUpdateObjective(ObjectiveUpdateEvent evt)
        {
            if (m_ObjectivesDictionnary.TryGetValue(evt.Objective, out ObjectiveToast toast) && toast != null)
            {
                // Пустые строки не перезаписываем — это «нет изменений» от Objective.
                if (!string.IsNullOrEmpty(evt.DescriptionText))
                    toast.DescriptionTextContent.text = evt.DescriptionText;

                if (!string.IsNullOrEmpty(evt.CounterText))
                    toast.CounterTextContent.text = evt.CounterText;

                // MarkLayoutForRebuild — отложенный пересчёт (в отличие от ForceRebuild).
                // Дешевле, потому что rebuild произойдёт раз на кадр, даже если пометок много.
                RectTransform toastRect = toast.GetComponent<RectTransform>();
                if (toastRect != null)
                    UnityEngine.UI.LayoutRebuilder.MarkLayoutForRebuild(toastRect);
            }
        }

        void OnDestroy()
        {
            EventManager.RemoveListener<ObjectiveUpdateEvent>(OnUpdateObjective);

            Objective.OnObjectiveCreated -= RegisterObjective;
            Objective.OnObjectiveCompleted -= UnregisterObjective;
        }
    }
}
