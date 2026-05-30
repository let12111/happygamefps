using System;
using UnityEngine;
using UnityEngine.Events;

namespace Unity.FPS.Game
{
    // ============================================================================
    // Перечисление режимов стрельбы оружия.
    // Manual    — выстрел на каждый клик (пистолет).
    // Automatic — пока зажата клавиша — стреляет (автомат).
    // Charge    — копит заряд, выстрел на отпускание (railgun).
    // ============================================================================
    public enum WeaponShootType
    {
        Manual,
        Automatic,
        Charge,
    }

    // CrosshairData — данные прицела (картинка, размер, цвет).
    // Хранится В оружии — каждое оружие имеет два состояния: «по умолчанию» и
    // «цель в прицеле». [Serializable] нужно, чтобы Unity показал поля в Inspector.
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

    // ============================================================================
    // WeaponController — главный класс оружия. Висит на префабе оружия.
    //
    // Архитектура: WeaponController сам не хранит ВСЁ. Он делегирует «грязную»
    // работу трём модулям-компонентам:
    //   WeaponAmmoModule  — патроны, перезарядка, гильзы.
    //   WeaponChargeModule — накопительный заряд.
    //   WeaponAudioModule — звуки выстрела, смены оружия, луп-стрельбы.
    //
    // [RequireComponent] — Unity автоматически добавит эти модули при создании,
    // и не даст их удалить пока висит WeaponController. Гарантия инвариантов.
    //
    // Сам WeaponController отвечает за:
    //  - реакцию на ввод (HandleShootInputs);
    //  - спавн снарядов и muzzle flash;
    //  - расчёт MuzzleWorldVelocity (для наследования скорости пулям);
    //  - проксирование к модулям, чтобы внешний код не знал о них.
    // ============================================================================
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

        // Два состояния прицела: обычный и при наведении на врага.
        // CrosshairManager сам меняет картинку, мы только хранит данные.
        [Tooltip("Default data for the crosshair")]
        public CrosshairData CrosshairDataDefault;

        [Tooltip("Data for the crosshair when targeting an enemy")]
        public CrosshairData CrosshairDataTargetInSight;

        [Header("Internal References")]
        // Корневой объект — выключается при смене на другое оружие, чтобы
        // спрятать модель оружия из вида.
        [Tooltip("The root object for the weapon, this is what will be deactivated when the weapon isn't active")]
        public GameObject WeaponRoot;

        // Дуло — отсюда летят пули.
        [Tooltip("Tip of the weapon, where the projectiles are shot")]
        public Transform WeaponMuzzle;

        [Header("Shoot Parameters")]
        [Tooltip("The type of weapon wil affect how it shoots")]
        public WeaponShootType ShootType;

        [Tooltip("The projectile prefab")]
        public ProjectileBase ProjectilePrefab;

        // Скорострельность: минимум секунд между выстрелами.
        [Tooltip("Minimum duration between two shots")]
        public float DelayBetweenShots = 0.5f;

        // Угол конуса разброса. 0 = идеально точный выстрел.
        [Tooltip("Angle for the cone in which the bullets will be shot randomly (0 means no spread at all)")]
        public float BulletSpreadAngle = 0f;

        // Сколько пуль на один выстрел (дробовик > 1).
        [Tooltip("Amount of bullets per shot")]
        public int BulletsPerShot = 1;

        // Сила отдачи — толкает модель оружия назад.
        [Tooltip("Force that will push back the weapon after each shot")]
        [Range(0f, 2f)]
        public float RecoilForce = 1;

        // Множитель FOV при прицеливании (зум). 1 = без зума, 0.5 = двукратный.
        [Tooltip("Ratio of the default FOV that this weapon applies while aiming")]
        [Range(0f, 1f)]
        public float AimZoomRatio = 1f;

        // Сдвиг руки при прицеливании (опустить, поднести к глазам).
        [Tooltip("Translation to apply to weapon arm when aiming with this weapon")]
        public Vector3 AimOffset;

        [Header("Visual")]
        [Tooltip("Optional weapon animator for OnShoot animations")]
        public Animator WeaponAnimator;

        [Tooltip("Prefab of the muzzle flash")]
        public GameObject MuzzleFlashPrefab;

        // Если true — вспышка дула отвязывается от оружия (полезно для
        // дальних эффектов: при движении оружие уехало, а вспышка осталась
        // на месте выстрела).
        [Tooltip("Unparent the muzzle flash instance on spawn")]
        public bool UnparentMuzzleFlash;

        // OnShoot — UnityAction для UI/анимаций; OnShootProcessed (event Action) —
        // системы вроде PlayerWeaponsManager используют его для recoil.
        // Разделено для нюансов API (event защищает от случайной перезаписи).
        public UnityAction OnShoot;
        public event Action OnShootProcessed;

        // Владелец оружия (игрок). Прокидывается снарядам, чтобы не нанести
        // самому себе урон и чтобы AI знал кто стрелял.
        public GameObject Owner { get; set; }
        // Префаб, из которого оружие создано — для логики смены оружия.
        public GameObject SourcePrefab { get; set; }
        // Активно ли сейчас (видимо ли).
        public bool IsWeaponActive { get; private set; }
        // Текущая мировая скорость дула (важно для наследования снарядом).
        public Vector3 MuzzleWorldVelocity { get; private set; }

        // Proxies to sub-modules so callers don't need to know which module owns the state.
        // Эти прокси-свойства — фасад: внешнему коду (UI, PlayerWeaponsManager)
        // не нужно знать о существовании модулей. Если завтра логику ammo
        // перенесём ещё куда-то — поправим только тут, остальные останутся.
        public float CurrentAmmoRatio => m_AmmoModule.CurrentAmmoRatio;
        public bool IsReloading => m_AmmoModule.IsReloading;
        public bool IsCooling => m_AmmoModule.IsCooling;
        public bool AutomaticReload => m_AmmoModule.AutomaticReload;
        public bool HasPhysicalBullets => m_AmmoModule.HasPhysicalBullets;
        public bool IsCharging => m_ChargeModule.IsCharging;
        public float CurrentCharge => m_ChargeModule.CurrentCharge;
        public float LastChargeTriggerTimestamp => m_ChargeModule.LastChargeTriggerTimestamp;

        // Имя параметра аниматора для триггера атаки. const — нет выделения памяти
        // на каждой записи, плюс защита от опечаток через имя в одном месте.
        const string k_AnimAttackParameter = "Attack";

        WeaponAmmoModule m_AmmoModule;
        WeaponChargeModule m_ChargeModule;
        WeaponAudioModule m_AudioModule;
        // -infinity — чтобы первое условие «прошёл DelayBetweenShots с момента
        // последнего выстрела» сразу выполнилось.
        float m_LastTimeShot = Mathf.NegativeInfinity;
        Vector3 m_LastMuzzlePosition;

        void Awake()
        {
            // Кешируем модули. RequireComponent гарантирует, что они есть.
            m_AmmoModule = GetComponent<WeaponAmmoModule>();
            m_ChargeModule = GetComponent<WeaponChargeModule>();
            m_AudioModule = GetComponent<WeaponAudioModule>();

            m_LastMuzzlePosition = WeaponMuzzle.position;
        }

        void Update()
        {
            // Каждый кадр прогоняем модули: ammo тикает регенерацией,
            // charge накапливается, audio управляет лупом стрельбы.
            m_AmmoModule.UpdateAmmo(m_LastTimeShot, m_ChargeModule.IsCharging);
            m_ChargeModule.UpdateCharge(m_AmmoModule);
            m_AudioModule.UpdateContinuousShootSound(m_AmmoModule.GetCurrentAmmo());

            // Считаем мировую скорость дула: (текущая позиция - прошлая) / dt.
            // Защита от dt=0 — на первом кадре или в паузе.
            if (Time.deltaTime > 0)
            {
                MuzzleWorldVelocity = (WeaponMuzzle.position - m_LastMuzzlePosition) / Time.deltaTime;
                m_LastMuzzlePosition = WeaponMuzzle.position;
            }
        }

        // Показать/скрыть оружие. Вызывается PlayerWeaponsManager при смене.
        public void ShowWeapon(bool show)
        {
            WeaponRoot.SetActive(show);
            if (show) m_AudioModule.PlayChangeSfx();
            IsWeaponActive = show;
        }

        // Обработка ввода: пришли три булева флага «нажал/держит/отпустил».
        // Возвращает true, если выстрел произошёл — нужно для recoil-обработки.
        public bool HandleShootInputs(bool inputDown, bool inputHeld, bool inputUp)
        {
            // Аудио-модулю говорим «игрок хочет стрелять» — для луп-звука.
            m_AudioModule.SetWantsToShoot(inputDown || inputHeld);
            switch (ShootType)
            {
                case WeaponShootType.Manual:
                    // Только на момент нажатия. Зажатие игнорируется.
                    if (inputDown) return TryShoot();
                    return false;

                case WeaponShootType.Automatic:
                    // Пока зажато — стреляем (с кулдауном DelayBetweenShots).
                    if (inputHeld) return TryShoot();
                    return false;

                case WeaponShootType.Charge:
                    // Накапливаем заряд пока держат.
                    if (inputHeld)
                        m_ChargeModule.TryBeginCharge(m_AmmoModule, DelayBetweenShots, m_LastTimeShot, BulletsPerShot);
                    // Стреляем на отпускание ИЛИ автоматически при максимуме.
                    if (inputUp || (m_ChargeModule.AutomaticReleaseOnCharged && m_ChargeModule.CurrentCharge >= 1f))
                        return TryReleaseCharge();
                    return false;

                default:
                    return false;
            }
        }

        // Попытка выстрелить: проверяем патроны и кулдаун.
        bool TryShoot()
        {
            // GetCurrentAmmo возвращает float (для дробной перезарядки).
            // >=1 — есть хотя бы один полный патрон.
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
            // Стреляем только если действительно копили.
            if (m_ChargeModule.IsCharging)
            {
                HandleShoot();
                m_ChargeModule.ReleaseCharge();
                return true;
            }
            return false;
        }

        // Сам выстрел: спавн пуль, вспышки, звук, анимация.
        void HandleShoot()
        {
            // Для заряженного оружия число пуль масштабируется текущим зарядом.
            // Ceil — даже 0.1 заряда даст хотя бы 1 пулю.
            int bulletsPerShotFinal = ShootType == WeaponShootType.Charge
                ? Mathf.CeilToInt(m_ChargeModule.CurrentCharge * BulletsPerShot)
                : BulletsPerShot;

            for (int i = 0; i < bulletsPerShotFinal; i++)
            {
                // Каждая пуля летит со своим случайным отклонением (разброс).
                Vector3 shotDirection = GetShotDirectionWithinSpread(WeaponMuzzle);
                ProjectileBase newProjectile;
                // Пытаемся достать из пула — если пул живой. Иначе Instantiate
                // как fallback (например, на старте сцены пул мог быть не готов).
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
                // Инициализируем пулю — она запомнит владельца и скорости.
                newProjectile.Shoot(this);
            }

            // Вспышка дула — тоже пулится.
            if (MuzzleFlashPrefab != null)
            {
                if (GameObjectPoolManager.Instance != null)
                {
                    var muzzleFlashInstance = GameObjectPoolManager.Instance.Get(MuzzleFlashPrefab,
                        WeaponMuzzle.position, WeaponMuzzle.rotation);
                    // SetParent: либо к дулу (вспышка едет с оружием), либо null (остаётся в мире).
                    muzzleFlashInstance.transform.SetParent(UnparentMuzzleFlash ? null : WeaponMuzzle.transform);
                    // Only use timed release if no particle system — otherwise PooledParticleAutoRelease handles it
                    // Если есть PooledParticleAutoRelease — он сам вернёт в пул по окончании частиц.
                    // Если частиц нет — возвращаем через 2 секунды по таймеру.
                    if (muzzleFlashInstance.GetComponent<PooledParticleAutoRelease>() == null)
                        GameObjectPoolManager.Instance.ReleaseDelayed(muzzleFlashInstance, 2f);
                }
                else
                {
                    // Fallback без пула: обычный Instantiate + Destroy с таймером.
                    GameObject muzzleFlashInstance = Instantiate(MuzzleFlashPrefab, WeaponMuzzle.position,
                        WeaponMuzzle.rotation, WeaponMuzzle.transform);
                    if (UnparentMuzzleFlash)
                        muzzleFlashInstance.transform.SetParent(null);
                    Destroy(muzzleFlashInstance, 2f);
                }
            }

            // Выкидываем гильзу.
            m_AmmoModule.EjectShell();
            m_LastTimeShot = Time.time;
            m_AudioModule.PlayShootSfx();

            // Дёргаем триггер анимации, если аниматор есть.
            if (WeaponAnimator)
                WeaponAnimator.SetTrigger(k_AnimAttackParameter);

            // События для внешних слушателей.
            OnShoot?.Invoke();
            OnShootProcessed?.Invoke();
        }

        // Вычисляет направление пули с учётом разброса.
        // spreadAngleRatio: 0 → точно по forward, 1 → совсем случайно в сфере.
        // Slerp по направлениям интерполирует на сфере, давая равномерный конус.
        public Vector3 GetShotDirectionWithinSpread(Transform shootTransform)
        {
            float spreadAngleRatio = BulletSpreadAngle / 180f;
            return Vector3.Slerp(shootTransform.forward, UnityEngine.Random.insideUnitSphere, spreadAngleRatio);
        }

        // Forwarded methods — preserve public API for external callers.
        // Прокси-методы к модулям. См. комментарий выше про прокси-свойства.
        public void UseAmmo(float amount) => m_AmmoModule.UseAmmo(amount);
        public int GetCurrentAmmo() => m_AmmoModule.GetCurrentAmmoInt();
        public int GetCarriedPhysicalBullets() => m_AmmoModule.GetCarriedPhysicalBullets();
        public void AddCarriablePhysicalBullets(int count) => m_AmmoModule.AddCarriablePhysicalBullets(count);
        public float GetAmmoNeededToShoot() =>
            m_AmmoModule.GetAmmoNeededToShoot(ShootType, m_ChargeModule.AmmoUsedOnStartCharge, BulletsPerShot);
        public void StartReloadAnimation() => m_AmmoModule.StartReloadAnimation(GetComponent<Animator>());
    }
}
