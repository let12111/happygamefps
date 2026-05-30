using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.Gameplay
{
    // ============================================================================
    // ProjectileStandard — стандартный снаряд (пуля). Наследует ProjectileBase
    // (там Owner, InitialPosition, OnShoot и т.д.) и добавляет:
    //  - физическое движение по прямой/параболе (с гравитацией опционально);
    //  - детект попаданий через SphereCastNonAlloc — точнее чем точечный райкаст
    //    и не пропускает быстрые цели;
    //  - спавн VFX/SFX на попадание;
    //  - игнорирование своего владельца (не убиваем себя);
    //  - «коррекцию траектории» для FPS: пуля выходит из дула, но корректируется
    //    в направлении центра экрана, чтобы попадать туда, где прицел.
    //  - возврат в пул через ReturnToPool вместо Destroy.
    // ============================================================================
    public class ProjectileStandard : ProjectileBase
    {
        [Header("General")] [Tooltip("Radius of this projectile's collision detection")]
        public float Radius = 0.01f;

        [Tooltip("Transform representing the root of the projectile (used for accurate collision detection)")]
        public Transform Root;

        [Tooltip("Transform representing the tip of the projectile (used for accurate collision detection)")]
        public Transform Tip;

        [Tooltip("LifeTime of the projectile")]
        public float MaxLifeTime = 5f;

        [Tooltip("VFX prefab to spawn upon impact")]
        public GameObject ImpactVfx;

        [Tooltip("LifeTime of the VFX before being destroyed")]
        public float ImpactVfxLifetime = 5f;

        // Смещение позиции VFX от точки попадания по нормали — чтобы эффект
        // не лез в стену, а торчал из неё.
        [Tooltip("Offset along the hit normal where the VFX will be spawned")]
        public float ImpactVfxSpawnOffset = 0.1f;

        [Tooltip("Clip to play on impact")]
        public AudioClip ImpactSfxClip;

        [Tooltip("Layers this projectile can collide with")]
        public LayerMask HittableLayers = -1;

        [Header("Movement")] [Tooltip("Speed of the projectile")]
        public float Speed = 20f;

        [Tooltip("Downward acceleration from gravity")]
        public float GravityDownAcceleration = 0f;

        // -1 значит «без коррекции». Иначе чем меньше — тем агрессивнее.
        [Tooltip(
            "Distance over which the projectile will correct its course to fit the intended trajectory (used to drift projectiles towards center of screen in First Person view). At values under 0, there is no correction")]
        public float TrajectoryCorrectionDistance = -1;

        [Tooltip("Determines if the projectile inherits the velocity that the weapon's muzzle had when firing")]
        public bool InheritWeaponVelocity = false;

        [Header("Damage")] [Tooltip("Damage of the projectile")]
        public float Damage = 40f;

        // Если задано — взрывной урон через DamageArea. Иначе обычный одиночный.
        [Tooltip("Area of damage. Keep empty if you don<t want area damage")]
        public DamageArea AreaOfDamage;

        [Header("Debug")] [Tooltip("Color of the projectile radius debug view")]
        public Color RadiusColor = Color.cyan * 0.2f;

        ProjectileBase m_ProjectileBase;
        Vector3 m_LastRootPosition;
        Vector3 m_Velocity;
        bool m_HasTrajectoryOverride;
        float m_ShootTime;
        Vector3 m_TrajectoryCorrectionVector;
        Vector3 m_ConsumedTrajectoryCorrectionVector;
        // HashSet, потому что мы делаем Contains часто — у Set это O(1).
        HashSet<Collider> m_IgnoredColliders;
        // Защита от двойного возврата: если попадание и таймаут случились в одном кадре.
        bool m_Returned;

        Camera m_Camera;
        GameObject m_CachedOwner;
        // Кеш коллайдеров владельца — собирается ОДИН раз на смену владельца,
        // не GetComponentsInChildren на каждый выстрел.
        Collider[] m_CachedOwnerColliders;
        bool m_IsPlayerOwner;

        // Shared buffer — projectiles update sequentially on main thread
        // Один общий буфер на все снаряды — Unity-апдейты последовательны, никто
        // не вломится в чужой кадр.
        static readonly RaycastHit[] s_HitBuffer = new RaycastHit[16];

        // Collide — снаряд столкнётся даже с триггерами. Это нужно для урона
        // через коллайдеры-триггеры на хитбоксах врагов.
        const QueryTriggerInteraction k_TriggerInteraction = QueryTriggerInteraction.Collide;

        void Awake()
        {
            m_ProjectileBase = GetComponent<ProjectileBase>();
            m_IgnoredColliders = new HashSet<Collider>();
            // Camera.main внутри вызывает FindWithTag — дорого, делаем ОДИН раз.
            m_Camera = Camera.main;
        }

        // OnEnable вызывается каждый раз при выдаче из пула. Тут переинициализируем
        // состояние, чтобы не унаследовать данные предыдущего выстрела.
        void OnEnable()
        {
            DebugUtility.HandleErrorIfNullGetComponent<ProjectileBase, ProjectileStandard>(m_ProjectileBase, this,
                gameObject);

            // Подписываемся на OnShoot — он сработает когда WeaponController вызовет Shoot().
            m_ProjectileBase.OnShoot += OnShoot;
            m_Returned = false;
            m_ShootTime = Time.time;
        }

        // ОБЯЗАТЕЛЬНО отписываемся при возврате в пул, иначе подписки накапливаются
        // и на следующий выстрел OnShoot выполнится дважды/трижды.
        void OnDisable()
        {
            m_ProjectileBase.OnShoot -= OnShoot;
        }

        // 'new' — намеренно скрываем виртуальный OnShoot базы (если бы он был).
        new void OnShoot()
        {
            m_LastRootPosition = Root.position;
            m_Velocity = transform.forward * Speed;
            m_IgnoredColliders.Clear();
            // Учитываем скорость движения дула в момент выстрела — пуля не «отстаёт».
            transform.position += m_ProjectileBase.InheritedMuzzleVelocity * Time.deltaTime;

            // Re-fetch owner colliders only when owner changes (avoids GetComponentsInChildren every shot)
            // Если стреляет тот же владелец что и раньше — переиспользуем кеш.
            GameObject owner = m_ProjectileBase.Owner;
            if (owner != m_CachedOwner)
            {
                m_CachedOwner = owner;
                m_CachedOwnerColliders = owner.GetComponentsInChildren<Collider>();
                m_IsPlayerOwner = owner.GetComponent<PlayerCharacterController>() != null;
            }
            // Игнорируем все коллайдеры владельца — иначе пуля сразу столкнётся
            // с самим стрелком и взорвётся.
            foreach (var c in m_CachedOwnerColliders) m_IgnoredColliders.Add(c);

            // Handle case of player shooting (make projectiles not go through walls, and remember center-of-screen trajectory)
            // Особый случай для игрока: чтобы пули попадали туда, куда смотрит прицел,
            // а не туда, куда показывает дуло (они в разных точках).
            if (m_Camera != null && m_IsPlayerOwner)
            {
                m_HasTrajectoryOverride = true;

                // Вектор от камеры к дулу.
                Vector3 cameraToMuzzle = (m_ProjectileBase.InitialPosition -
                                          m_Camera.transform.position);

                // Перпендикулярная составляющая (то, что надо скомпенсировать).
                m_TrajectoryCorrectionVector = Vector3.ProjectOnPlane(-cameraToMuzzle,
                    m_Camera.transform.forward);
                if (TrajectoryCorrectionDistance == 0)
                {
                    // Моментальная коррекция — пуля сразу «прыгает» в центр.
                    transform.position += m_TrajectoryCorrectionVector;
                    m_ConsumedTrajectoryCorrectionVector = m_TrajectoryCorrectionVector;
                }
                else if (TrajectoryCorrectionDistance < 0)
                {
                    // Коррекция отключена.
                    m_HasTrajectoryOverride = false;
                }

                // Защита от стрельбы через стену: если между камерой и дулом
                // что-то есть — снаряд «попадает» в это прямо сейчас.
                if (Physics.Raycast(m_Camera.transform.position, cameraToMuzzle.normalized,
                    out RaycastHit hit, cameraToMuzzle.magnitude, HittableLayers, k_TriggerInteraction))
                {
                    if (IsHitValid(hit))
                    {
                        OnHit(hit.point, hit.normal, hit.collider);
                    }
                }
            }
        }

        void Update()
        {
            // Таймаут жизни — даже если не во что не попал, снаряд исчезает.
            if (Time.time - m_ShootTime >= MaxLifeTime)
            {
                ReturnToPool();
                return;
            }

            // Move
            transform.position += m_Velocity * Time.deltaTime;
            if (InheritWeaponVelocity)
                transform.position += m_ProjectileBase.InheritedMuzzleVelocity * Time.deltaTime;

            // Drift towards trajectory override
            // Плавная коррекция к центру экрана. Не вся сразу — иначе видно «скачок».
            if (m_HasTrajectoryOverride && m_ConsumedTrajectoryCorrectionVector.sqrMagnitude <
                m_TrajectoryCorrectionVector.sqrMagnitude)
            {
                Vector3 correctionLeft = m_TrajectoryCorrectionVector - m_ConsumedTrajectoryCorrectionVector;
                float distanceThisFrame = (Root.position - m_LastRootPosition).magnitude;
                // Доля коррекции, пропорциональная пройденному пути.
                Vector3 correctionThisFrame =
                    (distanceThisFrame / TrajectoryCorrectionDistance) * m_TrajectoryCorrectionVector;
                correctionThisFrame = Vector3.ClampMagnitude(correctionThisFrame, correctionLeft.magnitude);
                m_ConsumedTrajectoryCorrectionVector += correctionThisFrame;

                if (m_ConsumedTrajectoryCorrectionVector.sqrMagnitude == m_TrajectoryCorrectionVector.sqrMagnitude)
                    m_HasTrajectoryOverride = false;

                transform.position += correctionThisFrame;
            }

            // Orient towards velocity
            // Пуля «смотрит» туда, куда летит — нужно для ровного спрайта/меша.
            transform.forward = m_Velocity.normalized;

            // Gravity
            // Опционально: гранатам делает дугу.
            if (GravityDownAcceleration > 0)
                m_Velocity += Vector3.down * GravityDownAcceleration * Time.deltaTime;

            // Hit detection
            {
                RaycastHit closestHit = new RaycastHit();
                closestHit.distance = Mathf.Infinity;
                bool foundHit = false;

                // Смещение Tip за кадр — длина и направление SphereCast'а.
                Vector3 displacementSinceLastFrame = Tip.position - m_LastRootPosition;
                // SphereCastNonAlloc — луч с радиусом. NonAlloc избегает аллокации массива.
                int hitCount = Physics.SphereCastNonAlloc(m_LastRootPosition, Radius,
                    displacementSinceLastFrame.normalized, s_HitBuffer, displacementSinceLastFrame.magnitude,
                    HittableLayers, k_TriggerInteraction);

                // Ищем ближайшее валидное попадание.
                for (int i = 0; i < hitCount; i++)
                {
                    var hit = s_HitBuffer[i];
                    if (IsHitValid(hit) && hit.distance < closestHit.distance)
                    {
                        foundHit = true;
                        closestHit = hit;
                    }
                }

                if (foundHit)
                {
                    // distance=0 значит попадание прямо в стартовой точке (овершут).
                    // Восстанавливаем разумные point и normal.
                    if (closestHit.distance <= 0f)
                    {
                        closestHit.point = Root.position;
                        closestHit.normal = -transform.forward;
                    }

                    OnHit(closestHit.point, closestHit.normal, closestHit.collider);
                }
            }

            // Запоминаем для следующего кадра.
            m_LastRootPosition = Root.position;
        }

        // Валидно ли попадание (не наш ли это коллайдер, не помеченный ли IgnoreHitDetection).
        bool IsHitValid(RaycastHit hit)
        {
            if (hit.collider.GetComponent<IgnoreHitDetection>())
                return false;

            // Триггер без Damageable пропускаем (например, триггеры зон).
            if (hit.collider.isTrigger && hit.collider.GetComponent<Damageable>() == null)
                return false;

            if (m_IgnoredColliders != null && m_IgnoredColliders.Contains(hit.collider))
                return false;

            return true;
        }

        void OnHit(Vector3 point, Vector3 normal, Collider collider)
        {
            // damage
            if (AreaOfDamage)
            {
                // Взрыв — урон по радиусу.
                AreaOfDamage.InflictDamageInArea(Damage, point, HittableLayers, k_TriggerInteraction,
                    m_ProjectileBase.Owner);
            }
            else
            {
                // Обычный одиночный урон через Damageable цели.
                Damageable damageable = collider.GetComponent<Damageable>();
                if (damageable)
                    damageable.InflictDamage(Damage, false, m_ProjectileBase.Owner);
            }

            // impact vfx
            if (ImpactVfx)
            {
                // Сдвигаем VFX от стены по нормали и поворачиваем «лицом» от стены.
                Vector3 spawnPos = point + (normal * ImpactVfxSpawnOffset);
                Quaternion spawnRot = Quaternion.LookRotation(normal);

                if (GameObjectPoolManager.Instance != null)
                {
                    GameObjectPoolManager.Instance.Get(ImpactVfx, spawnPos, spawnRot);
                    // PooledParticleAutoRelease handles return automatically when particles finish
                }
                else
                {
                    GameObject vfx = Instantiate(ImpactVfx, spawnPos, spawnRot);
                    if (ImpactVfxLifetime > 0)
                        Destroy(vfx, ImpactVfxLifetime);
                }
            }

            // impact sfx
            // spatialBlend=1 — 3D-звук, чтобы было слышно откуда попадание.
            if (ImpactSfxClip)
                AudioUtility.CreateSFX(ImpactSfxClip, point, AudioUtility.AudioGroups.Impact, 1f, 3f);

            ReturnToPool();
        }

        void ReturnToPool()
        {
            // Защита от двойного возврата (попадание и таймаут в одном кадре).
            if (m_Returned) return;
            m_Returned = true;

            if (GameObjectPoolManager.Instance != null)
                GameObjectPoolManager.Instance.Release(gameObject);
            else
                Destroy(gameObject);
        }

        // Визуализация радиуса в редакторе.
        void OnDrawGizmosSelected()
        {
            Gizmos.color = RadiusColor;
            Gizmos.DrawSphere(transform.position, Radius);
        }
    }
}
