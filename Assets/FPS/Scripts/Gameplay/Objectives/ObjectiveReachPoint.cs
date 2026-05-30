using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.Gameplay
{
    // ============================================================================
    // ObjectiveReachPoint — цель «дойди до точки». Триггер: коллайдер с isTrigger=true.
    // Когда игрок входит — цель закрывается.
    //
    // DestroyRoot — корень, который удалится по завершении. Это сделано, чтобы
    // компас (CompassMarker, прикреплённый ВЫШЕ в иерархии) тоже удалился вместе
    // с триггером, иначе остался бы «сирота».
    // ============================================================================
    [RequireComponent(typeof(Collider))]
    public class ObjectiveReachPoint : Objective
    {
        [Tooltip("Visible transform that will be destroyed once the objective is completed")]
        public Transform DestroyRoot;

        void Awake()
        {
            // Если не задано — удаляем себя.
            if (DestroyRoot == null)
                DestroyRoot = transform;
        }

        void OnTriggerEnter(Collider other)
        {
            if (IsCompleted)
                return;

            var player = other.GetComponent<PlayerCharacterController>();
            // test if the other collider contains a PlayerCharacterController, then complete
            if (player != null)
            {
                CompleteObjective(string.Empty, string.Empty, "Objective complete : " + Title);

                // destroy the transform, will remove the compass marker if it has one
                Destroy(DestroyRoot.gameObject);
            }
        }
    }
}
