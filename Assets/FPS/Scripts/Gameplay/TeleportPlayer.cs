using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace Unity.FPS.Gameplay
{
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
            if (Keyboard.current.f12Key.wasPressedThisFrame)
            {
                m_PlayerCharacterController.transform.SetPositionAndRotation(transform.position, transform.rotation);
                Health playerHealth = m_PlayerCharacterController.GetComponent<Health>();
                if (playerHealth)
                {
                    playerHealth.Heal(999);
                }
            }
        }
    }
}
