using UnityEngine;

namespace Unity.FPS.Game
{
    // ============================================================================
    // Actor — базовый класс для всех «живых» сущностей в игре (игрок, враги).
    // Зачем: даёт единый способ их различать (свой/чужой) и единую «точку прицела»
    // (AimPoint), куда AI наводит огонь. Все Actor'ы автоматически регистрируются
    // в ActorsManager — так системам не нужно искать их через FindObject в каждом кадре.
    // ============================================================================
    public class Actor : MonoBehaviour
    {
        // Целое число — «команда»/«фракция». Actor'ы с одинаковым Affiliation
        // считаются союзниками (например, игрок = 0, враги = 1). Используется
        // в DetectionModule, чтобы враг не стрелял по другим врагам.
        [Tooltip("Represents the affiliation (or team) of the actor. Actors of the same affiliation are friendly to each other")]
        public int Affiliation;

        // Точка, по которой целятся другие Actor'ы при атаке.
        // Зачем отдельно: меш персонажа большой, но прицел нужен в один Transform
        // (обычно «голова» или центр массы), чтобы AI не «дёргал» прицел.
        [Tooltip("Represents point where other actors will aim when they attack this actor")]
        public Transform AimPoint;

        // Ссылка на менеджер. Кешируется в Start, чтобы в OnDestroy не искать заново.
        IActorsManager m_ActorsManager;

        // Start вызывается Unity один раз перед первым кадром, когда все объекты
        // уже созданы. Удобно для поиска синглтонов в сцене.
        void Start()
        {
            // Ищем единственный ActorsManager в сцене. FindAnyObjectByType быстрее,
            // чем FindObjectOfType, потому что не сортирует найденные объекты.
            var actorsManager = FindAnyObjectByType<ActorsManager>();
            // Если менеджера в сцене нет — выведем понятную ошибку в редактор
            // (внутри стоит #if UNITY_EDITOR, в билд ошибка не попадёт).
            DebugUtility.HandleErrorIfNullFindObject<ActorsManager, Actor>(actorsManager, this);
            m_ActorsManager = actorsManager;

            // Регистрируем себя в общем списке. Проверка Contains — защита от
            // двойной регистрации (например, если Start вызовут вручную).
            if (!m_ActorsManager.Actors.Contains(this))
                m_ActorsManager.Actors.Add(this);
        }

        // OnDestroy — Unity вызывает при уничтожении объекта или выгрузке сцены.
        // Обязательно убираем себя из списка, иначе там останется null-ссылка,
        // и менеджер в следующий раз упадёт с NullReferenceException.
        void OnDestroy()
        {
            // Менеджер мог уже быть уничтожен при выходе из игры — проверяем на null.
            if (m_ActorsManager != null)
                m_ActorsManager.Actors.Remove(this);
        }
    }
}
