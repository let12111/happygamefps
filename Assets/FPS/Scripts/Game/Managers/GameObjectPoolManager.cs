using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Unity.FPS.Game
{
    public class GameObjectPoolManager : MonoBehaviour
    {
        static GameObjectPoolManager s_Instance;

        public static GameObjectPoolManager Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    s_Instance = FindAnyObjectByType<GameObjectPoolManager>();
                    if (s_Instance == null)
                    {
                        var go = new GameObject("GameObjectPoolManager");
                        s_Instance = go.AddComponent<GameObjectPoolManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return s_Instance;
            }
        }

        readonly Dictionary<int, ObjectPool<GameObject>> m_Pools = new();
        readonly Dictionary<int, GameObject> m_Prefabs = new();

        void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            s_Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            var pool = GetOrCreatePool(prefab);
            var obj = pool.Get();
            obj.transform.SetPositionAndRotation(position, rotation);
            return obj;
        }

        public void Release(GameObject instance)
        {
            var pooled = instance.GetComponent<PooledObject>();
            if (pooled != null && m_Pools.TryGetValue(pooled.PrefabId, out var pool))
                pool.Release(instance);
            else
                Destroy(instance);
        }

        public void ReleaseDelayed(GameObject instance, float delay)
        {
            StartCoroutine(DelayedRelease(instance, delay));
        }

        IEnumerator DelayedRelease(GameObject instance, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (instance != null && instance.activeSelf)
                Release(instance);
        }

        ObjectPool<GameObject> GetOrCreatePool(GameObject prefab)
        {
            int id = prefab.GetInstanceID();
            if (!m_Pools.TryGetValue(id, out var pool))
            {
                m_Prefabs[id] = prefab;
                pool = new ObjectPool<GameObject>(
                    createFunc: () => CreatePooledInstance(id),
                    actionOnGet: obj => obj.SetActive(true),
                    actionOnRelease: obj =>
                    {
                        obj.transform.SetParent(null);
                        obj.SetActive(false);
                    },
                    actionOnDestroy: obj => Destroy(obj),
                    collectionCheck: false,
                    defaultCapacity: 8,
                    maxSize: 64
                );
                m_Pools[id] = pool;
            }
            return pool;
        }

        GameObject CreatePooledInstance(int prefabId)
        {
            var obj = Instantiate(m_Prefabs[prefabId]);
            obj.AddComponent<PooledObject>().PrefabId = prefabId;
            if (obj.GetComponent<ParticleSystem>() != null)
                obj.AddComponent<PooledParticleAutoRelease>();
            return obj;
        }
    }
}
