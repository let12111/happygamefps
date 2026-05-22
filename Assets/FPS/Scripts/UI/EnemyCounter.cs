using Unity.FPS.AI;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.FPS.UI
{
    public class EnemyCounter : MonoBehaviour
    {
        [Header("Enemies")] [Tooltip("Text component for displaying enemy objective progress")]
        public Text EnemiesText;

        EnemyManager m_EnemyManager;
        int m_LastRemaining = -1;
        int m_LastTotal = -1;

        void Awake()
        {
            m_EnemyManager = FindAnyObjectByType<EnemyManager>();
            DebugUtility.HandleErrorIfNullFindObject<EnemyManager, EnemyCounter>(m_EnemyManager, this);
        }

        void Update()
        {
            int remaining = m_EnemyManager.NumberOfEnemiesRemaining;
            int total = m_EnemyManager.NumberOfEnemiesTotal;
            if (remaining != m_LastRemaining || total != m_LastTotal)
            {
                m_LastRemaining = remaining;
                m_LastTotal = total;
                EnemiesText.text = $"{remaining}/{total}";
            }
        }
    }
}