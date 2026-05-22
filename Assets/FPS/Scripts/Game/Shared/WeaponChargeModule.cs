using UnityEngine;

namespace Unity.FPS.Game
{
    public class WeaponChargeModule : MonoBehaviour
    {
        [Header("Charging Parameters")]
        [Tooltip("Trigger a shot when maximum charge is reached")]
        public bool AutomaticReleaseOnCharged;

        [Tooltip("Duration to reach maximum charge")]
        public float MaxChargeDuration = 2f;

        [Tooltip("Initial ammo used when starting to charge")]
        public float AmmoUsedOnStartCharge = 1f;

        [Tooltip("Additional ammo used when charge reaches its maximum")]
        public float AmmoUsageRateWhileCharging = 1f;

        public bool IsCharging { get; private set; }
        public float CurrentCharge { get; private set; }
        public float LastChargeTriggerTimestamp { get; private set; }

        public void UpdateCharge(WeaponAmmoModule ammoModule)
        {
            if (!IsCharging || CurrentCharge >= 1f)
                return;

            float chargeLeft = 1f - CurrentCharge;
            float chargeAdded = MaxChargeDuration <= 0f
                ? chargeLeft
                : (1f / MaxChargeDuration) * Time.deltaTime;

            chargeAdded = Mathf.Clamp(chargeAdded, 0f, chargeLeft);

            float ammoRequired = chargeAdded * AmmoUsageRateWhileCharging;
            if (ammoRequired <= ammoModule.GetCurrentAmmo())
            {
                ammoModule.UseAmmo(ammoRequired);
                CurrentCharge = Mathf.Clamp01(CurrentCharge + chargeAdded);
            }
        }

        public bool TryBeginCharge(WeaponAmmoModule ammoModule, float delayBetweenShots, float lastTimeShot,
            int bulletsPerShot)
        {
            float currentAmmo = ammoModule.GetCurrentAmmo();
            bool hasEnoughAmmo = currentAmmo >= AmmoUsedOnStartCharge
                && Mathf.FloorToInt((currentAmmo - AmmoUsedOnStartCharge) * bulletsPerShot) > 0;

            if (!IsCharging && hasEnoughAmmo && lastTimeShot + delayBetweenShots < Time.time)
            {
                ammoModule.UseAmmo(AmmoUsedOnStartCharge);
                LastChargeTriggerTimestamp = Time.time;
                IsCharging = true;
                return true;
            }

            return false;
        }

        public void ReleaseCharge()
        {
            CurrentCharge = 0f;
            IsCharging = false;
        }
    }
}
