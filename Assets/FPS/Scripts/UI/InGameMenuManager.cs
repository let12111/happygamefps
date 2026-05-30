using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer;

namespace Unity.FPS.UI
{
    // ============================================================================
    // InGameMenuManager — меню паузы. По Esc открывается/закрывается.
    //
    // Что делает при открытии:
    //  - расфиксирует курсор, делает видимым;
    //  - выставляет Time.timeScale = 0 (мир замирает);
    //  - приглушает мастер-громкость;
    //  - очищает выделение в EventSystem.
    //
    // Настройки сохраняются в PlayerPrefs (запекаются между запусками):
    //  - чувствительность мыши;
    //  - вкл/выкл теней;
    //  - вкл/выкл FPS-счётчика;
    //  - неуязвимость (отладочная).
    // ============================================================================
    public class InGameMenuManager : MonoBehaviour
    {
        [Tooltip("Root GameObject of the menu used to toggle its activation")]
        public GameObject MenuRoot;

        // Громкость пока меню открыто — нельзя ставить совсем 0, иначе log10
        // в SetMasterVolume сломается. Поэтому [Range(0.001f, 1f)].
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

        // Ключи PlayerPrefs — выносим в константы, чтобы не дублировать
        // строки и не сломать сохранения опечаткой.
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
            // Health у игрока на том же объекте, что и PlayerInputHandler.
            m_PlayerHealth = m_PlayerInputsHandler.GetComponent<Health>();
            DebugUtility.HandleErrorIfNullGetComponent<Health, InGameMenuManager>(m_PlayerHealth, this, gameObject);

            MenuRoot.SetActive(false);

            LoadSettings();

            // Подписки на изменения слайдеров/тогглов.
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
            // Клик мышью когда меню скрыто — залочить курсор обратно.
            // Полезно если фокус ушёл (Alt+Tab и обратно) — игрок кликает и
            // моментально возвращается в игру.
            if (!MenuRoot.activeSelf && (m_ClickAction?.WasPressedThisFrame() ?? false))
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            // Esc открывает меню или закрывает уже открытое.
            if (m_MenuAction.WasPressedThisFrame()
                || (MenuRoot.activeSelf && m_CancelAction.WasPressedThisFrame()))
            {
                // Если показывается «управление» подменю — Esc сначала закроет его.
                if (ControlImage.activeSelf)
                {
                    ControlImage.SetActive(false);
                    return;
                }

                SetPauseMenuActivation(!MenuRoot.activeSelf);
            }

            // Если пользователь начал двигать стик по Y — фокусируемся на слайдере.
            if (m_NavigateAction.ReadValue<Vector2>().y != 0)
            {
                if (EventSystem.current.currentSelectedGameObject == null)
                {
                    EventSystem.current.SetSelectedGameObject(null);
                    LookSensitivitySlider.Select();
                }
            }
        }

        // Привязывается из UI к кнопке закрытия в Inspector.
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
                // Time.timeScale = 0 → весь Update'ы продолжают идти, но Time.deltaTime = 0,
                // физика стоит, анимации замирают. Это пауза «по-юнитёвски».
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

        // Чтение настроек из PlayerPrefs. Если ключа нет — берём текущее значение.
        void LoadSettings()
        {
            float sensitivity = PlayerPrefs.GetFloat(k_PrefSensitivity, m_PlayerInputsHandler.LookSensitivity);
            m_PlayerInputsHandler.LookSensitivity = sensitivity;
            LookSensitivitySlider.value = sensitivity;

            // PlayerPrefs не хранит bool — используем int 0/1.
            bool shadowsOn = PlayerPrefs.GetInt(k_PrefShadows, 1) == 1;
            QualitySettings.shadows = shadowsOn ? ShadowQuality.All : ShadowQuality.Disable;
            ShadowsToggle.isOn = shadowsOn;

            bool framerateOn = PlayerPrefs.GetInt(k_PrefFramerate, 0) == 1;
            m_FramerateCounter.UIText.gameObject.SetActive(framerateOn);
            FramerateToggle.isOn = framerateOn;
        }

        // Каждый обработчик меняет состояние И тут же сохраняет в PlayerPrefs.
        void OnMouseSensitivityChanged(float newValue)
        {
            // Нулевая чувствительность = камера не крутится. Защищаемся.
            m_PlayerInputsHandler.LookSensitivity = Mathf.Max(0.001f, newValue);
            PlayerPrefs.SetFloat(k_PrefSensitivity, m_PlayerInputsHandler.LookSensitivity);
            // Save — форсированная запись на диск. Без него Unity запишет на выходе.
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
            // Бессмертие не сохраняем — каждая игра должна начинаться «честно».
            m_PlayerHealth.Invincible = newValue;
        }

        void OnFramerateCounterChanged(bool newValue)
        {
            m_FramerateCounter.UIText.gameObject.SetActive(newValue);
            PlayerPrefs.SetInt(k_PrefFramerate, newValue ? 1 : 0);
            PlayerPrefs.Save();
        }

        // Привязывается из Button.OnClick в Inspector.
        public void OnShowControlButtonClicked(bool show)
        {
            ControlImage.SetActive(show);
        }

        void OnDestroy()
        {
            // Снимаем подписки и выключаем InputAction'ы.
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
