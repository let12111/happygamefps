using System;
using UnityEngine;
using UnityEngine.Events;

namespace Unity.FPS.Game
{
    public enum WeaponShootType
    {
        Manual,
        Automatic,
        Charge,
    }

    [Serializable]
    public struct CrosshairData
    {
        [Tooltip("The image that will be used for this weapon's crosshair")]
        public Sprite CrosshairSprite;

        [Tooltip("The size of the crosshair image")]
        public int CrosshairSize;

        [Tooltip("The color of the crosshair image")]
        public Color CrosshairColor;
    }

    [RequireComponent(typeof(WeaponAmmoModule))]
    [RequireComponent(typeof(WeaponChargeModule))]
    [RequireComponent(typeof(WeaponAudioModule))]
    public class WeaponController : MonoBehaviour
    {
        [Header("Information")]
        [Tooltip("The name that will be displayed in the UI for this weapon")]
        public string WeaponName;

        [Tooltip("The image that will be displayed in the UI for this weapon")]
        public Sprite WeaponIcon;

        [Tooltip("Default data for the crosshair")]
        public CrosshairData CrosshairDataDefault;

        [Tooltip("Data for the crosshair when targeting an enemy")]
        public CrosshairData CrosshairDataTargetInSight;

        [Header("Internal References")]
        [Tooltip("The root object for the weapon, this is what will be deactivated when the weapon isn't active")]
        public GameObject WeaponRoot;

        [Tooltip("Tip of the weapon, where the projectiles are shot")]
        public Transform WeaponMuzzle;

        [Header("Shoot Parameters")]
        [Tooltip("The type of weapon wil affect how it shoots")]
        public WeaponShootType ShootType;

        [Tooltip("The projectile prefab")]
        public ProjectileBase ProjectilePrefab;

        [Tooltip("Minimum duration between two shots")]
        public float DelayBetweenShots = 0.5f;

        [Tooltip("Angle for the cone in which the bullets will be shot randomly (0 means no spread at all)")]
        public float BulletSpreadAngle = 0f;

        [Tooltip("Amount of bullets per shot")]
        public int BulletsPerShot = 1;

        [Tooltip("Force that will push back the weapon after each shot")]
        [Range(0f, 2f)]
        public float RecoilForce = 1;

        [Tooltip("Ratio of the default FOV that this weapon applies while aiming")]
        [Range(0f, 1f)]
        public float AimZoomRatio = 1f;

        [Tooltip("Translation to apply to weapon arm when aiming with this weapon")]
        public Vector3 AimOffset;

        [Header("Visual")]
        [Tooltip("Optional weapon animator for OnShoot animations")]
        public Animator WeaponAnimator;

        [Tooltip("Prefab of the muzzle flash")]
        public GameObject MuzzleFlashPrefab;

        [Tooltip("Unparent the muzzle flash instance on spawn")]
        public bool UnparentMuzzleFlash;

        public UnityAction OnShoot;
        public event Action OnShootProcessed;

        public GameObject Owner { get; set; }
        public GameObject SourcePrefab { get; set; }
        public bool IsWeaponActive { get; private set; }
        public Vector3 MuzzleWorldVelocity { get; private set; }

        // Proxies to sub-modules so callers don't need to know which module owns the state.
        public float CurrentAmmoRatio => m_AmmoModule.CurrentAmmoRatio;
        public bool IsReloading => m_AmmoModule.IsReloading;
        public bool IsCooling => m_AmmoModule.IsCooling;
        public bool AutomaticReload => m_AmmoModule.AutomaticReload;
        public bool HasPhysicalBullets => m_AmmoModule.HasPhysicalBullets;
        public bool IsCharging => m_ChargeModule.IsCharging;
        public float CurrentCharge => m_ChargeModule.CurrentCharge;
        public float LastChargeTriggerTimestamp => m_ChargeModule.LastChargeTriggerTimestamp;

        const string k_AnimAttackParameter = "Attack";

        WeaponAmmoModule m_AmmoModule;
        WeaponChargeModule m_ChargeModule;
        WeaponAudioModule m_AudioModule;
        float m_LastTimeShot = Mathf.NegativeInfinity;
        Vector3 m_LastMuzzlePosition;

        void Awake()
        {
            m_AmmoModule = GetComponent<WeaponAmmoModule>();
            m_ChargeModule = GetComponent<WeaponChargeModule>();
            m_AudioModule = GetComponent<WeaponAudioModule>();

            m_LastMuzzlePosition = WeaponMuzzle.position;
        }

        void Update()
        {
            m_AmmoModule.UpdateAmmo(m_LastTimeShot, m_ChargeModule.IsCharging);
            m_ChargeModule.UpdateCharge(m_AmmoModule);
            m_AudioModule.UpdateContinuousShootSound(m_AmmoModule.GetCurrentAmmo());

            if (Time.deltaTime > 0)
            {
                MuzzleWorldVelocity = (WeaponMuzzle.position - m_LastMuzzlePosition) / Time.deltaTime;
                m_LastMuzzlePosition = WeaponMuzzle.position;
            }
        }

        public void ShowWeapon(bool show)
        {
            WeaponRoot.SetActive(show);
            if (show) m_AudioModule.PlayChangeSfx();
            IsWeaponActive = show;
        }

        public bool HandleShootInputs(bool inputDown, bool inputHeld, bool inputUp)
        {
            m_AudioModule.SetWantsToShoot(inputDown || inputHeld);
            switch (ShootType)
            {
                case WeaponShootType.Manual:
                    if (inputDown) return TryShoot();
                    return false;

                case WeaponShootType.Automatic:
                    if (inputHeld) return TryShoot();
                    return false;

                case WeaponShootType.Charge:
                    if (inputHeld)
                        m_ChargeModule.TryBeginCharge(m_AmmoModule, DelayBetweenShots, m_LastTimeShot, BulletsPerShot);
                    if (inputUp || (m_ChargeModule.AutomaticReleaseOnCharged && m_ChargeModule.CurrentCharge >= 1f))
                        return TryReleaseCharge();
                    return false;

                default:
                    return false;
            }
        }

        bool TryShoot()
        {
            if (m_AmmoModule.GetCurrentAmmo() >= 1f && m_LastTimeShot + DelayBetweenShots < Time.time)
            {
                HandleShoot();
                m_AmmoModule.DeductCurrentAmmo();
                return true;
            }
            return false;
        }

        bool TryReleaseCharge()
        {
            if (m_ChargeModule.IsCharging)
            {
                HandleShoot();
                m_ChargeModule.ReleaseCharge();
                return true;
            }
            return false;
        }

        void HandleShoot()
        {
            int bulletsPerShotFinal = ShootType == WeaponShootType.Charge
                ? Mathf.CeilToInt(m_ChargeModule.CurrentCharge * BulletsPerShot)
                : BulletsPerShot;

            for (int i = 0; i < bulletsPerShotFinal; i++)
            {
                Vector3 shotDirection = GetShotDirectionWithinSpread(WeaponMuzzle);
                ProjectileBase newProjectile;
                if (GameObjectPoolManager.Instance != null)
                {
                    var go = GameObjectPoolManager.Instance.Get(ProjectilePrefab.gameObject,
                        WeaponMuzzle.position, Quaternion.LookRotation(shotDirection));
                    newProjectile = go.GetComponent<ProjectileBase>();
                }
                else
                {
                    newProjectile = Instantiate(ProjectilePrefab, WeaponMuzzle.position,
                        Quaternion.LookRotation(shotDirection));
                }
                newProjectile.Shoot(this);
            }

            if (MuzzleFlashPrefab != null)
            {
                if (GameObjectPoolManager.Instance != null)
                {
                    var muzzleFlashInstance = GameObjectPoolManager.Instance.Get(MuzzleFlashPrefab,
                        WeaponMuzzle.position, WeaponMuzzle.rotation);
                    muzzleFlashInstance.transform.SetParent(UnparentMuzzleFlash ? null : WeaponMuzzle.transform);
                    // Only use timed release if no particle system — otherwise PooledParticleAutoRelease handles it
                    if (muzzleFlashInstance.GetComponent<PooledParticleAutoRelease>() == null)
                        GameObjectPoolManager.Instance.ReleaseDelayed(muzzleFlashInstance, 2f);
                }
                else
                {
                    GameObject muzzleFlashInstance = Instantiate(MuzzleFlashPrefab, WeaponMuzzle.position,
                        WeaponMuzzle.rotation, WeaponMuzzle.transform);
                    if (UnparentMuzzleFlash)
                        muzzleFlashInstance.transform.SetParent(null);
                    Destroy(muzzleFlashInstance, 2f);
                }
            }

            m_AmmoModule.EjectShell();
            m_LastTimeShot = Time.time;
            m_AudioModule.PlayShootSfx();

            if (WeaponAnimator)
                WeaponAnimator.SetTrigger(k_AnimAttackParameter);

            OnShoot?.Invoke();
            OnShootProcessed?.Invoke();
        }

        public Vector3 GetShotDirectionWithinSpread(Transform shootTransform)
        {
            float spreadAngleRatio = BulletSpreadAngle / 180f;
            return Vector3.Slerp(shootTransform.forward, UnityEngine.Random.insideUnitSphere, spreadAngleRatio);
        }

        // Forwarded methods — preserve public API for external callers.
        public void UseAmmo(float amount) => m_AmmoModule.UseAmmo(amount);
        public int GetCurrentAmmo() => m_AmmoModule.GetCurrentAmmoInt();
        public int GetCarriedPhysicalBullets() => m_AmmoModule.GetCarriedPhysicalBullets();
        public void AddCarriablePhysicalBullets(int count) => m_AmmoModule.AddCarriablePhysicalBullets(count);
        public float GetAmmoNeededToShoot() =>
            m_AmmoModule.GetAmmoNeededToShoot(ShootType, m_ChargeModule.AmmoUsedOnStartCharge, BulletsPerShot);
        public void StartReloadAnimation() => m_AmmoModule.StartReloadAnimation(GetComponent<Animator>());
    }
}
