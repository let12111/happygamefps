using UnityEngine;

namespace Unity.FPS.Game
{
    public class Actor : MonoBehaviour
    {
        [Tooltip("Represents the affiliation (or team) of the actor. Actors of the same affiliation are friendly to each other")]
        public int Affiliation;

        [Tooltip("Represents point where other actors will aim when they attack this actor")]
        public Transform AimPoint;

        IActorsManager m_ActorsManager;

        void Start()
        {
            var actorsManager = FindAnyObjectByType<ActorsManager>();
            DebugUtility.HandleErrorIfNullFindObject<ActorsManager, Actor>(actorsManager, this);
            m_ActorsManager = actorsManager;

            if (!m_ActorsManager.Actors.Contains(this))
                m_ActorsManager.Actors.Add(this);
        }

        void OnDestroy()
        {
            if (m_ActorsManager != null)
                m_ActorsManager.Actors.Remove(this);
        }
    }
}
