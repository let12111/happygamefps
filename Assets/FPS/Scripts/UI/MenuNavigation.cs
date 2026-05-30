using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Unity.FPS.UI
{
    // ============================================================================
    // MenuNavigation — стартовая настройка меню: расфиксировать курсор, сделать
    // его видимым, очистить выделение в EventSystem.
    //
    // В LateUpdate: если ничего не выделено и игрок начал что-то жать
    // (Submit или стрелки), автоматически выделяем DefaultSelection. Это удобно
    // для геймпада — без выделения навигация по меню не работает.
    // ============================================================================
    public class MenuNavigation : MonoBehaviour
    {
        public Selectable DefaultSelection;

        private InputAction m_SubmitAction;
        private InputAction m_NavigateAction;

        void Start()
        {
            // В меню курсор нужен видимым и расфиксированным.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            EventSystem.current.SetSelectedGameObject(null);

            m_SubmitAction = InputSystem.actions.FindAction("UI/Submit");
            m_NavigateAction  = InputSystem.actions.FindAction("UI/Navigate");
        }

        void LateUpdate()
        {
            // Если выделение пропало (мышка ушла, или только что вошли) — восстанавливаем.
            if (EventSystem.current.currentSelectedGameObject == null)
            {
                // Триггерим только при попытке ввода — иначе мышка не сможет ничего разсодинить.
                if (m_SubmitAction.WasPressedThisFrame()
                    || m_NavigateAction.ReadValue<Vector2>().sqrMagnitude != 0 )
                {
                    EventSystem.current.SetSelectedGameObject(DefaultSelection.gameObject);
                }
            }
        }
    }
}
