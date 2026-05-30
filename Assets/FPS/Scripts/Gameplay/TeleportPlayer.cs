using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace Unity.FPS.Gameplay
{
    // ============================================================================
    // TeleportPlayer — дебажный читкод. По F12 телепортирует игрока в позицию
    // этого Transform'а и полностью лечит. Удобно при разработке: ставишь на сцене
    // несколько точек, и можешь быстро прыгать по карте без полного прохождения.
    //
    // В продакшен-билде это, очевидно, должно отключаться — иначе игроки тоже
    // смогут пользоваться.
    //
    // VContainer [Inject]: PlayerCharacterController приходит через DI, без поиска.
    // ============================================================================
    public class TeleportPlayer : MonoBehaviour
    {
        PlayerCharacterController m_PlayerCharacterController;

        [Inject]
        public void Construct(PlayerCharacterController playerCharacterController)
        {
            m_PlayerCharacterController = playerCharacterController;
        }

        void Update()
        {
            // wasPressedThisFrame — срабатывает ровно один кадр.
            if (Keyboard.current.f12Key.wasPressedThisFrame)
            {
                // SetPositionAndRotation за один вызов — чуть быстрее двух отдельных присваиваний.
                m_PlayerCharacterController.transform.SetPositionAndRotation(transform.position, transform.rotation);
                Health playerHealth = m_PlayerCharacterController.GetComponent<Health>();
                if (playerHealth)
                {
                    // Лечим с запасом — Health сам зажмёт по MaxHealth.
                    playerHealth.Heal(999);
                }
            }
        }
    }
}
