using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Unity.FPS.UI
{
    // ============================================================================
    // ToggleGameObjectButton — простая кнопка «включи/выключи этот GameObject».
    // Бонус: пока цель активна, по нажатию Cancel (Esc) автоматически закрывает её.
    //
    // Используется для подменю в паузе: открыл настройки → нажал Esc → закрыл.
    // ============================================================================
    public class ToggleGameObjectButton : MonoBehaviour
    {
        public GameObject ObjectToToggle;
        public bool ResetSelectionAfterClick;

        private InputAction m_CancelAction;

        void Start()
        {
            m_CancelAction = InputSystem.actions.FindAction("UI/Cancel");
            m_CancelAction.Enable();
        }

        void Update()
        {
            // Cancel закрывает подменю, только если оно сейчас активно.
            if (ObjectToToggle.activeSelf && m_CancelAction.WasPressedThisFrame())
            {
                SetGameObjectActive(false);
            }
        }

        public void SetGameObjectActive(bool active)
        {
            ObjectToToggle.SetActive(active);

            // Сброс выделения нужен, чтобы Submit не «застрял» на той же кнопке после закрытия.
            if (ResetSelectionAfterClick)
                EventSystem.current.SetSelectedGameObject(null);
        }
    }
}
