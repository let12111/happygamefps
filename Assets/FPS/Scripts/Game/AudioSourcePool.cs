using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.FPS.Game
{
    // ============================================================================
    // AudioSourcePool — пул AudioSource-компонентов.
    //
    // Зачем нужен:
    //  - на каждый звук в игре нужен AudioSource;
    //  - PlayOneShot ограничен (нельзя задать spatial blend, distance, и т.п.);
    //  - создавать-уничтожать GameObject на каждый звук — расход GC и просадки.
    //
    // Решение: держим очередь свободных AudioSource. Get() выдаёт один,
    // ReturnAfterDelay через корутину возвращает после окончания звука.
    //
    // Это lazy-singleton: при первом обращении к Instance создаст сам себя в сцене.
    // ============================================================================
    public class AudioSourcePool : MonoBehaviour
    {
        // Реальная ссылка на единственный экземпляр.
        static AudioSourcePool s_Instance;

        // Свойство-фасад. Если экземпляра ещё нет — создаём GameObject
        // и навешиваем на него этот компонент.
        public static AudioSourcePool Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    var go = new GameObject("AudioSourcePool");
                    s_Instance = go.AddComponent<AudioSourcePool>();
                }
                return s_Instance;
            }
        }

        // Мягкий лимит. Если превысили — выводим предупреждение, но всё равно создаём.
        // Это сигнал «слишком много одновременных звуков, надо профилировать».
        const int MaxPoolSize = 32;
        // Счётчик всех когда-либо созданных источников, для диагностики.
        int m_TotalCreated;
        // Очередь свободных источников. Queue (FIFO) лучше чем Stack тут только тем,
        // что давно вернувшийся в пул источник «отдохнёт» (доиграются эффекты эха).
        readonly Queue<AudioSource> m_Pool = new Queue<AudioSource>();

        // Awake — защита от дубликата. Если кто-то вручную положил пул в сцену,
        // и его «нашли» раньше чем lazy-getter сработал — оставляем первого.
        void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            s_Instance = this;
        }

        // Чистим статическую ссылку при уничтожении, иначе lazy-getter
        // в следующий раз вернёт «протухшую» ссылку на удалённый объект.
        void OnDestroy()
        {
            if (s_Instance == this)
                s_Instance = null;
        }

        // Достаём из пула свободный AudioSource и ставим в нужную позицию.
        public AudioSource Get(Vector3 position)
        {
            AudioSource source;
            // Перебираем очередь, пропуская «протухшие» (уничтоженные сценой) источники.
            while (m_Pool.Count > 0)
            {
                source = m_Pool.Dequeue();
                if (source != null)
                {
                    source.transform.position = position;
                    source.gameObject.SetActive(true);
                    return source;
                }
            }

            // Свободных нет — создаём новый.
            m_TotalCreated++;
            if (m_TotalCreated > MaxPoolSize)
                Debug.LogWarning($"[AudioSourcePool] Pool exceeded {MaxPoolSize} sources ({m_TotalCreated} active). Too many simultaneous SFX.");

            // Создаём дочерний GameObject — так все источники сгруппированы в иерархии.
            var go = new GameObject("PooledAudioSource");
            go.transform.SetParent(transform);
            source = go.AddComponent<AudioSource>();
            return source;
        }

        // Удобный метод: верни источник в пул через delay секунд.
        // Используется AudioUtility.CreateSFX — передаёт длину клипа.
        public void ReturnAfterDelay(AudioSource source, float delay)
        {
            StartCoroutine(ReturnCoroutine(source, delay));
        }

        // Корутина — это метод, возвращающий IEnumerator. Unity управляет ею
        // покадрово, yield-точки приостанавливают её до условия. WaitForSeconds —
        // подождать N секунд игрового времени.
        IEnumerator ReturnCoroutine(AudioSource source, float delay)
        {
            yield return new WaitForSeconds(delay);
            // Объект мог быть уничтожен раньше (например, выгружена сцена).
            if (source == null) yield break;
            source.Stop();
            // Снимаем ссылку на клип — иначе он останется заблокирован в памяти.
            source.clip = null;
            source.gameObject.SetActive(false);
            // Возвращаем в очередь свободных.
            m_Pool.Enqueue(source);
        }
    }
}
