using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.FPS.Game
{
    public class AudioSourcePool : MonoBehaviour
    {
        static AudioSourcePool s_Instance;

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

        const int MaxPoolSize = 32;
        int m_TotalCreated;
        readonly Queue<AudioSource> m_Pool = new Queue<AudioSource>();

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

        public AudioSource Get(Vector3 position)
        {
            AudioSource source;
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

            m_TotalCreated++;
            if (m_TotalCreated > MaxPoolSize)
                Debug.LogWarning($"[AudioSourcePool] Pool exceeded {MaxPoolSize} sources ({m_TotalCreated} active). Too many simultaneous SFX.");

            var go = new GameObject("PooledAudioSource");
            go.transform.SetParent(transform);
            source = go.AddComponent<AudioSource>();
            return source;
        }

        public void ReturnAfterDelay(AudioSource source, float delay)
        {
            StartCoroutine(ReturnCoroutine(source, delay));
        }

        IEnumerator ReturnCoroutine(AudioSource source, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (source == null) yield break;
            source.Stop();
            source.clip = null;
            source.gameObject.SetActive(false);
            m_Pool.Enqueue(source);
        }
    }
}
