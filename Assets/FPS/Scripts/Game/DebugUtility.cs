using UnityEngine;

namespace Unity.FPS.Game
{
    // ============================================================================
    // DebugUtility — утилиты для понятных сообщений в редакторе, когда что-то
    // не настроено. Все методы обёрнуты в #if UNITY_EDITOR — в финальный билд
    // эти проверки НЕ попадают, чтобы не тратить процессор на проверки в релизе.
    //
    // Зачем нужно: Unity «молча» подсунет null если компонент не нашёлся,
    // и игра свалится с NullReferenceException где-то в другом месте. А эти
    // методы сразу скажут «вот тут не настроена ссылка», что экономит часы поиска.
    //
    // Generic-параметры <TO, TS>: TO = тип, который ИСКАЛИ (Object); TS = тип,
    // который ИЩЕТ (Source). В сообщение оба пишутся — удобно понять контекст.
    // ============================================================================
    public static class DebugUtility
    {
        // Случай 1: вызвали GetComponent и получили null.
        // Например, ожидали Rigidbody на префабе, но забыли его повесить.
        public static void HandleErrorIfNullGetComponent<TO, TS>(Component component, Component source,
            GameObject onObject)
        {
#if UNITY_EDITOR
            if (component == null)
            {
                Debug.LogError("Error: Component of type " + typeof(TS) + " on GameObject " + source.gameObject.name +
                               " expected to find a component of type " + typeof(TO) + " on GameObject " +
                               onObject.name + ", but none were found.");
            }
#endif
        }

        // Случай 2: искали через FindObject в сцене и не нашли.
        // Например, ActorsManager должен быть в сцене, но его не положили.
        public static void HandleErrorIfNullFindObject<TO, TS>(Object obj, Component source)
        {
#if UNITY_EDITOR
            if (obj == null)
            {
                Debug.LogError("Error: Component of type " + typeof(TS) + " on GameObject " + source.gameObject.name +
                               " expected to find an object of type " + typeof(TO) +
                               " in the scene, but none were found.");
            }
#endif
        }

        // Случай 3: GetComponentsInChildren вернул 0 — ожидали хотя бы один.
        public static void HandleErrorIfNoComponentFound<TO, TS>(int count, Component source, GameObject onObject)
        {
#if UNITY_EDITOR
            if (count == 0)
            {
                Debug.LogError("Error: Component of type " + typeof(TS) + " on GameObject " + source.gameObject.name +
                               " expected to find at least one component of type " + typeof(TO) + " on GameObject " +
                               onObject.name + ", but none were found.");
            }
#endif
        }

        // Случай 4: ожидали ОДИН компонент, а нашлось несколько — это варнинг
        // (не ошибка), потому что код всё равно возьмёт первый и продолжит.
        public static void HandleWarningIfDuplicateObjects<TO, TS>(int count, Component source, GameObject onObject)
        {
#if UNITY_EDITOR
            if (count > 1)
            {
                Debug.LogWarning("Warning: Component of type " + typeof(TS) + " on GameObject " +
                                 source.gameObject.name +
                                 " expected to find only one component of type " + typeof(TO) + " on GameObject " +
                                 onObject.name + ", but several were found. First one found will be selected.");
            }
#endif
        }
    }
}
