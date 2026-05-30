using System.Collections.Generic;
using UnityEngine;

namespace Unity.FPS.Game
{
    // ============================================================================
    // WeaponAmmoModule — модуль патронов оружия.
    //
    // Отвечает за:
    //  - текущее число патронов и их регенерацию;
    //  - режим автоматической перезарядки (когда оружие восполняет ammo само);
    //  - физические гильзы (Rigidbody-объекты, выпадающие из оружия) с пулом.
    //
    // Зачем вынесено в отдельный модуль: WeaponController становится тоньше,
    // и эту логику можно протестировать/заменить независимо.
    // ============================================================================
    public class WeaponAmmoModule : MonoBehaviour
    {
        [Header("Ammo Parameters")]
        // true: оружие само восстанавливает заряд (плазменное).
        // false: игроку нужно нажимать R.
        [Tooltip("Should the player manually reload")]
        public bool AutomaticReload = true;

        // У «физических» оружий есть реальная обойма и гильзы вылетают.
        [Tooltip("Has physical clip on the weapon and ammo shells are ejected when firing")]
        public bool HasPhysicalBullets = false;

        [Tooltip("Number of bullets in a clip")]
        public int ClipSize = 30;

        [Tooltip("Bullet Shell Casing")]
        public GameObject ShellCasing;

        // Точка вылета гильзы (обычно справа на затворе).
        [Tooltip("Weapon Ejection Port for physical ammo")]
        public Transform EjectionPort;

        [Tooltip("Force applied on the shell")]
        [Range(0.0f, 5.0f)]
        public float ShellCasingEjectionForce = 2.0f;

        // Сколько гильз держим в пуле. При превышении — переиспользуем самую старую.
        // Это микро-пул отдельный от GameObjectPoolManager (предшественник).
        [Tooltip("Maximum number of shell that can be spawned before reuse")]
        [Range(1, 30)]
        public int ShellPoolSize = 1;

        [Tooltip("Amount of ammo reloaded per second")]
        public float AmmoReloadRate = 1f;

        [Tooltip("Delay after the last shot before starting to reload")]
        public float AmmoReloadDelay = 2f;

        [Tooltip("Maximum amount of ammo in the gun")]
        public int MaxAmmo = 8;

        // Прокси-наружу: «насколько обойма полна» (0..1) — для UI-полоски.
        public float CurrentAmmoRatio { get; private set; }
        public bool IsReloading { get; private set; }
        // IsCooling — оружие сейчас восстанавливается. Используется для UI-эффектов.
        public bool IsCooling { get; private set; }

        // Текущий запас в обойме (float, чтобы регенерация шла плавно).
        float m_CurrentAmmo;
        // Сколько физических патронов «в кармане» (резерв вне обоймы).
        int m_CarriedPhysicalBullets;
        // Простой пул гильз. Queue — самые старые гильзы переиспользуем первыми.
        Queue<Rigidbody> m_PhysicalAmmoPool;

        void Awake()
        {
            // На старте обойма полная, резерв = размер обоймы (для физического оружия).
            m_CurrentAmmo = MaxAmmo;
            m_CarriedPhysicalBullets = HasPhysicalBullets ? ClipSize : 0;

            // Предсоздаём пул гильз — чтобы не Instantiate'ить во время стрельбы.
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

        // Вызывается WeaponController'ом каждый кадр.
        // Условия для авто-регенерации:
        //  - режим включён;
        //  - прошло хотя бы AmmoReloadDelay с последнего выстрела;
        //  - обойма не полная;
        //  - оружие сейчас не заряжается (charge-режим).
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

            // Защита от деления на ноль: если MaxAmmo=0 — считаем полной (UI покажет 1).
            CurrentAmmoRatio = MaxAmmo > 0 ? m_CurrentAmmo / MaxAmmo : 1f;
        }

        // Decrements both current ammo and carried bullets (used by charge mode).
        // Снять конкретное количество (для charge-режима, где трата дробная).
        public void UseAmmo(float amount)
        {
            m_CurrentAmmo = Mathf.Clamp(m_CurrentAmmo - amount, 0f, MaxAmmo);
            m_CarriedPhysicalBullets -= Mathf.RoundToInt(amount);
            m_CarriedPhysicalBullets = Mathf.Clamp(m_CarriedPhysicalBullets, 0, MaxAmmo);
        }

        // Decrements only current ammo by 1 (used by manual/auto shooting).
        // Снять 1 — обычный выстрел manual/automatic.
        public void DeductCurrentAmmo()
        {
            m_CurrentAmmo -= 1f;
        }

        // Ejects a shell casing and decrements carried bullets (used by manual/auto with physical bullets).
        // Выкидывание гильзы. Берём самую старую из очереди, кидаем в начало
        // (FIFO ротация — старейшая успела отскакать физикой и упокоиться).
        public void EjectShell()
        {
            if (!HasPhysicalBullets || m_PhysicalAmmoPool == null)
                return;

            Rigidbody nextShell = m_PhysicalAmmoPool.Dequeue();
            nextShell.transform.position = EjectionPort.transform.position;
            nextShell.transform.rotation = EjectionPort.transform.rotation;
            nextShell.gameObject.SetActive(true);
            // Отвязываем от оружия — гильза должна свободно падать на пол.
            nextShell.transform.SetParent(null);
            // Continuous — гильза быстрая и мелкая, без этого пролетит сквозь пол.
            nextShell.collisionDetectionMode = CollisionDetectionMode.Continuous;
            // Импульс по up самого Rigidbody — направление зависит от того,
            // как оружие повёрнуто. ForceMode.Impulse — это «мгновенный пинок».
            nextShell.AddForce(nextShell.transform.up * ShellCasingEjectionForce, ForceMode.Impulse);
            // Сразу кладём обратно в очередь — она вернётся к нам, когда переполнится.
            m_PhysicalAmmoPool.Enqueue(nextShell);

            m_CarriedPhysicalBullets--;
        }

        // Перезарядка по факту — заполнить обойму из резерва.
        public void Reload()
        {
            if (m_CarriedPhysicalBullets > 0)
                m_CurrentAmmo = Mathf.Min(m_CarriedPhysicalBullets, ClipSize);
            IsReloading = false;
        }

        // Запуск анимации перезарядки. Сама перезарядка завершается анимационным
        // событием, которое вызывает Reload().
        public void StartReloadAnimation(Animator animator)
        {
            // Перезаряжаться есть смысл только если в обойме меньше, чем мы носим.
            if (m_CurrentAmmo < m_CarriedPhysicalBullets)
            {
                animator.SetTrigger("Reload");
                IsReloading = true;
            }
        }

        // Подобрать патроны (например, с AmmoPickup).
        public void AddCarriablePhysicalBullets(int count) =>
            m_CarriedPhysicalBullets = Mathf.Min(m_CarriedPhysicalBullets + count, MaxAmmo);

        public float GetCurrentAmmo() => m_CurrentAmmo;
        // Floor, чтобы UI показывал целое число патронов даже при дробном CurrentAmmo.
        public int GetCurrentAmmoInt() => Mathf.FloorToInt(m_CurrentAmmo);
        public int GetCarriedPhysicalBullets() => m_CarriedPhysicalBullets;

        // Сколько ammo нужно для одного выстрела в долях от полной обоймы.
        // Используется как «индикатор готовности» для UI прицела.
        public float GetAmmoNeededToShoot(WeaponShootType shootType, float ammoUsedOnStartCharge, int bulletsPerShot) =>
            (shootType != WeaponShootType.Charge ? 1f : Mathf.Max(1f, ammoUsedOnStartCharge)) /
            (MaxAmmo * bulletsPerShot);
    }
}
