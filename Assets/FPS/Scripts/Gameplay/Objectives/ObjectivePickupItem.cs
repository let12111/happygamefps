using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.Gameplay
{
    // ============================================================================
    // ObjectivePickupItem — цель «подбери конкретный предмет».
    //
    // Слушает PickupEvent. Когда игрок что-то подобрал, проверяем — это наш предмет?
    // Если да — закрываем цель. Сам предмет удаляется (или уже удалён пикапом).
    //
    // Нюанс: цель завершается даже если игрок не СМОГ подобрать предмет
    // (например, попытка подобрать аптечку с полным HP). Это сделано специально:
    // некоторые уровни используют пикап как «триггер дошёл до точки»,
    // а не как реальный полезный предмет.
    // ============================================================================
    public class ObjectivePickupItem : Objective
    {
        [Tooltip("Item to pickup to complete the objective")]
        public GameObject ItemToPickup;

        protected override void Start()
        {
            base.Start();

            EventManager.AddListener<PickupEvent>(OnPickupEvent);
        }

        void OnPickupEvent(PickupEvent evt)
        {
            // Игнорируем чужие подборы и повторы.
            if (IsCompleted || ItemToPickup != evt.Pickup)
                return;

            // this will trigger the objective completion
            // it works even if the player can't pickup the item (i.e. objective pickup healthpack while at full heath)
            CompleteObjective(string.Empty, string.Empty, "Objective complete : " + Title);

            if (gameObject)
            {
                Destroy(gameObject);
            }
        }

        // Чистим подписку, иначе после уничтожения сам объект остался бы в делегате.
        void OnDestroy()
        {
            EventManager.RemoveListener<PickupEvent>(OnPickupEvent);
        }
    }
}
