using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Unity.FPS.Game
{
    // ============================================================================
    // GameObjectPoolManager — централизованный пул GameObject'ов.
    //
    // Зачем нужен: Instantiate/Destroy на каждый снаряд, искру, гильзу — это
    // мусор для GC и просадки FPS. Пул держит экземпляры в памяти и переиспользует.
    //
    // Архитектура: словарь «PrefabId → ObjectPool<GameObject>». Ключ — InstanceID
    // префаба (уникальный в рантайме). Внутренний ObjectPool — это встроенный
    // в Unity класс из UnityEngine.Pool, ему задают коллбэки:
    //   createFunc        — как создавать новый, если в пуле пусто;
    //   actionOnGet       — что делать при выдаче (активируем);
    //   actionOnRelease   — что делать при возврате (привязываем под себя + деактивируем);
    //   actionOnDestroy   — при переполнении пула просто Destroy.
    //
    // Lazy-singleton: при первом Instance создаёт сам себя в сцене.
    // ============================================================================
    public class GameObjectPoolManager : MonoBehaviour
    {
        static GameObjectPoolManager s_Instance;

        public static GameObjectPoolManager Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    // Сначала пробуем найти в сцене (может быть уже положен).
                    s_Instance = FindAnyObjectByType<GameObjectPoolManager>();
                    if (s_Instance == null)
                    {
                        // Нет — создаём.
                        var go = new GameObject("GameObjectPoolManager");
                        s_Instance = go.AddComponent<GameObjectPoolManager>();
                    }
                }
                return s_Instance;
            }
        }

        // Пулы по InstanceID префаба. Шорткат `new()` — это C# 9 target-typed new.
        readonly Dictionary<int, ObjectPool<GameObject>> m_Pools = new();
        // Сохранённые ссылки на сами префабы — нужны для createFunc,
        // потому что ObjectPool сам префаба не помнит.
        readonly Dictionary<int, GameObject> m_Prefabs = new();

        // Защита от дубликата (если кто-то вручную положил пул в сцену + сработал lazy-getter).
        void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            s_Instance = this;
        }

        void OnDestroy()
        {
            if (s_Instance == this)
                s_Instance = null;
        }

        // Выдать экземпляр префаба в позицию + поворот.
        public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            var obj = GetOrCreatePool(prefab).Get();
            // Отвязываем — чтобы менеджер не таскал его как «ребёнка» в иерархии.
            obj.transform.SetParent(null);
            // SetPositionAndRotation за один вызов эффективнее, чем два отдельных.
            obj.transform.SetPositionAndRotation(position, rotation);
            return obj;
        }

        // Вернуть в пул. По PooledObject определяем какой именно пул нужен.
        public void Release(GameObject instance)
        {
            var pooled = instance.GetComponent<PooledObject>();
            if (pooled != null && m_Pools.TryGetValue(pooled.PrefabId, out var pool))
                pool.Release(instance);
            else
                // Объект не из пула (или его пул уже уничтожен) — fallback на Destroy.
                Destroy(instance);
        }

        // Удобный шорткат: вернуть через delay секунд. Используется для muzzle flash
        // без частиц (которые сами знают, когда умереть).
        public void ReleaseDelayed(GameObject instance, float delay)
        {
            StartCoroutine(DelayedRelease(instance, delay));
        }

        IEnumerator DelayedRelease(GameObject instance, float delay)
        {
            yield return new WaitForSeconds(delay);
            // Перепроверяем — объект мог быть уже возвращён в пул или уничтожен.
            if (instance != null && instance.activeSelf)
                Release(instance);
        }

        // Достать пул или создать его.
        ObjectPool<GameObject> GetOrCreatePool(GameObject prefab)
        {
            int id = prefab.GetInstanceID();
            if (!m_Pools.TryGetValue(id, out var pool))
            {
                m_Prefabs[id] = prefab;
                pool = new ObjectPool<GameObject>(
                    createFunc: () => CreatePooledInstance(id),
                    // Активируем при выдаче — на release был SetActive(false).
                    actionOnGet: obj => obj.SetActive(true),
                    actionOnRelease: obj =>
                    {
                        // Привязываем под менеджер — он будет «контейнером» в иерархии.
                        obj.transform.SetParent(transform);
                        obj.SetActive(false);
                    },
                    actionOnDestroy: obj => Destroy(obj),
                    // collectionCheck=false — отключаем проверку «не возвращён ли уже».
                    // Это экономит время; цена — двойной Release приведёт к багам.
                    collectionCheck: false,
                    defaultCapacity: 8,
                    // При превышении пул не растёт, лишние объекты уходят в actionOnDestroy.
                    maxSize: 64
                );
                m_Pools[id] = pool;
            }
            return pool;
        }

        // Фабрика новых экземпляров. Вызывается ObjectPool, когда в пуле пусто.
        GameObject CreatePooledInstance(int prefabId)
        {
            var obj = Instantiate(m_Prefabs[prefabId], transform);
            // Маркируем — на release найдём пул через PrefabId.
            obj.AddComponent<PooledObject>().PrefabId = prefabId;
            // Если есть ParticleSystem — добавим автоматический Release.
            // Так VFX-эффекты можно «выстрелить и забыть».
            if (obj.GetComponentInChildren<ParticleSystem>() != null)
                obj.AddComponent<PooledParticleAutoRelease>();
            return obj;
        }
    }
}
