using UnityEngine;

namespace Unity.FPS.Game
{
    public class PooledParticleAutoRelease : MonoBehaviour
    {
        ParticleSystem m_ParticleSystem;
        PooledObject m_PooledObject;
        bool m_IsPlaying;

        void Awake()
        {
            m_ParticleSystem = GetComponent<ParticleSystem>();
            m_PooledObject = GetComponent<PooledObject>();
        }

        void OnEnable()
        {
            m_IsPlaying = false;
            if (m_ParticleSystem != null)
            {
                m_ParticleSystem.Play(true);
                m_IsPlaying = true;
            }
        }

        void Update()
        {
            if (m_IsPlaying && !m_ParticleSystem.IsAlive(true))
            {
                m_IsPlaying = false;
                GameObjectPoolManager.Instance.Release(gameObject);
            }
        }
    }
}
