using System.Collections.Generic;
using UnityEngine;

namespace Unity.FPS.Game
{
    public class WeaponAmmoModule : MonoBehaviour
    {
        [Header("Ammo Parameters")]
        [Tooltip("Should the player manually reload")]
        public bool AutomaticReload = true;

        [Tooltip("Has physical clip on the weapon and ammo shells are ejected when firing")]
        public bool HasPhysicalBullets = false;

        [Tooltip("Number of bullets in a clip")]
        public int ClipSize = 30;

        [Tooltip("Bullet Shell Casing")]
        public GameObject ShellCasing;

        [Tooltip("Weapon Ejection Port for physical ammo")]
        public Transform EjectionPort;

        [Tooltip("Force applied on the shell")]
        [Range(0.0f, 5.0f)]
        public float ShellCasingEjectionForce = 2.0f;

        [Tooltip("Maximum number of shell that can be spawned before reuse")]
        [Range(1, 30)]
        public int ShellPoolSize = 1;

        [Tooltip("Amount of ammo reloaded per second")]
        public float AmmoReloadRate = 1f;

        [Tooltip("Delay after the last shot before starting to reload")]
        public float AmmoReloadDelay = 2f;

        [Tooltip("Maximum amount of ammo in the gun")]
        public int MaxAmmo = 8;

        public float CurrentAmmoRatio { get; private set; }
        public bool IsReloading { get; private set; }
        public bool IsCooling { get; private set; }

        float m_CurrentAmmo;
        int m_CarriedPhysicalBullets;
        Queue<Rigidbody> m_PhysicalAmmoPool;

        void Awake()
        {
            m_CurrentAmmo = MaxAmmo;
            m_CarriedPhysicalBullets = HasPhysicalBullets ? ClipSize : 0;

            if (HasPhysicalBullets)
            {
                m_PhysicalAmmoPool = new Queue<Rigidbody>(ShellPoolSize);
                for (int i = 0; i < ShellPoolSize; i++)
                {
                    GameObject shell = Instantiate(ShellCasing, transform);
                    shell.SetActive(false);
                    m_PhysicalAmmoPool.Enqueue(shell.GetComponent<Rigidbody>());
                }
            }
        }

        public void UpdateAmmo(float lastTimeShot, bool isCharging)
        {
            if (AutomaticReload && lastTimeShot + AmmoReloadDelay < Time.time && m_CurrentAmmo < MaxAmmo && !isCharging)
            {
                m_CurrentAmmo += AmmoReloadRate * Time.deltaTime;
                m_CurrentAmmo = Mathf.Clamp(m_CurrentAmmo, 0, MaxAmmo);
                IsCooling = true;
            }
            else
            {
                IsCooling = false;
            }

            CurrentAmmoRatio = MaxAmmo > 0 ? m_CurrentAmmo / MaxAmmo : 1f;
        }

        // Decrements both current ammo and carried bullets (used by charge mode).
        public void UseAmmo(float amount)
        {
            m_CurrentAmmo = Mathf.Clamp(m_CurrentAmmo - amount, 0f, MaxAmmo);
            m_CarriedPhysicalBullets -= Mathf.RoundToInt(amount);
            m_CarriedPhysicalBullets = Mathf.Clamp(m_CarriedPhysicalBullets, 0, MaxAmmo);
        }

        // Decrements only current ammo by 1 (used by manual/auto shooting).
        public void DeductCurrentAmmo()
        {
            m_CurrentAmmo -= 1f;
        }

        // Ejects a shell casing and decrements carried bullets (used by manual/auto with physical bullets).
        public void EjectShell()
        {
            if (!HasPhysicalBullets || m_PhysicalAmmoPool == null)
                return;

            Rigidbody nextShell = m_PhysicalAmmoPool.Dequeue();
            nextShell.transform.position = EjectionPort.transform.position;
            nextShell.transform.rotation = EjectionPort.transform.rotation;
            nextShell.gameObject.SetActive(true);
            nextShell.transform.SetParent(null);
            nextShell.collisionDetectionMode = CollisionDetectionMode.Continuous;
            nextShell.AddForce(nextShell.transform.up * ShellCasingEjectionForce, ForceMode.Impulse);
            m_PhysicalAmmoPool.Enqueue(nextShell);

            m_CarriedPhysicalBullets--;
        }

        public void Reload()
        {
            if (m_CarriedPhysicalBullets > 0)
                m_CurrentAmmo = Mathf.Min(m_CarriedPhysicalBullets, ClipSize);
            IsReloading = false;
        }

        public void StartReloadAnimation(Animator animator)
        {
            if (m_CurrentAmmo < m_CarriedPhysicalBullets)
            {
                animator.SetTrigger("Reload");
                IsReloading = true;
            }
        }

        public void AddCarriablePhysicalBullets(int count) =>
            m_CarriedPhysicalBullets = Mathf.Min(m_CarriedPhysicalBullets + count, MaxAmmo);

        public float GetCurrentAmmo() => m_CurrentAmmo;
        public int GetCurrentAmmoInt() => Mathf.FloorToInt(m_CurrentAmmo);
        public int GetCarriedPhysicalBullets() => m_CarriedPhysicalBullets;

        public float GetAmmoNeededToShoot(WeaponShootType shootType, float ammoUsedOnStartCharge, int bulletsPerShot) =>
            (shootType != WeaponShootType.Charge ? 1f : Mathf.Max(1f, ammoUsedOnStartCharge)) /
            (MaxAmmo * bulletsPerShot);
    }
}
