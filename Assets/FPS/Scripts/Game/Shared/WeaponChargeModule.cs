using UnityEngine;

namespace Unity.FPS.Game
{
    // ============================================================================
    // WeaponChargeModule — модуль «накопительной» стрельбы для charge-оружия.
    //
    // Поведение: игрок держит кнопку, заряд от 0 копится до 1 за MaxChargeDuration
    // секунд. На каждую долю заряда тратится часть патронов. Отпустил — выстрел;
    // сила выстрела масштабируется по CurrentCharge.
    //
    // Используется только если ShootType == Charge. Висит обязательно (RequireComponent
    // в WeaponController), но в простом оружии ничего не делает.
    // ============================================================================
    public class WeaponChargeModule : MonoBehaviour
    {
        [Header("Charging Parameters")]
        // Если true — при достижении полного заряда выстрел сам собой.
        [Tooltip("Trigger a shot when maximum charge is reached")]
        public bool AutomaticReleaseOnCharged;

        [Tooltip("Duration to reach maximum charge")]
        public float MaxChargeDuration = 2f;

        // Сколько ammo «съест» само начало зарядки (стоимость старта).
        [Tooltip("Initial ammo used when starting to charge")]
        public float AmmoUsedOnStartCharge = 1f;

        // Сколько ammo съедает каждая «единица» заряда.
        [Tooltip("Additional ammo used when charge reaches its maximum")]
        public float AmmoUsageRateWhileCharging = 1f;

        public bool IsCharging { get; private set; }
        // 0..1 — текущая доля заряда. Снаружи только читать.
        public float CurrentCharge { get; private set; }
        // Время старта последней зарядки — для UI и эффектов.
        public float LastChargeTriggerTimestamp { get; private set; }

        // Тикается WeaponController'ом каждый кадр.
        public void UpdateCharge(WeaponAmmoModule ammoModule)
        {
            // Не заряжаем — выходим. Достиг 100% — тоже стоим, ждём выстрела.
            if (!IsCharging || CurrentCharge >= 1f)
                return;

            // Сколько осталось до полного заряда.
            float chargeLeft = 1f - CurrentCharge;
            // Сколько мы добавили бы за этот кадр. Защита от деления на 0
            // (мгновенный заряд) — добавим всё разом.
            float chargeAdded = MaxChargeDuration <= 0f
                ? chargeLeft
                : (1f / MaxChargeDuration) * Time.deltaTime;

            // Не «перепрыгиваем» 1 — зажимаем по chargeLeft.
            chargeAdded = Mathf.Clamp(chargeAdded, 0f, chargeLeft);

            // Сколько ammo надо за этот прирост.
            float ammoRequired = chargeAdded * AmmoUsageRateWhileCharging;
            // Если ammo хватает — тратим и увеличиваем заряд.
            // Иначе зарядка «замораживается» — игрок не выстрелит мощнее, чем позволяет
            // оставшийся боезапас.
            if (ammoRequired <= ammoModule.GetCurrentAmmo())
            {
                ammoModule.UseAmmo(ammoRequired);
                CurrentCharge = Mathf.Clamp01(CurrentCharge + chargeAdded);
            }
        }

        // Попытка начать зарядку. Возвращает true если действительно начали.
        public bool TryBeginCharge(WeaponAmmoModule ammoModule, float delayBetweenShots, float lastTimeShot,
            int bulletsPerShot)
        {
            float currentAmmo = ammoModule.GetCurrentAmmo();
            // Проверки:
            //  1) ammo хватит на стартовую стоимость;
            //  2) оставшегося хватит хотя бы на одну пулю (учитывая bulletsPerShot).
            bool hasEnoughAmmo = currentAmmo >= AmmoUsedOnStartCharge
                && Mathf.FloorToInt((currentAmmo - AmmoUsedOnStartCharge) * bulletsPerShot) > 0;

            // Стартуем только если: ещё не заряжаем, ammo хватает, и кулдаун прошёл.
            if (!IsCharging && hasEnoughAmmo && lastTimeShot + delayBetweenShots < Time.time)
            {
                ammoModule.UseAmmo(AmmoUsedOnStartCharge);
                LastChargeTriggerTimestamp = Time.time;
                IsCharging = true;
                return true;
            }

            return false;
        }

        // Сбросить состояние зарядки (после выстрела).
        public void ReleaseCharge()
        {
            CurrentCharge = 0f;
            IsCharging = false;
        }
    }
}
