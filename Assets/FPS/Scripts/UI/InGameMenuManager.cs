using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer;

namespace Unity.FPS.UI
{
    public class InGameMenuManager : MonoBehaviour
    {
        [Tooltip("Root GameObject of the menu used to toggle its activation")]
        public GameObject MenuRoot;

        [Tooltip("Master volume when menu is open")] [Range(0.001f, 1f)]
        public float VolumeWhenMenuOpen = 0.5f;

        [Tooltip("Slider component for look sensitivity")]
        public Slider LookSensitivitySlider;

        [Tooltip("Toggle component for shadows")]
        public Toggle ShadowsToggle;

        [Tooltip("Toggle component for invincibility")]
        public Toggle InvincibilityToggle;

        [Tooltip("Toggle component for framerate display")]
        public Toggle FramerateToggle;

        [Tooltip("GameObject for the controls")]
        public GameObject ControlImage;

        PlayerInputHandler m_PlayerInputsHandler;
        Health m_PlayerHealth;
        FramerateCounter m_FramerateCounter;

        private InputAction m_SubmitAction;
        private InputAction m_CancelAction;
        private InputAction m_NavigateAction;
        private InputAction m_MenuAction;
        private InputAction m_ClickAction;

        const string k_PrefSensitivity = "LookSensitivity";
        const string k_PrefShadows = "ShadowsEnabled";
        const string k_PrefFramerate = "FramerateEnabled";

        [Inject]
        public void Construct(PlayerInputHandler playerInputsHandler, FramerateCounter framerateCounter)
        {
            m_PlayerInputsHandler = playerInputsHandler;
            m_FramerateCounter = framerateCounter;
        }

        void Start()
        {
            m_PlayerHealth = m_PlayerInputsHandler.GetComponent<Health>();
            DebugUtility.HandleErrorIfNullGetComponent<Health, InGameMenuManager>(m_PlayerHealth, this, gameObject);

            MenuRoot.SetActive(false);

            LoadSettings();

            LookSensitivitySlider.onValueChanged.AddListener(OnMouseSensitivityChanged);
            ShadowsToggle.onValueChanged.AddListener(OnShadowsChanged);
            InvincibilityToggle.isOn = m_PlayerHealth.Invincible;
            InvincibilityToggle.onValueChanged.AddListener(OnInvincibilityChanged);
            FramerateToggle.onValueChanged.AddListener(OnFramerateCounterChanged);

            m_SubmitAction = InputSystem.actions.FindAction("UI/Submit");
            m_CancelAction = InputSystem.actions.FindAction("UI/Cancel");
            m_NavigateAction = InputSystem.actions.FindAction("UI/Navigate");
            m_MenuAction = InputSystem.actions.FindAction("UI/Menu");
            m_ClickAction = InputSystem.actions.FindAction("UI/Click");

            m_SubmitAction.Enable();
            m_CancelAction.Enable();
            m_NavigateAction.Enable();
            m_MenuAction.Enable();
            m_ClickAction?.Enable();
        }

        void Update()
        {
            if (!MenuRoot.activeSelf && (m_ClickAction?.WasPressedThisFrame() ?? false))
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            if (m_MenuAction.WasPressedThisFrame()
                || (MenuRoot.activeSelf && m_CancelAction.WasPressedThisFrame()))
            {
                if (ControlImage.activeSelf)
                {
                    ControlImage.SetActive(false);
                    return;
                }

                SetPauseMenuActivation(!MenuRoot.activeSelf);
            }

            if (m_NavigateAction.ReadValue<Vector2>().y != 0)
            {
                if (EventSystem.current.currentSelectedGameObject == null)
                {
                    EventSystem.current.SetSelectedGameObject(null);
                    LookSensitivitySlider.Select();
                }
            }
        }

        public void ClosePauseMenu()
        {
            SetPauseMenuActivation(false);
        }

        void SetPauseMenuActivation(bool active)
        {
            MenuRoot.SetActive(active);

            if (MenuRoot.activeSelf)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                Time.timeScale = 0f;
                AudioUtility.SetMasterVolume(VolumeWhenMenuOpen);

                EventSystem.current.SetSelectedGameObject(null);
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                Time.timeScale = 1f;
                AudioUtility.SetMasterVolume(1);
            }
        }

        void LoadSettings()
        {
            float sensitivity = PlayerPrefs.GetFloat(k_PrefSensitivity, m_PlayerInputsHandler.LookSensitivity);
            m_PlayerInputsHandler.LookSensitivity = sensitivity;
            LookSensitivitySlider.value = sensitivity;

            bool shadowsOn = PlayerPrefs.GetInt(k_PrefShadows, 1) == 1;
            QualitySettings.shadows = shadowsOn ? ShadowQuality.All : ShadowQuality.Disable;
            ShadowsToggle.isOn = shadowsOn;

            bool framerateOn = PlayerPrefs.GetInt(k_PrefFramerate, 0) == 1;
            m_FramerateCounter.UIText.gameObject.SetActive(framerateOn);
            FramerateToggle.isOn = framerateOn;
        }

        void OnMouseSensitivityChanged(float newValue)
        {
            m_PlayerInputsHandler.LookSensitivity = Mathf.Max(0.001f, newValue);
            PlayerPrefs.SetFloat(k_PrefSensitivity, m_PlayerInputsHandler.LookSensitivity);
            PlayerPrefs.Save();
        }

        void OnShadowsChanged(bool newValue)
        {
            QualitySettings.shadows = newValue ? ShadowQuality.All : ShadowQuality.Disable;
            PlayerPrefs.SetInt(k_PrefShadows, newValue ? 1 : 0);
            PlayerPrefs.Save();
        }

        void OnInvincibilityChanged(bool newValue)
        {
            m_PlayerHealth.Invincible = newValue;
        }

        void OnFramerateCounterChanged(bool newValue)
        {
            m_FramerateCounter.UIText.gameObject.SetActive(newValue);
            PlayerPrefs.SetInt(k_PrefFramerate, newValue ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void OnShowControlButtonClicked(bool show)
        {
            ControlImage.SetActive(show);
        }

        void OnDestroy()
        {
            LookSensitivitySlider.onValueChanged.RemoveListener(OnMouseSensitivityChanged);
            ShadowsToggle.onValueChanged.RemoveListener(OnShadowsChanged);
            InvincibilityToggle.onValueChanged.RemoveListener(OnInvincibilityChanged);
            FramerateToggle.onValueChanged.RemoveListener(OnFramerateCounterChanged);

            m_SubmitAction?.Disable();
            m_CancelAction?.Disable();
            m_NavigateAction?.Disable();
            m_MenuAction?.Disable();
            m_ClickAction?.Disable();
        }
    }
}
