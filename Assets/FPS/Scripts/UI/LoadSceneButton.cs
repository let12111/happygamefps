using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Unity.FPS.UI
{
    // ============================================================================
    // LoadSceneButton — кнопка в меню, загружающая названную сцену.
    // Также реагирует на «Submit» (Enter/A на геймпаде) когда кнопка выбрана.
    // ============================================================================
    public class LoadSceneButton : MonoBehaviour
    {
        public string SceneName = "";

        private InputAction m_SubmitAction;

        void Start()
        {
            m_SubmitAction = InputSystem.actions.FindAction("UI/Submit");
            m_SubmitAction.Enable();
        }

        void Update()
        {
            // Нажатие Submit срабатывает только если ИМЕННО эта кнопка сейчас выделена.
            if (EventSystem.current.currentSelectedGameObject == gameObject
                && m_SubmitAction.WasPressedThisFrame())
            {
                LoadTargetScene();
            }
        }

        // Вызывается также из Inspector у Button.OnClick.
        public void LoadTargetScene()
        {
            SceneManager.LoadScene(SceneName);
        }
    }
}
