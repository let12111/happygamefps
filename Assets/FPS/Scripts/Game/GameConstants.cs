namespace Unity.FPS.Game
{
    // ============================================================================
    // GameConstants — строковые имена для старой Input Manager Unity.
    // Зачем константы: чтобы не дублировать «магические строки» по всему коду.
    // Если завтра имя оси «Vertical» переименуют — поменяем тут в одном месте.
    //
    // Префикс k_ — соглашение из мира C++/Unity: «k» = constant. Подчёркивает,
    // что это неизменяемое значение времени компиляции.
    //
    // Несмотря на наличие нового Input System в проекте, эти константы оставлены
    // для совместимости и используются в PlayerInputHandler.
    // ============================================================================
    public class GameConstants
    {
        // all the constant string used across the game

        // Оси для движения с клавиатуры/стика: вперёд-назад / влево-вправо.
        public const string k_AxisNameVertical = "Vertical";
        public const string k_AxisNameHorizontal = "Horizontal";
        // Оси мыши — поворот камеры (Y — вверх/вниз, X — влево/вправо).
        public const string k_MouseAxisNameVertical = "Mouse Y";
        public const string k_MouseAxisNameHorizontal = "Mouse X";
        // Те же оси, но с правого стика геймпада — отдельные, потому что
        // у мыши и стика разные кривые чувствительности.
        public const string k_AxisNameJoystickLookVertical = "Look Y";
        public const string k_AxisNameJoystickLookHorizontal = "Look X";

        // Кнопки игрового действия (стрельба, прицел, спринт, прыжок, присед).
        public const string k_ButtonNameAim = "Aim";
        public const string k_ButtonNameFire = "Fire";
        public const string k_ButtonNameSprint = "Sprint";
        public const string k_ButtonNameJump = "Jump";
        public const string k_ButtonNameCrouch = "Crouch";

        // Отдельные имена для геймпада — у мыши и геймпада разные триггеры,
        // надо различать «нажал ЛКМ» и «нажал триггер геймпада».
        public const string k_ButtonNameGamepadFire = "Gamepad Fire";
        public const string k_ButtonNameGamepadAim = "Gamepad Aim";
        public const string k_ButtonNameSwitchWeapon = "Mouse ScrollWheel";
        public const string k_ButtonNameGamepadSwitchWeapon = "Gamepad Switch";
        public const string k_ButtonNameNextWeapon = "NextWeapon";
        // Меню паузы и кнопки навигации в UI.
        public const string k_ButtonNamePauseMenu = "Pause Menu";
        public const string k_ButtonNameSubmit = "Submit";
        public const string k_ButtonNameCancel = "Cancel";
        // Перезарядка.
        public const string k_ButtonReload = "Reload";
    }
}
