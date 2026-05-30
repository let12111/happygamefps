using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.Gameplay
{
    // ============================================================================
    // WeaponPickup — пикап оружия. Добавляет оружие в инвентарь игрока.
    // Если у игрока ещё нет активного — автоматически переключает на него.
    // ============================================================================
    public class WeaponPickup : Pickup
    {
        [Tooltip("The prefab for the weapon that will be added to the player on pickup")]
        public WeaponController WeaponPrefab;

        // Переопределяем Start чтобы поменять слой детей.
        protected override void Start()
        {
            base.Start();

            // Set all children layers to default (to prefent seeing weapons through meshes)
            // На префабе оружия слой обычно FpsWeaponLayer (рисуется в отдельной камере),
            // что нужно когда оно в руках. В пикапе нужен обычный слой 0 (Default).
            foreach (Transform t in GetComponentsInChildren<Transform>())
            {
                if (t != transform)
                    t.gameObject.layer = 0;
            }
        }

        protected override void OnPicked(PlayerCharacterController byPlayer)
        {
            PlayerWeaponsManager playerWeaponsManager = byPlayer.GetComponent<PlayerWeaponsManager>();
            if (playerWeaponsManager)
            {
                // AddWeapon вернёт false если такое оружие уже есть.
                if (playerWeaponsManager.AddWeapon(WeaponPrefab))
                {
                    // Handle auto-switching to weapon if no weapons currently
                    if (playerWeaponsManager.GetActiveWeapon() == null)
                    {
                        playerWeaponsManager.SwitchWeapon(true);
                    }

                    PlayPickupFeedback();
                    Destroy(gameObject);
                }
            }
        }
    }
}
