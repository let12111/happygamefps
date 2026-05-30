using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.Gameplay
{
    // ============================================================================
    // AmmoPickup — пикап патронов для КОНКРЕТНОГО оружия. Подбирается только
    // если у игрока есть это оружие — иначе бесполезен и не исчезает.
    //
    // Наследует Pickup → анимация, триггер и общий фидбек уже есть.
    // ============================================================================
    public class AmmoPickup : Pickup
    {
        // Префаб оружия, для которого патроны. По нему ищем оружие в инвентаре.
        [Tooltip("Weapon those bullets are for")]
        public WeaponController Weapon;

        [Tooltip("Number of bullets the player gets")]
        public int BulletCount = 30;

        protected override void OnPicked(PlayerCharacterController byPlayer)
        {
            PlayerWeaponsManager playerWeaponsManager = byPlayer.GetComponent<PlayerWeaponsManager>();
            if (playerWeaponsManager)
            {
                // HasWeapon вернёт null, если у игрока нет такого оружия — тогда пропускаем подбор.
                WeaponController weapon = playerWeaponsManager.HasWeapon(Weapon);
                if (weapon != null)
                {
                    weapon.AddCarriablePhysicalBullets(BulletCount);

                    // Специализированное событие — UI обновит счётчик именно для этого оружия.
                    AmmoPickupEvent evt = Events.AmmoPickupEvent;
                    evt.Weapon = weapon;
                    EventManager.Broadcast(evt);

                    PlayPickupFeedback();
                    Destroy(gameObject);
                }
            }
        }
    }
}
