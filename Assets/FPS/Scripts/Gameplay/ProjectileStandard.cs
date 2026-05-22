using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.Gameplay
{
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

        [Tooltip(
            "Distance over which the projectile will correct its course to fit the intended trajectory (used to drift projectiles towards center of screen in First Person view). At values under 0, there is no correction")]
        public float TrajectoryCorrectionDistance = -1;

        [Tooltip("Determines if the projectile inherits the velocity that the weapon's muzzle had when firing")]
        public bool InheritWeaponVelocity = false;

        [Header("Damage")] [Tooltip("Damage of the projectile")]
        public float Damage = 40f;

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
        List<Collider> m_IgnoredColliders;
        bool m_Returned;

        // Shared buffer — projectiles update sequentially on main thread
        static readonly RaycastHit[] s_HitBuffer = new RaycastHit[16];

        const QueryTriggerInteraction k_TriggerInteraction = QueryTriggerInteraction.Collide;

        void Awake()
        {
            m_ProjectileBase = GetComponent<ProjectileBase>();
            m_IgnoredColliders = new List<Collider>();
        }

        void OnEnable()
        {
            DebugUtility.HandleErrorIfNullGetComponent<ProjectileBase, ProjectileStandard>(m_ProjectileBase, this,
                gameObject);

            m_ProjectileBase.OnShoot += OnShoot;
            m_Returned = false;
            m_ShootTime = Time.time;
        }

        void OnDisable()
        {
            m_ProjectileBase.OnShoot -= OnShoot;
        }

        new void OnShoot()
        {
            m_LastRootPosition = Root.position;
            m_Velocity = transform.forward * Speed;
            m_IgnoredColliders.Clear();
            transform.position += m_ProjectileBase.InheritedMuzzleVelocity * Time.deltaTime;

            // Ignore colliders of owner
            Collider[] ownerColliders = m_ProjectileBase.Owner.GetComponentsInChildren<Collider>();
            m_IgnoredColliders.AddRange(ownerColliders);

            // Handle case of player shooting (make projectiles not go through walls, and remember center-of-screen trajectory)
            PlayerWeaponsManager playerWeaponsManager = m_ProjectileBase.Owner.GetComponent<PlayerWeaponsManager>();
            if (playerWeaponsManager)
            {
                m_HasTrajectoryOverride = true;

                Vector3 cameraToMuzzle = (m_ProjectileBase.InitialPosition -
                                          playerWeaponsManager.WeaponCamera.transform.position);

                m_TrajectoryCorrectionVector = Vector3.ProjectOnPlane(-cameraToMuzzle,
                    playerWeaponsManager.WeaponCamera.transform.forward);
                if (TrajectoryCorrectionDistance == 0)
                {
                    transform.position += m_TrajectoryCorrectionVector;
                    m_ConsumedTrajectoryCorrectionVector = m_TrajectoryCorrectionVector;
                }
                else if (TrajectoryCorrectionDistance < 0)
                {
                    m_HasTrajectoryOverride = false;
                }

                if (Physics.Raycast(playerWeaponsManager.WeaponCamera.transform.position, cameraToMuzzle.normalized,
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
            if (m_HasTrajectoryOverride && m_ConsumedTrajectoryCorrectionVector.sqrMagnitude <
                m_TrajectoryCorrectionVector.sqrMagnitude)
            {
                Vector3 correctionLeft = m_TrajectoryCorrectionVector - m_ConsumedTrajectoryCorrectionVector;
                float distanceThisFrame = (Root.position - m_LastRootPosition).magnitude;
                Vector3 correctionThisFrame =
                    (distanceThisFrame / TrajectoryCorrectionDistance) * m_TrajectoryCorrectionVector;
                correctionThisFrame = Vector3.ClampMagnitude(correctionThisFrame, correctionLeft.magnitude);
                m_ConsumedTrajectoryCorrectionVector += correctionThisFrame;

                if (m_ConsumedTrajectoryCorrectionVector.sqrMagnitude == m_TrajectoryCorrectionVector.sqrMagnitude)
                    m_HasTrajectoryOverride = false;

                transform.position += correctionThisFrame;
            }

            // Orient towards velocity
            transform.forward = m_Velocity.normalized;

            // Gravity
            if (GravityDownAcceleration > 0)
                m_Velocity += Vector3.down * GravityDownAcceleration * Time.deltaTime;

            // Hit detection
            {
                RaycastHit closestHit = new RaycastHit();
                closestHit.distance = Mathf.Infinity;
                bool foundHit = false;

                Vector3 displacementSinceLastFrame = Tip.position - m_LastRootPosition;
                int hitCount = Physics.SphereCastNonAlloc(m_LastRootPosition, Radius,
                    displacementSinceLastFrame.normalized, s_HitBuffer, displacementSinceLastFrame.magnitude,
                    HittableLayers, k_TriggerInteraction);

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
                    if (closestHit.distance <= 0f)
                    {
                        closestHit.point = Root.position;
                        closestHit.normal = -transform.forward;
                    }

                    OnHit(closestHit.point, closestHit.normal, closestHit.collider);
                }
            }

            m_LastRootPosition = Root.position;
        }

        bool IsHitValid(RaycastHit hit)
        {
            if (hit.collider.GetComponent<IgnoreHitDetection>())
                return false;

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
                AreaOfDamage.InflictDamageInArea(Damage, point, HittableLayers, k_TriggerInteraction,
                    m_ProjectileBase.Owner);
            }
            else
            {
                Damageable damageable = collider.GetComponent<Damageable>();
                if (damageable)
                    damageable.InflictDamage(Damage, false, m_ProjectileBase.Owner);
            }

            // impact vfx
            if (ImpactVfx)
            {
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
            if (ImpactSfxClip)
                AudioUtility.CreateSFX(ImpactSfxClip, point, AudioUtility.AudioGroups.Impact, 1f, 3f);

            ReturnToPool();
        }

        void ReturnToPool()
        {
            if (m_Returned) return;
            m_Returned = true;

            if (GameObjectPoolManager.Instance != null)
                GameObjectPoolManager.Instance.Release(gameObject);
            else
                Destroy(gameObject);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = RadiusColor;
            Gizmos.DrawSphere(transform.position, Radius);
        }
    }
}
