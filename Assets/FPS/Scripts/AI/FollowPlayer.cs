using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.AI
{
    // ============================================================================
    // FollowPlayer — объект следует за игроком с фиксированным оффсетом.
    // Используется обычно для «компаньонов»/HUD-якорей или летающих штук.
    //
    // Простейшая реализация: запоминаем разность позиций в Start, в LateUpdate
    // (после движения игрока) ставим себя = позиция игрока + сохранённый offset.
    // ============================================================================
    public class FollowPlayer : MonoBehaviour
    {
        Transform m_PlayerTransform;
        Vector3 m_OriginalOffset;

        void Start()
        {
            ActorsManager actorsManager = FindAnyObjectByType<ActorsManager>();
            if (actorsManager != null && actorsManager.Player != null)
                m_PlayerTransform = actorsManager.Player.transform;
            else
            {
                // Игрока нет (например, сцена ещё не настроена) — выключаемся.
                enabled = false;
                return;
            }

            m_OriginalOffset = transform.position - m_PlayerTransform.position;
        }

        // LateUpdate — после движения игрока. Если бы делали в Update —
        // могли бы опередить игрока, и оффсет был бы устарелым.
        void LateUpdate()
        {
            if (m_PlayerTransform == null) return;
            transform.position = m_PlayerTransform.position + m_OriginalOffset;
        }
    }
}
