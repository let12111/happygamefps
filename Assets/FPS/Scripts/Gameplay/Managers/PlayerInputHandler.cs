using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace Unity.FPS.Gameplay
{
    // ============================================================================
    // PlayerInputHandler — слой абстракции над вводом игрока.
    //
    // Зачем: PlayerCharacterController и PlayerWeaponsManager не должны знать
    // про мышь, клавиатуру, геймпад. Они спрашивают абстрактные вещи —
    // «нажат ли прыжок?», «куда смотрит мышка?». Если завтра переедем на
    // новую систему ввода или добавим тач — поправим только этот файл.
    //
    // Использует Unity New Input System (InputSystem.actions из InputActionAsset).
    // VContainer [Inject] — DI: IGameFlowManager автоматически приходит из
    // GameLifetimeScope (см. UI/GameLifetimeScope.cs).
    // ============================================================================
    public class PlayerInputHandler : MonoBehaviour
    {
        [Tooltip("Sensitivity multiplier for moving the camera around")]
        public float LookSensitivity = 1f;

        // WebGL: мышь в браузере обрабатывается с другой акселерацией, сенс выше.
        [Tooltip("Additional sensitivity multiplier for WebGL")]
        public float WebGLLookSensitivityMultiplier = 0.25f;

        // Триггер геймпада — это аналоговая ось. Считаем «нажатым» только когда
        // больше TriggerAxisThreshold (защита от случайных микро-нажатий).
        [Tooltip("Limit to consider an input when using a trigger on a controller")]
        public float TriggerAxisThreshold = 0.4f;

        // Опции инвертирования осей — кому-то удобнее наоборот.
        [Tooltip("Used to flip the vertical input axis")]
        public bool InvertYAxis = false;

        [Tooltip("Used to flip the horizontal input axis")]
        public bool InvertXAxis = false;

        IGameFlowManager m_GameFlowManager;
        PlayerCharacterController m_PlayerCharacterController;
        // Запоминаем «удерживался ли огонь в прошлом кадре» — нужно для
        // вычисления Down (только что нажали) и Released (только что отпустили).
        bool m_FireInputWasHeld;

        // InputAction — это «именованное действие», за которым прячется набор
        // конкретных кнопок/осей. Имена приходят из InputActions asset.
        private InputAction m_MoveAction;
        private InputAction m_LookAction;
        private InputAction m_JumpAction;
        private InputAction m_FireAction;
        private InputAction m_AimAction;
        private InputAction m_SprintAction;
        private InputAction m_CrouchAction;
        private InputAction m_ReloadAction;
        private InputAction m_NextWeaponAction;

        // VContainer DI: при создании контейнер вызовет Construct и передаст менеджера.
        // Так зависимости видны явно и заменяются на моки в тестах.
        [Inject]
        public void Construct(IGameFlowManager gameFlowManager)
        {
            m_GameFlowManager = gameFlowManager;
        }

        void Start()
        {
            m_PlayerCharacterController = GetComponent<PlayerCharacterController>();
            DebugUtility.HandleErrorIfNullGetComponent<PlayerCharacterController, PlayerInputHandler>(
                m_PlayerCharacterController, this, gameObject);

            // Локаем мышь в центре экрана — стандарт для FPS.
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Находим действия по их пути в asset'е.
            m_MoveAction = InputSystem.actions.FindAction("Player/Move");
            m_LookAction = InputSystem.actions.FindAction("Player/Look");
            m_JumpAction = InputSystem.actions.FindAction("Player/Jump");
            m_FireAction = InputSystem.actions.FindAction("Player/Fire");
            m_AimAction = InputSystem.actions.FindAction("Player/Aim");
            m_SprintAction = InputSystem.actions.FindAction("Player/Sprint");
            m_CrouchAction = InputSystem.actions.FindAction("Player/Crouch");
            m_ReloadAction = InputSystem.actions.FindAction("Player/Reload");
            m_NextWeaponAction = InputSystem.actions.FindAction("Player/NextWeapon");

            // По умолчанию действия отключены — надо включить.
            m_MoveAction.Enable();
            m_LookAction.Enable();
            m_JumpAction.Enable();
            m_FireAction.Enable();
            m_AimAction.Enable();
            m_SprintAction.Enable();
            m_CrouchAction.Enable();
            m_ReloadAction.Enable();
            m_NextWeaponAction.Enable();
        }

        // Чистим за собой — действия после выгрузки сцены продолжали бы получать ввод.
        // ?. защищает от случая «Start не выполнился».
        void OnDestroy()
        {
            m_MoveAction?.Disable();
            m_LookAction?.Disable();
            m_JumpAction?.Disable();
            m_FireAction?.Disable();
            m_AimAction?.Disable();
            m_SprintAction?.Disable();
            m_CrouchAction?.Disable();
            m_ReloadAction?.Disable();
            m_NextWeaponAction?.Disable();
        }

        // LateUpdate — после Update'ов всех скриптов. Запоминаем состояние огня,
        // чтобы в следующем кадре можно было вычислить Down/Released.
        void LateUpdate()
        {
            m_FireInputWasHeld = GetFireInputHeld();
        }

        // Можно ли сейчас обрабатывать ввод. Нельзя если:
        //  - курсор разлочен (значит игрок в меню/настройках);
        //  - игра завершается (затемнение, переход в финальную сцену).
        public bool CanProcessInput()
        {
            return Cursor.lockState == CursorLockMode.Locked && !m_GameFlowManager.GameIsEnding;
        }

        public Vector3 GetMoveInput()
        {
            if (CanProcessInput())
            {
                // ReadValue<Vector2> для движения — (x, y) от WASD/стика.
                var input = m_MoveAction.ReadValue<Vector2>();
                // Y геймпад → Z игрока (вперёд). Y игрока (вверх) тут не нужен.
                Vector3 move = new Vector3(input.x, 0f, input.y);

                // constrain move input to a maximum magnitude of 1, otherwise diagonal movement might exceed the max move speed defined
                // Диагональ (1,1) даст длину √2 — без зажатия игрок двигался бы по диагонали быстрее.
                move = Vector3.ClampMagnitude(move, 1);

                return move;
            }

            return Vector3.zero;
        }

        public float GetLookInputsHorizontal()
        {
            if (!CanProcessInput())
                return 0.0f;

            float input = m_LookAction.ReadValue<Vector2>().x;

            // Инверсия — для игроков, привыкших к обратному mapping'у.
            if (InvertXAxis)
                input *= -1;

            input *= LookSensitivity;

            // Условная компиляция: только под WebGL применяем доп. множитель.
#if UNITY_WEBGL
            // Mouse tends to be even more sensitive in WebGL due to mouse acceleration, so reduce it even more
            input *= WebGLLookSensitivityMultiplier;
#endif

            return input;
        }

        public float GetLookInputsVertical()
        {
            if (!CanProcessInput())
                return 0.0f;

            float input = m_LookAction.ReadValue<Vector2>().y;

            if (InvertYAxis)
                input *= -1;

            input *= LookSensitivity;

#if UNITY_WEBGL
            // Mouse tends to be even more sensitive in WebGL due to mouse acceleration, so reduce it even more
            input *= WebGLLookSensitivityMultiplier;
#endif

            return input;
        }

        // WasPressedThisFrame — только момент нажатия (один кадр), не зажатие.
        public bool GetJumpInputDown()
        {
            if (CanProcessInput())
            {
                return m_JumpAction.WasPressedThisFrame();
            }

            return false;
        }

        // IsPressed — пока зажато.
        public bool GetJumpInputHeld()
        {
            if (CanProcessInput())
            {
                return m_JumpAction.IsPressed();
            }

            return false;
        }

        // Огонь Down: сейчас зажат, а в прошлом кадре не был.
        public bool GetFireInputDown()
        {
            return GetFireInputHeld() && !m_FireInputWasHeld;
        }

        // Огонь Released: сейчас не зажат, а в прошлом кадре был.
        public bool GetFireInputReleased()
        {
            return !GetFireInputHeld() && m_FireInputWasHeld;
        }

        public bool GetFireInputHeld()
        {
            if (CanProcessInput())
            {
                return m_FireAction.IsPressed();
            }

            return false;
        }

        public bool GetAimInputHeld()
        {
            if (CanProcessInput())
            {
                return m_AimAction.IsPressed();
            }

            return false;
        }

        public bool GetSprintInputHeld()
        {
            if (CanProcessInput())
            {
                return m_SprintAction.IsPressed();
            }

            return false;
        }

        public bool GetCrouchInputDown()
        {
            if (CanProcessInput())
            {
                return m_CrouchAction.WasPressedThisFrame();
            }

            return false;
        }

        public bool GetCrouchInputReleased()
        {
            if (CanProcessInput())
            {
                return m_CrouchAction.WasReleasedThisFrame();
            }

            return false;
        }

        public bool GetReloadButtonDown()
        {
            if (CanProcessInput())
            {
                return m_ReloadAction.WasPressedThisFrame();
            }

            return false;
        }

        // Смена оружия колесом мыши: возвращаем +1, -1 или 0.
        // Колесо назад (минус) → следующее оружие (1); вперёд (плюс) → предыдущее (-1).
        public int GetSwitchWeaponInput()
        {
            if (CanProcessInput())
            {
                var input = m_NextWeaponAction.ReadValue<float>();

                if (input > 0f)
                    return -1;

                if (input < 0f)
                    return 1;
            }

            return 0;
        }

        // Выбор оружия по цифровым клавишам 1-9. Возвращает номер слота или 0.
        public int GetSelectWeaponInput()
        {
            if (CanProcessInput())
            {
                if (Keyboard.current.digit1Key.wasPressedThisFrame)
                    return 1;
                if (Keyboard.current.digit2Key.wasPressedThisFrame)
                    return 2;
                if (Keyboard.current.digit3Key.wasPressedThisFrame)
                    return 3;
                if (Keyboard.current.digit4Key.wasPressedThisFrame)
                    return 4;
                if (Keyboard.current.digit5Key.wasPressedThisFrame)
                    return 5;
                if (Keyboard.current.digit6Key.wasPressedThisFrame)
                    return 6;
                if (Keyboard.current.digit7Key.wasPressedThisFrame)
                    return 7;
                if (Keyboard.current.digit8Key.wasPressedThisFrame)
                    return 8;
                if (Keyboard.current.digit9Key.wasPressedThisFrame)
                    return 9;
            }

            return 0;
        }
    }
}
